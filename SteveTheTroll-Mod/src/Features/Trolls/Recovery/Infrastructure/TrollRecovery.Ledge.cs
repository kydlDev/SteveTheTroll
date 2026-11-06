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
    private Vector3 _ledgeStart, _ledgeLast;
    private float _ledgeBegan, _ledgeStalled;

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
        if (zs == null || flat < 0.5f || !zs.IsZoneLoaded(ZoneSystem.GetZone(p)) || !zs.IsZoneLoaded(ZoneSystem.GetZone(_goal)))
        {
            return;
        }

        Vector3 dir = to / flat;
        float h0 = zs.GetGroundHeight(p), worst = 0f;
        for (int i = 1; i <= (int)LedgeRules.CliffProbeMetres; i++)
        {
            Vector3 q = p + dir * i;
            if (!zs.IsZoneLoaded(ZoneSystem.GetZone(q)))
            {
                break;
            }

            float change = zs.GetGroundHeight(q) - h0;
            if (Mathf.Abs(change) > Mathf.Abs(worst))
            {
                worst = change;
            }
        }

        cliff = LedgeRules.IsCliff(worst);
        ok = LedgeRules.Possible(flat, zs.GetGroundHeight(_goal) - h0);
    }

    private bool LedgeDone(float dt)
    {
        Vector3 pos = transform.position;
        float moved = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_ledgeLast.x, 0f, _ledgeLast.z));
        _ledgeStalled = moved < LedgeRules.StallMetres * dt ? _ledgeStalled + dt : 0f;
        _ledgeLast = pos;
        return LedgeRules.Done(Flat(_goal), Time.time - _ledgeBegan, _ledgeStalled);
    }

    private void OnStartLedge()
    {
        _ledgeStart = transform.position;
        _ledgeLast = _ledgeStart;
        _ledgeBegan = Time.time;
        _ledgeStalled = 0f;
        ModLog.Decision("TrollRecovery.ledge", "start",
                        TrollTaming.Tag(this) + " at " + _ledgeStart.ToString("F0") + ", goal " + _goal.ToString("F0") + ", " + _d.ToString("F1") + " m to go, stuck " +
                        _meter.StuckSeconds.ToString("F1") + " s, detours done " + M.DetoursDone);
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " cannot reach its goal " + _d.ToString("F1") + " m away on the game's path - walks it over the ground itself (ledge walk, down / up a cliff)");
    }

    private void OnEndLedge()
    {
        ModLog.Info("TrollRecovery: " + TrollTaming.Tag(this) + " ledge walk over after " + Flat2(_ledgeStart).ToString("F1") + " m and " + (Time.time - _ledgeBegan).ToString("F1") +
                    " s - " + _d.ToString("F1") + " m from its goal, height change " + (transform.position.y - _ledgeStart.y).ToString("F1") + " m");
        _meter.Reset(_d);
    }

    /// One frame of the ledge walk: a short step along the line to the goal, the feet on the terrain there. Expected outcome: true = it drove the troll (always while the walk runs).
    private bool LedgeStep(float dt, ITrollMovement mv)
    {
        ZoneSystem zs = ZoneSystem.instance;
        Vector3 pos = transform.position;
        Vector3 flat = _goal - pos;
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
        if (!zs.IsZoneLoaded(ZoneSystem.GetZone(next)) || Blocked(pos, next))
        {
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

            return true;
        }

        return false;
    }
}
