using System;
using System.Collections.Generic;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
using SteveTheTroll.Features.Trolls.Farming.Application;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Farming.Execution;
using SteveTheTroll.Features.Trolls.Orders.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using SteveTheTroll.Features.Trolls.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>
///     The farming state of one tamed troll (owner client; T-008d, D-048..D-051). Search (D-051, user): quick check within
///     QuickRange of the troll - the focus item first, then
///     the other allowed items ("Farming now" changes); none -> FarmRange, then MaxRange, then the long-range search
///     (D-163); nothing -> it waits and searches again (D-149).
///     Hitting (D-051): a try = up to 2 swings; no damage -> step forward and try again, 4 tries, then give the source up
///     (skipped BadSeconds). A follow order or a dead troll
///     ends farming; a fight (target creature) hands the AI back for that frame.
///     D-170 (Architecture V2, farming slice): the Infrastructure adapter. It reads the facts into a FarmGateInput,
///     FarmingPipeline decides (owner check, then the priority order
///     of FarmGateRules), FarmingExecutor performs (owner checked again); the approach (ApproachRules / ApproachMeter),
///     the dry-swing ladder (SwingLadder) and the search order
///     (TargetSelection) are game-free domain code. Movement goes through TrollContext's capability; reservations, aim,
///     friendly-troll detour, spot memory and the home trip
///     are helpers in this folder.
/// </summary>
internal sealed partial class TrollFarmer : MonoBehaviour
{
    private const float SearchSeconds = 3f,
        LogSeconds = 5f,
        LookAngle = 25f,
        MinAttackGap = 0.3f,
        BadSeconds = 120f,
        StepMetres = 2f,
        StepSeconds = 2.5f,
        FullCheckSeconds = 2f;

    private const float TravelArriveMetres = 12f, TravelMaxSeconds = 420f, NothingWaitSeconds = 20f;
    internal const float FarWalkMetres = 80f;

    /// D-163: the remembered farm spot (ZDO key; FarmSpotMemory reads / writes it).
    internal const string SpotKey = FarmSpotMemory.SpotKey;

    /// D-150 (test run 014829: the game destroyed and re-created far trolls and the troll wandered for the 90 s of the idle wait): the ZDO remembers that this troll was farming.
    internal const string FarmingKey = "stt_farming";

    // D-175 (run 181331: walking back to its remembered spot the troll crossed into a reloaded zone, its object was re-created, the new TrollFarmer knew nothing of the
    // walk and farmed where it stood, 60 m short): the walk is flagged in the troll's ZDO (owner write); a new farm job with the flag set resumes the walk.
    internal const string SpotWalkKey = "stt_spot_walk";
    private readonly ApproachMeter _approach = new();

    private readonly Dictionary<GameObject, float> _bad = new();
    private readonly List<TrollFarming.Candidate> _cands = new();
    private readonly FarmFriendDetour _friend = new();
    private readonly SwingLadder _ladder = new();
    private MonsterAI _ai;
    private Vector3 _centre, _stepTo, _travelTo;
    private TrollContext _ctx;

    private float
        _dt, _aimDy; // the frame being executed (the bound actions read it); height of the aim point above the troll (m)

    private FarmingExecutor _executor;
    private string _focus = "";
    private Collider _hit;
    private bool _home, _announce, _saidNothing, _toSpot;
    private float _exploreHeading = float.NaN; // D-189: the direction a farmed-out troll walks on in (NaN = not exploring)
    private int _exploreSteps;
    internal static int ExploreStepsTotal; // D-189: the lab reads it
    private Humanoid _me;
    private float _nextSearch, _nextAttack, _nextLog, _stepUntil, _nextFullCheck, _travelStart, _nextNothingLog;
    private FarmingPipeline _pipeline;
    private GameObject _target;
    private GameObject _ordered; // the source an order named (Force) - kept across fights (Retarget)

    /// The source this farmer works on now (null = none). D-177: read by the stt.farmersOn test hook.
    internal GameObject Target => _target;

    /// The farmer walks home / waits there (the home order or slippers off). D-180: read by the stt.goingHome test hook.
    internal bool GoingHome => _home;
    private FarmTool _tool;
    private FarmHomeTrip _trip;
    private ItemDrop.ItemData _weapon;

    internal FarmSpotMemory Spots { get; private set; }
    internal ITrollMovement Movement => _ctx.Capabilities.Movement;

    /// Full backpack: on its way to unload (D-062).
    internal bool IsFull { get; set; }

    private GameObject _rememberedFor; // the source whose place was written as the spot (once per source)

    /// Went farming on its own (D-058 / D-062: no "nothing found" / "home" messages).
    internal bool Auto { get; set; }

    /// Walking to a far source or back to the remembered spot (test hooks and the status text).
    internal bool Travelling { get; private set; }

    /// D-159: on its way to unload (hub / bed -> depot).
    internal bool Unloading => _home && IsFull;

    internal string CurrentItem { get; private set; } = "";

    /// Swings started since this farmer exists (TestLab hook stt.farmAttacks).
    internal int Attacks { get; private set; }

    private int Me => GetInstanceID();

    private void Awake()
    {
        _ctx = new TrollContext(GetComponent<Character>());
        Spots = new FarmSpotMemory(this);
        _trip = new FarmHomeTrip(this);
        _tool = new FarmTool(this);
        _pipeline = new FarmingPipeline();
        // the executor owns the actions: bound once here (method groups), never per frame
        _executor = new FarmingExecutor(_ctx, new FarmingActions
        {
            Stop = OnStop,
            StopFollow = OnStopFollow,
            YieldToFight = OnYieldToFight,
            RecoveryDrives = OnRecovery,
            CheckFull = Full,
            GoHome = OnGoHome,
            NoWeapon = OnNoWeapon,
            Work = Work,
            Swing = OnSwing,
            StepForward = OnStepForward,
            GiveUp = OnGiveUpDry
        });
        ModLog.Verbose("TrollFarmer: " + TrollTaming.Tag(this) +
                       " set up (Architecture V2: farming pipeline + executor, capabilities complete " +
                       _ctx.Capabilities.Complete + ")");
    }

    private void OnDestroy()
    {
        CurrentItem = "";
        Release();
        TrollRecovery rc = GetComponent<TrollRecovery>();
        if (rc != null)
        {
            rc.Forget();
        }
    }

    /// `announce` (D-058): the troll starts on its own after waiting without orders - its owner gets a message with the item it found.
    internal void Setup(string focus, Vector3 centre, bool announce = false)
    {
        if (Travelling && _toSpot)
        {
            FarmHomeTrip.Note(GetComponent<Character>(), TripGrammar.SpotCancelled, "new setup / order");
        }

        if (!announce)
        {
            SetSpotWalk(
                false); // D-175: an order (or ResumeAtSpot, which starts a new walk right after) ends an old walk to the spot
        }

        _focus = focus ?? "";
        _centre = centre;
        _target = null;
        _ordered = null; // a new order / setup ends the old ordered source
        _hit = null;
        _nextSearch = 0f;
        CurrentItem = "";
        _home = false;
        _announce = announce;
        Auto = announce;
        Travelling = false;
        ResetTries();
        MarkFarming(true);
    }

    private void MarkFarming(bool on)
    {
        ZNetView view = GetComponent<ZNetView>();
        if (view != null && view.IsValid() && view.IsOwner())
        {
            view.GetZDO().Set(FarmingKey, on);
        }
    }

    /// D-084: after a fight - look for the nearest source again from where the troll stands now.
    /// 2026-11-05 (gate run 1, recovery-walled-tree: a Greyling attacked the owner mid-order, the guard chase ended and the troll farmed random trees for 6 minutes):
    /// a troll that had been ORDERED to break a source goes back to it when it still exists and the backpack is not full. Expected outcome: true = back to the ordered source.
    internal bool Retarget()
    {
        GameObject ordered = _ordered;
        _target = null;
        _hit = null;
        _nextSearch = 0f;
        _home = IsFull;
        ResetTries();
        _centre = transform.position;
        return ordered != null && !IsFull && Force(ordered); // Unity null: a source broken meanwhile is gone -> the normal search
    }

    /// D-143 (user 2026-10-31: after the logout Steve 1 ran around the base until he got an order instead of emptying his backpack): to the hub / bed and into the depot, the
    /// same way as a full backpack, then it is done (the next idle check sends it farming). Expected outcome: none.
    internal void SetupUnload()
    {
        Setup("", transform.position, true);
        MarkFarming(false);
        _home = true;
        IsFull = true;
    }

    /// T-008e: no farming, straight to the bed (silent).
    internal void SetupHome()
    {
        Setup("", transform.position, true);
        MarkFarming(false);
        _home = true;
    }

    /// D-065: the attack key named this source. Expected outcome: whether it became the target (it must be breakable for the troll).
    internal bool Force(GameObject source)
    {
        Collider col = source != null ? TrollFarming.Biggest(source) : null;
        if (col == null)
        {
            ModLog.Warn("TrollFarmer: " + TrollTaming.Tag(this) + " cannot farm " +
                        (source != null ? source.name : "?") + " (no collider)");
            return col != null;
        }

        string item = ReadableName.FromPrefab(Utils.GetPrefabName(source)); // D-214: English words when it yields none of the known items
        foreach (TrollFocus.Goal g in TrollFocus.DefaultOrder)
        {
            if (TrollFarming.Yields(source, g.Item))
            {
                item = g.Item;
                break;
            }
        }

        _target = source;
        _ordered = source;
        _hit = col;
        CurrentItem = item;
        _home = false;
        Travelling = false;
        Spots.Remember(source.transform.position);
        ResetTries();
        _nextSearch = Time.time + SearchSeconds;
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " ordered to break " + source.name + " (" +
                    TrollFarming.KindOf(source) + ", " + item + ")");
        return _target == source;
    }

    /// The facts the farming gate decides on (D-170), read in one place for Step and for PeekGate. Expected outcome: the input as Step has always built it.
    private FarmGateInput GateInput(bool alive, bool follow, bool fighting, TrollRecovery rec, bool hasWeapon)
    {
        return new FarmGateInput
        {
            Alive = alive,
            IsOwner = _ctx.IsOwner,
            FollowOrder = follow,
            Unloading = _home && IsFull,
            Fighting = fighting,
            RecoveryBusy = rec != null && rec.Busy && !_trip.UnloadWalkExpired && !_trip.InUnloadRange,
            FullCheckDue = Time.time >= _nextFullCheck && !IsFull,
            Home = _home,
            HasWeapon = hasWeapon
        };
    }

    /// 2026-11-09 (analysis of instruction-latency: "home flag set" is not "the troll is allowed to walk home"): what the farming gate would decide for this troll now, and the facts behind it,
    /// WITHOUT driving anything (read-only, for the stt.gateGoesHome test hook). A fight target, a follow order or a running recovery manoeuvre come before GoHome in FarmGateRules.Decide, so a
    /// home order given then is accepted and the flag is set, but the troll does not walk - this tells a test which of the two failed. Expected outcome: "GoHome (...)" when the troll walks home.
    internal string PeekGate()
    {
        MonsterAI ai = GetComponent<MonsterAI>();
        Humanoid me = _ctx.Humanoid;
        bool alive = me != null && !me.IsDead() && ai != null;
        Character target = alive ? ai.GetTargetCreature() : null;
        FarmGateInput input = GateInput(alive, alive && ai.GetFollowTarget() != null, target != null, GetComponent<TrollRecovery>(), true);
        FarmAction action = FarmGateRules.Decide(input);
        if (action == FarmAction.CheckFull)
        {
            input = input.Without(action); // a full check that finds the backpack not full changes nothing else (Step does the same)
            action = FarmGateRules.Decide(input);
        }

        return action + " (" + input + ", target " + (target != null ? target.name : "none") + ")";
    }

    /// One AI step. Expected outcome: returns whether farming drove the troll this frame (true = the game's AI is skipped: no idling, no feeding animation, D-048);
    /// false = fighting / done -> the game's AI runs.
    internal bool Step(MonsterAI ai, float dt)
    {
        // T-217 R1: never farms, walks or recovers under a rider (false = farming did not drive the troll)
        if (RiddenGate.Refuses(_ctx.Humanoid, "TrollFarmer.Step"))
        {
            _lastStep = Time.time; // the Part3 watchdog: a ridden troll is not a stalled farmer
            return false;
        }

        if (_stallLogged)
        {
            _stallLogged = false;
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " farming step RESUMED after " + (Time.time - _lastStep).ToString("F1") + " s without one - target " + (_target != null ? _target.name : "none") +
                        ", " + TrollOwnerDiag.Describe(GetComponent<ZNetView>() != null && GetComponent<ZNetView>().IsValid() ? GetComponent<ZNetView>().GetZDO() : null));
        }

        _lastStep = Time.time; // the Part3 watchdog
        _ai = ai;
        _dt = dt;
        _me = _ctx.Humanoid;
        TrollRecovery rec = GetComponent<TrollRecovery>();
        bool alive = _me != null && !_me.IsDead();
        bool home = _home,
            follow = alive && ai.GetFollowTarget() != null,
            fighting = alive && ai.GetTargetCreature() != null;
        // the weapon is read only when the gate can get that far (as before D-170: not while dead, following, fighting or going home)
        _weapon = alive && !home && !follow && !fighting ? _tool.For(_me, _target) : null;
        FarmGateInput input = GateInput(alive, follow, fighting, rec, _weapon != null);
        // D-170: decide, then perform; RecoveryDrives / CheckFull that did not drive fall through to the next priority (as the old code did)
        for (int pass = 0; pass < 3; pass++)
        {
            OperationResponse<FarmAction> decision = _pipeline.Evaluate(input);
            if (!decision.IsSuccess)
            {
                if (ModLog.IsDue("TrollFarmer.step." + GetInstanceID(), 5d))
                {
                    ModLog.Decision("TrollFarmer.step", "game AI runs",
                                    TrollTaming.Tag(this) + ": farming " + decision.Status + ", " + decision.Reason);
                    ModLog.Every("TrollFarmer.step." + GetInstanceID(), 5d, "step refused: " + decision.Status);
                }
                return decision.IsSuccess;
            }

            OperationResponse<bool> done = _executor.Execute(decision.Value);
            if (done.Status == ResponseStatus.Rejected)
            {
                return done.IsSuccess;
            }

            if (done.Value || !FarmGateRules.MayFallThrough(decision.Value))
            {
                return done.Value;
            }

            input = input.Without(decision.Value); // Full() that found the backpack not full changed nothing else
        }

        return _home;
    }

    // ---- the gate's actions (D-170: bound once in Awake) ----

    private bool OnStop()
    {
        Destroy(this);
        return _me != null && !_me.IsDead();
    }

    private bool OnStopFollow()
    {
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " got a follow order - stops farming");
        Destroy(this);
        return _ai.GetFollowTarget() == null;
    }

    private bool OnYieldToFight()
    {
        return _ai.GetTargetCreature() == null;
    } // fighting (D-042 defend): the game's AI drives

    private bool OnRecovery()
    {
        TrollRecovery rec = GetComponent<TrollRecovery>();
        return rec != null && rec.Continue(_dt);
    } // D-167: a running rage / detour of the shared recovery drives first

    private bool OnGoHome()
    {
        return _trip.GoHome(_ai, _dt);
    }
}
