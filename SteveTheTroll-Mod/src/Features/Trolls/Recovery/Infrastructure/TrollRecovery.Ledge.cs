using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Recovery.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Recovery.Infrastructure;

/// <summary>
///     Item 5 (user 2026-11-06): the LEDGE WALK of TrollRecovery - the troll walks the straight line to its goal itself with its feet on the terrain under it, so it goes down or up
///     a cliff that the game's navmesh has no way over. Part of the one shared recovery, so every walk that uses it (a tree, a rock, a far place, the way home) gets it. The rules
///     (what is a cliff, when it may start, when it ends) are LedgeRules; this part reads the terrain and moves the troll.
/// </summary>
internal sealed partial class TrollRecovery
{
    private static readonly RaycastHit[] LedgeHits = new RaycastHit[8];
    private string _lastBlocker = "";
    private Vector3 _ledgeStart, _ledgeLast;
    private float _ledgeBegan, _ledgeStalled;
    private bool _forcedLedge, _forcedStarted, _ledgeActive, _ledgeFar;
    private Vector3 _ledgeGoal;

    /// 2026-11-06 (user: full trolls close to him did not walk home down a steep mountain path): where THIS ledge walk goes - the goal itself, or for a goal beyond LedgeRules.MaxFlatMetres a
    /// point LedgeRules.FarWalkMetres ahead on the line (the game's path takes over from there).
    private Vector3 LedgeGoal => _ledgeFar ? _ledgeGoal : _goal;

    /// Counters of every ledge walk of this session (the cliff tests read them): started, and how each one ended.
    internal static int LedgeStarted, LedgeArrived, LedgeStalledEnds, LedgeTimeouts;

    /// The counters as one line. Expected outcome: "started n, arrived n, stalled n, timed out n".
    internal static string LedgeStats()
    {
        return "started " + LedgeStarted + ", arrived " + LedgeArrived + ", stalled " + LedgeStalledEnds + ", timed out " + LedgeTimeouts;
    }

    /// Test hook support: the next walk of this troll goes over the ledge walk at once (no waiting for the troll to be stuck), so the walk itself can be tested on any cliff.
    internal void TestForceLedge()
    {
        _forcedLedge = true;
        _forcedStarted = false;
    }

    // The forced ledge walk of TestForceLedge: it replaces the whole ladder until it ends. Expected outcome: true = it drove the troll this frame.
    private bool ForcedLedge(float dt, Vector3 goal, GameObject goalObject)
    {
        _goal = goal;
        _goalObject = goalObject;
        _lastTick = Time.time;
        _d = Flat(goal);
        ITrollMovement mv = _ctx.Capabilities.Movement;
        if (mv == null)
        {
            return false;
        }

        if (!_forcedStarted)
        {
            _forcedStarted = true;
            OnStartLedge();
        }

        if (LedgeDone(dt))
        {
            OnEndLedge();
            _forcedLedge = false;
            _forcedStarted = false;
            return false;
        }

        return LedgeStep(dt, mv);
    }

    /// What the terrain ahead of a stuck troll looks like: `cliff` = it rises / falls by LedgeRules.CliffRise within CliffProbeMetres on the line to the goal, `ok` = a ledge walk
    /// to the goal may start (zones loaded, goal within the metres and height limits). Expected outcome: both false when the ground is unknown.
    private void LedgeFacts(out bool cliff, out bool ok)
    {
        cliff = false;
        ok = false;
        ZoneSystem zs = ZoneSystem.instance;
        Vector3 p = transform.position;
        Vector3 to = _goal - p;
        to.y = 0f;
        float flat = to.magnitude;
        if (zs == null || flat < 0.5f || !zs.IsZoneLoaded(ZoneSystem.GetZone(p)))
        {
            string key = "TrollRecovery.ledgefacts." + TrollTaming.Tag(this);
            if (ModLog.IsDue(key, 2d))
            {
                ModLog.Every(key, 2d, TrollTaming.Tag(this) + " ledge facts: not judged - " + (zs == null ? "no zone system" : "zone here loaded " + zs.IsZoneLoaded(ZoneSystem.GetZone(p)) +
                                                                                                                   ", zone of the goal loaded " + zs.IsZoneLoaded(ZoneSystem.GetZone(_goal))) + ", " + flat.ToString("F1") + " m flat to the goal");
            }

            return;
        }

        Vector3 dir = to / flat;
        float h0 = zs.GetGroundHeight(p), worst = 0f, steepestMetre = 0f, prevH = h0;
        for (int i = 1; i <= (int)LedgeRules.CliffProbeMetres; i++)
        {
            Vector3 q = p + dir * i;
            if (!zs.IsZoneLoaded(ZoneSystem.GetZone(q)))
            {
                break;
            }

            float hq = zs.GetGroundHeight(q);
            if (Mathf.Abs(hq - prevH) > Mathf.Abs(steepestMetre))
            {
                steepestMetre = hq - prevH;
            }

            prevH = hq;
            float change = hq - h0;
            if (Mathf.Abs(change) > Mathf.Abs(worst))
            {
                worst = change;
            }
        }

        cliff = LedgeRules.IsCliff(worst);
        // 2026-11-06 (lab run 171219, cliff-walk-mountain-down: "zone of the goal loaded False", the ledge was never judged and the troll stood at the cliff for 100 s while the hub was 72 m away in an unloaded zone):
        // the troll only needs ITS OWN zone to see the cliff; the goal is a known position (a hub / chest / source ZDO), so its own height is used when the ground there is not loaded
        bool goalLoaded = zs.IsZoneLoaded(ZoneSystem.GetZone(_goal));
        float toGoalHeight = (goalLoaded ? zs.GetGroundHeight(_goal) : _goal.y) - h0;
        ok = LedgeRules.IsFar(flat) || LedgeRules.Possible(flat, toGoalHeight);
        string factsKey = "TrollRecovery.ledgefacts." + TrollTaming.Tag(this);
        if (ModLog.IsDue(factsKey, 2d))
        {
            // 2026-11-06 (user: "check the log, how steep was the cliff"): the slope in degrees, the steepest single metre, and WHICH rule allowed / refused the walk
            string why = !cliff ? "NOT a cliff: the ground changes less than " + LedgeRules.CliffRise.ToString("F1") + " m within " + (int)LedgeRules.CliffProbeMetres + " m"
                : ok ? (LedgeRules.IsFar(flat) ? "cliff, goal beyond " + LedgeRules.MaxFlatMetres.ToString("F0") + " m so the walk goes " + LedgeRules.FarWalkMetres.ToString("F0") + " m ahead only" : "cliff, goal within reach")
                : "cliff but the goal is out of reach (" + flat.ToString("F0") + " m flat, " + toGoalHeight.ToString("F0") + " m height difference, limits " + LedgeRules.MaxFlatMetres.ToString("F0") + " m / " + LedgeRules.MaxHeightDifference.ToString("F0") + " m)";
            ModLog.Every(factsKey, 2d, TrollTaming.Tag(this) + " ledge facts: SLOPE " + (Mathf.Atan2(Mathf.Abs(worst), LedgeRules.CliffProbeMetres) * Mathf.Rad2Deg).ToString("F0") + " deg over " + (int)LedgeRules.CliffProbeMetres +
                                       " m, steepest metre " + steepestMetre.ToString("F1") + " m (" + (Mathf.Atan2(Mathf.Abs(steepestMetre), 1f) * Mathf.Rad2Deg).ToString("F0") + " deg), verdict: " + why + "; " +
                                       "the ground changes " + worst.ToString("F1") + " m within " + (int)LedgeRules.CliffProbeMetres + " m ahead (cliff from " +
                                       LedgeRules.CliffRise.ToString("F1") + " m) = cliff " + cliff + ", goal " + flat.ToString("F1") + " m flat and " + toGoalHeight.ToString("F1") +
                                       " m higher (goal height from " + (goalLoaded ? "the loaded ground" : "its known position, zone not loaded") + ", ledge walk possible within " + LedgeRules.MaxFlatMetres.ToString("F0") + " m / " + LedgeRules.MaxHeightDifference.ToString("F0") + " m) = " + ok);
        }
    }

    private bool LedgeDone(float dt)
    {
        Vector3 pos = transform.position;
        float moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_ledgeLast.x, 0f, _ledgeLast.z));
        _ledgeStalled = moved < LedgeRules.StallMetres * dt ? _ledgeStalled + dt : 0f;
        _ledgeLast = pos;
        return LedgeRules.Done(Flat(LedgeGoal), Time.time - _ledgeBegan, _ledgeStalled);
    }

    private void OnStartLedge()
    {
        _ledgeStart = transform.position;
        _ledgeLast = _ledgeStart;
        _ledgeBegan = Time.time;
        _ledgeStalled = 0f;
        _ledgeActive = true;
        Vector3 toGoal = _goal - _ledgeStart;
        toGoal.y = 0f;
        _ledgeFar = LedgeRules.IsFar(toGoal.magnitude);
        _ledgeGoal = _ledgeFar ? _ledgeStart + toGoal.normalized * LedgeRules.FarWalkMetres : _goal;
        LedgeStarted++;
        ModLog.Decision("TrollRecovery.ledge", "start",
                        TrollTaming.Tag(this) + " at " + _ledgeStart.ToString("F0") + ", goal " + _goal.ToString("F0") + ", " + _d.ToString("F1") + " m to go, stuck " +
                        _meter.StuckSeconds.ToString("F1") + " s, detours done " + M.DetoursDone);
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " cannot reach its goal " + _d.ToString("F1") + " m away on the game's path - walks it over the ground itself (ledge walk, down / up a cliff)");
    }

    /// T-214 (run 143626: the hub was within 25 m, the troll was "home" and unloaded, so the walk was never ticked again and its end was never counted): the caller says the troll REACHED
    /// its destination while a ledge walk was running - that walk ends as "arrived". Expected outcome: true when a running ledge walk was ended.
    private bool EndLedgeReached()
    {
        if (!_ledgeActive)
        {
            return false;
        }

        _ledgeActive = false;
        _forcedLedge = false;
        _forcedStarted = false;
        float elapsed = Time.time - _ledgeBegan;
        LedgeArrived++;
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " ledge walk over after " + Flat2(_ledgeStart).ToString("F1") + " m and " + elapsed.ToString("F1") + " s - the walk reached its destination (" + Flat(_goal).ToString("F1") +
                    " m from the goal point), height change " + (transform.position.y - _ledgeStart.y).ToString("F1") + " m - RESULT arrived (" + LedgeStats() + ")");
        return true;
    }

    private void OnEndLedge()
    {
        _ledgeActive = false;
        float left = Flat(LedgeGoal), elapsed = Time.time - _ledgeBegan;
        string outcome = LedgeRules.Outcome(left, elapsed, _ledgeStalled);
        if (outcome == "arrived")
        {
            LedgeArrived++;
        }
        else if (outcome == "timeout")
        {
            LedgeTimeouts++;
        }
        else
        {
            LedgeStalledEnds++;
        }

        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " ledge walk over after " + Flat2(_ledgeStart).ToString("F1") + " m and " + elapsed.ToString("F1") +
                    " s - " + left.ToString("F1") + " m from its goal, height change " + (transform.position.y - _ledgeStart.y).ToString("F1") + " m - RESULT " + outcome + " (" + LedgeStats() + ")");
        _meter.Reset(_d);
    }

    /// One frame of the ledge walk: a short step along the line to the goal, the feet on the terrain there. Expected outcome: true = it drove the troll (always while the walk runs).
    private bool LedgeStep(float dt, ITrollMovement mv)
    {
        ZoneSystem zs = ZoneSystem.instance;
        Vector3 pos = transform.position;
        Vector3 flat = LedgeGoal - pos;
        flat.y = 0f;
        float d = flat.magnitude;
        float step = LedgeRules.StepMetres(dt, d);
        if (zs == null || step <= 0f || d < 0.01f)
        {
            return true;
        }

        Vector3 dir = flat / d;
        Vector3 next = pos + dir * step;
        mv.LookAt(next);
        bool zoneLoaded = zs.IsZoneLoaded(ZoneSystem.GetZone(next));
        if (!zoneLoaded || Blocked(pos, next))
        {
            string key = "TrollRecovery.ledgestep." + TrollTaming.Tag(this);
            if (ModLog.IsDue(key, 1d))
            {
                ModLog.Every(key, 1d, TrollTaming.Tag(this) + " ledge step refused at " + pos.ToString("F1") + " towards " + next.ToString("F1") + ": " +
                                      (zoneLoaded ? "something solid at chest height is in the way (" + _lastBlocker + ")" : "the zone ahead is not loaded"));
            }

            return true; // no move this frame: the stall counter of LedgeDone ends the walk
        }

        var target = new Vector3(next.x, zs.GetGroundHeight(next) + 0.05f, next.z);
        transform.position = target;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = target;
            body.linearVelocity = Vector3.zero;
        }

        return true;
    }

    // Something solid at chest height between the two points (not the troll itself, not the thing it walks to): the ledge walk does not walk through trunks, rocks and walls.
    private bool Blocked(Vector3 from, Vector3 to)
    {
        if (_blockMask == 0)
        {
            _blockMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");
        }

        Vector3 o = from + Vector3.up * ChestHeight, delta = to - from;
        float len = delta.magnitude;
        int n = Physics.RaycastNonAlloc(o, delta / Mathf.Max(len, 0.001f), LedgeHits, len + ProbeRadius, _blockMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = LedgeHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform) || (_goalObject != null && c.transform.IsChildOf(_goalObject.transform)))
            {
                continue;
            }

            _lastBlocker = c.name + " (" + LayerMask.LayerToName(c.gameObject.layer) + ") " + LedgeHits[i].distance.ToString("F1") + " m ahead";
            return true;
        }

        return false;
    }
}
