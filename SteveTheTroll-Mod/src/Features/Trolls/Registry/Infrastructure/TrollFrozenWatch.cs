using System.Collections.Generic;
using KydlCore.Common;
using KydlCore.Features.Diagnostics.Contracts;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Features.Trolls.World.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure;

/// <summary>
///     NEXT_STEPS item 4 (user 09:45 / 09:48): frozen-troll protection, ALWAYS on (not debug-gated). An own troll that should be travelling (home, depot, its
///     spot) and has not moved for FrozenRules.ReclaimSeconds counts as frozen. Checked here, outside the AI step (a frozen troll's AI step does not run), from the
///     ZDO position, so it also works for a troll whose game object does not exist. The self-heal ladder, mildest first: keep its zone loaded and re-claim its ZDO,
///     rebuild its game object, move it along its route. The player is told once per freeze and the map pin is highlighted (<see cref="IsFrozen" />).
///     "Should be travelling" is written to the troll's ZDO (stt_travel) by the client that owns it, so any client can judge a far troll.
/// </summary>
internal static class TrollFrozenWatch
{
    internal const string TravelKey = "stt_travel";
    private const float TickSeconds = 2f;

    private static readonly Dictionary<ZDOID, Watch> Watches = new();
    private static readonly HashSet<string> Frozen = new();
    private static readonly List<ZDOID> Ids = new();
    private static readonly HashSet<ZDOID> Seen = new();
    private static float _next;

    /// Test override (stt.forceTravelling): every own loaded troll counts as "should be travelling". Reset by stt.reset.
    internal static bool TestForceTravelling;

    /// How many ownerless loaded own trolls this session's watch claimed (the farm-friend-detour tests read it).
    internal static int OwnerClaims;

    /// Test hook support: is the troll with this ZDO frozen now? Expected outcome: true from the first rung on.
    internal static bool IsFrozenZdo(ZDO z)
    {
        return z != null && IsFrozen(z.GetString(TrollRegistry.IdKey));
    }

    private sealed class Watch
    {
        internal Vector3 Pos;
        internal float StillSince, NextCatchUp;
        internal FrozenStage Stage;
        internal string SttId = "";
    }

    /// Is this troll (our stt_id) frozen now? Expected outcome: true from the first rung on (the map pin shows it).
    /// Set by the riding feature: true for a troll that must not be watched (it is ridden). Expected outcome: null = nothing excluded.
    internal static System.Func<Character, bool> Excluded;

    internal static bool IsFrozen(string sttId)
    {
        return sttId != null && Frozen.Contains(sttId);
    }

    /// Plugin / TrollRegistry.Tick. Expected outcome: how many trolls are frozen now (-1 = nothing done this frame).
    internal static int Tick()
    {
        if (Time.time < _next)
        {
            return -1;
        }

        _next = Time.time + TickSeconds;
        Player p = Player.m_localPlayer;
        ZDOMan zm = ZDOMan.instance;
        if (p == null || zm == null || ZNetScene.instance == null || !ModSettings.On)
        {
            Watches.Clear();
            Frozen.Clear();
            return 0;
        }

        long me = p.GetPlayerID();
        Seen.Clear();
        Ids.Clear();
        foreach (Character c in Character.GetAllCharacters())
        {
            ZDO z = c != null && c.IsTamed() && TrollTaming.IsTroll(c) ? TrollTaming.Zdo(c) : null;
            if (z != null && Seen.Add(z.m_uid))
            {
                Ids.Add(z.m_uid);
            }
        }

        foreach (ZDOID id in TrollZones.TrackedTrolls)
        {
            if (Seen.Add(id))
            {
                Ids.Add(id);
            }
        }

        Frozen.Clear();
        foreach (ZDOID id in Ids)
        {
            Check(zm, id, me);
        }

        var drop = new List<ZDOID>();
        foreach (ZDOID id in Watches.Keys)
        {
            if (!Seen.Contains(id))
            {
                drop.Add(id);
            }
        }

        foreach (ZDOID id in drop)
        {
            Watches.Remove(id);
        }

        if (ModLog.IsDue("TrollFrozenWatch.status", 30d))
        {
            ModLog.Every("TrollFrozenWatch.status", 30d, "watching " + Watches.Count + " own troll(s), " + Frozen.Count + " frozen");
        }

        return Frozen.Count;
    }

    private static void Check(ZDOMan zm, ZDOID id, long me)
    {
        ZDO z = zm.GetZDO(id);
        if (z == null || z.GetFloat(ZDOVars.s_health, 1f) <= 0f || z.GetLong(TrollTaming.OwnerKey) != me)
        {
            Watch dropped;
            if (Watches.TryGetValue(id, out dropped))
            {
                ModLog.Info("TrollFrozenWatch: troll " + id + " [" + dropped.SttId + "] leaves the watch - " + (z == null ? "its ZDO no longer exists (last known at " + dropped.Pos.ToString("F0") + ", stage " + dropped.Stage + ")" :
                    z.GetFloat(ZDOVars.s_health, 1f) <= 0f ? "it is dead" : "it no longer belongs to this player"));
            }

            Watches.Remove(id);
            return;
        }

        ZNetView view = ZNetScene.instance.FindInstance(z);
        Character c = view != null ? view.GetComponent<Character>() : null;
        bool owner = z.IsOwner();
        if (c != null && !owner && FrozenRules.ShouldClaim(z.GetOwner()))
        {
            OwnerClaims++;
            ModLog.Info("TrollFrozenWatch: " + TrollTaming.Tag(c) + " is loaded but nobody owns it (" + TrollOwnerDiag.Describe(z) + ") - this client claims it");
            z.SetOwner(ZDOMan.GetSessionID());
            owner = z.IsOwner();
            // 2026-11-06 (full lab run 163541: the same line every 2 s for 20+ s, the claim never stuck - instruction-latency, backpack tests): is the claim itself refused, or does the game release it afterwards?
            ModLog.Info("TrollFrozenWatch: " + TrollTaming.Tag(c) + " claim result - owner now " + z.GetOwner() + ", this client owns it " + owner + ", persistent " + z.Persistent + ", ZDO sector " + z.GetSector() + ", player zone " + ZoneSystem.GetZone(Player.m_localPlayer.transform.position));
        }

        // T-213: a troll the player rides stands still on purpose while the AI is off - not frozen (it was rebuilt under the rider after 30 s and threw them off)
        if (c != null && Excluded != null && Excluded(c))
        {
            Watches.Remove(id);
            return;
        }

        bool travelling = z.GetInt(TravelKey, 0) == 1;
        if (c != null && owner)
        {
            travelling = IsTravelling(c);
            if (travelling != (z.GetInt(TravelKey, 0) == 1))
            {
                z.Set(TravelKey, travelling ? 1 : 0);
            }
        }

        Vector3 pos = c != null ? c.transform.position : z.GetPosition();
        Watch w;
        if (!Watches.TryGetValue(id, out w))
        {
            Watches[id] = new Watch { Pos = pos, StillSince = Time.time, SttId = z.GetString(TrollRegistry.IdKey) };
            return;
        }

        float flat = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(w.Pos.x, w.Pos.z));
        if (FrozenRules.Moved(flat) || !travelling)
        {
            if (w.Stage != FrozenStage.None)
            {
                ModLog.State("troll " + id, "frozen (" + w.Stage + ")", "moving", "moved " + flat.ToString("F1") + " m after " + (Time.time - w.StillSince).ToString("F0") + " s");
            }

            w.Pos = pos;
            w.StillSince = Time.time;
            w.Stage = FrozenStage.None;
            w.NextCatchUp = 0f;
            return;
        }

        FrozenStage stage = FrozenRules.StageFor(true, Time.time - w.StillSince);
        if (stage == FrozenStage.None)
        {
            return;
        }

        // 2026-11-07 (full run agent24-full4, frozen-troll-detected): the watch starts the moment the troll exists, a moment before the registry gives it its id - the id read then
        // was empty, so the freeze was detected (evidence dumped, player told) but never recorded and the map pin / hook never showed it. Read the id again when it was missing.
        if (string.IsNullOrEmpty(w.SttId))
        {
            w.SttId = z.GetString(TrollRegistry.IdKey);
        }

        if (!string.IsNullOrEmpty(w.SttId))
        {
            Frozen.Add(w.SttId);
        }

        if (stage > w.Stage)
        {
            Climb(z, c, view, w, stage, pos);
        }

        if (stage == FrozenStage.CatchUp && Time.time >= w.NextCatchUp)
        {
            w.NextCatchUp = Time.time + FrozenRules.CatchUpEvery;
            CatchUp(z, c, w, pos, me: z.GetLong(TrollTaming.OwnerKey));
        }
    }

    /// The troll should be on its way somewhere: walking back to a hub, going home with a full backpack or travelling to its spot. Expected outcome: true while it is.
    private static bool IsTravelling(Character c)
    {
        TrollFarmer f = c.GetComponent<TrollFarmer>();
        bool travelling = TestForceTravelling || c.GetComponent<TrollReturn>() != null || (f != null && (f.Travelling || f.GoingHome));
        TrollRecovery rec = c.GetComponent<TrollRecovery>();
        if (travelling && !TestForceTravelling && rec != null && rec.Recovering)
        {
            // 2026-11-09 (agent24-y5): blocked by a wall and climbing the recovery ladder (detours, ledge walk) is not a freeze; rebuilding the troll threw the ladder away every 30 s
            ModLog.Every("TrollFrozenWatch.recovering." + c.GetInstanceID(), 30d, "TrollFrozenWatch: " + TrollTaming.Tag(c) + " is working through a recovery cycle - not counted as frozen");
            travelling = false;
        }
        // T-214 B1: standing still because the player looks at it (TrollHold), fighting or staggering is not a freeze
        return FrozenRules.ExpectsMovement(travelling, TrollHold.IsHolding(c), c.InAttack(), c.IsStaggering());
    }

    private static void Climb(ZDO z, Character c, ZNetView view, Watch w, FrozenStage stage, Vector3 pos)
    {
        float still = Time.time - w.StillSince;
        string who = c != null ? TrollTaming.Tag(c) : "troll " + z.m_uid;
        ModLog.State("troll " + z.m_uid, w.Stage.ToString(), stage.ToString(), "not moved for " + still.ToString("F0") + " s at " + pos.ToString("F0") +
                                                                           ", loaded " + (c != null) + ", owner here " + z.IsOwner());
        w.Stage = stage;
        switch (stage)
        {
            case FrozenStage.Reclaim:
                ModLog.Info("TrollFrozenWatch: " + who + " has not moved for " + still.ToString("F0") + " s although it should be travelling - evidence dumped, player told");
                FlightRecorder.Dump("TrollFrozenWatch: " + who + " has not moved for " + still.ToString("F0") + " s although it should be travelling (at " + pos.ToString("F0") + ")");
                Tell(w, pos, still);
                if (!z.IsOwner())
                {
                    z.SetOwner(ZDOMan.GetSessionID());
                }

                TrollZones.KeepAround(pos);
                break;
            case FrozenStage.Rebuild:
                if (view != null && view.IsValid())
                {
                    // T-214 B2: ZNetScene.Destroy(go) DESTROYS the ZDO of an owned object (the troll and its backpack were gone in cliff-walk-high-down) - only the instance may go; the ZDO stays and the game builds the troll again
                    ZDO zdo = view.GetZDO();
                    view.ClaimOwnership();
                    bool dropped = DropInstanceKeepZdo(view);
                    ModLog.Warn("TrollFrozenWatch: " + who + (dropped ? " rebuilt from its ZDO (frozen for " : " could NOT be rebuilt, left as it is (frozen for ") + still.ToString("F0") + " s); its ZDO " + (zdo != null && ZDOMan.instance.GetZDO(zdo.m_uid) != null ? "is still there" : "IS GONE"));
                }
                else
                {
                    TrollZones.KeepAround(pos);
                }

                break;
            case FrozenStage.CatchUp:
                ModLog.Warn("TrollFrozenWatch: " + who + " still frozen after " + still.ToString("F0") + " s - it is moved along its route");
                break;
        }
    }

    /// Removes the troll's scene object but keeps its ZDO (position, inventory, owner, tame state): the view lets go of the ZDO, the scene forgets the instance, then only the GameObject is destroyed. Expected outcome: true when done; false (nothing changed) when the scene's instance table cannot be reached.
    private static bool DropInstanceKeepZdo(ZNetView view)
    {
        ZDO zdo = view.GetZDO();
        var instances = HarmonyLib.AccessTools.Field(typeof(ZNetScene), "m_instances")?.GetValue(ZNetScene.instance) as System.Collections.IDictionary;
        if (zdo == null || instances == null)
        {
            return false;
        }

        view.ResetZDO();
        instances.Remove(zdo);
        Object.Destroy(view.gameObject);
        return true;
    }

    private static void CatchUp(ZDO z, Character c, Watch w, Vector3 pos, long me)
    {
        float d;
        HubInfo hub = HubRegistry.Nearest(me, pos, out d);
        if (hub == null || d <= 0f)
        {
            return; // no route known: the lower rungs and the player's message have to do
        }

        Vector3 dir = hub.Pos - pos;
        dir.y = 0f;
        Vector3 to = pos + dir.normalized * FrozenRules.CatchUpStep(d);
        if (ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(ZoneSystem.GetZone(to)))
        {
            to.y = ZoneSystem.instance.GetGroundHeight(to) + 0.5f;
        }
        else
        {
            to.y = pos.y;
        }

        if (!z.IsOwner())
        {
            z.SetOwner(ZDOMan.GetSessionID());
        }

        z.SetPosition(to);
        if (c != null)
        {
            c.transform.position = to;
        }

        w.Pos = to; // our own move does not end the freeze: the ladder goes on until the troll moves by itself
        ModLog.Decision("TrollFrozenWatch.catchUp", "moved " + FrozenRules.CatchUpStep(d).ToString("F0") + " m toward " + hub.Name, "from " + pos.ToString("F0") + " to " + to.ToString("F0") +
                                                                                                                           ", " + d.ToString("F0") + " m from the hub");
    }

    private static void Tell(Watch w, Vector3 pos, float still)
    {
        TrollInfo info = TrollRegistry.Peek().Find(k => k.Id == w.SttId);
        string name = info != null ? info.Shown(k => Loc.T(k)) : "Troll";
        Notify.Show(Loc.T("stevethetroll_troll_stuck", name, pos.x.ToString("0"), pos.z.ToString("0"), still.ToString("0")), Notify.Kind.Farm);
    }
}
