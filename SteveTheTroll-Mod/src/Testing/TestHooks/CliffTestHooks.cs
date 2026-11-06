using System.Globalization;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>Item 5: hooks for the ledge walk over a cliff. Every hook calls the mod's real code (TrollFarmer.StartTravel -> TrollRecovery) on a real troll and the real terrain.</summary>
internal static class CliffTestHooks
{
    private static Vector3 _bottom, _top, _goal;

    /// The hub of the player at the far side of the cliff (a ZDO, like stt.addHub): the full troll walks home to it, which is a walk of the shared recovery with a hub goal.
    /// Expected outcome: how many hubs are known now.
    [TestHook("stt.addHubAtCliff")]
    private static int AddHubAtCliff()
    {
        Vector3 d = _goal - Player.m_localPlayer.transform.position;
        return TestHooks.AddHub(1, d.x, d.z, "");
    }

    /// Looks for a cliff in the loaded terrain within `radius` m of the player: two points 6 - 12 m apart whose ground differs by 4 - 15 m, both on dry flat ground (the goal side too).
    /// Expected outcome: "bottom x,z | top x,z | height m" or "none".
    [TestHook("stt.findCliff")]
    private static string FindCliff(float radius)
    {
        return Search(radius, 4f, 15f, 6f, 12f, true, 0f, 4f);
    }

    /// Like stt.findCliff for a KIND of cliff: "small" (2.5 - 4 m step), "medium" (4 - 15 m), "high" (9 - 15 m, steep), "mountain" (15 - 40 m rise over 15 - 45 m, a slope of at least 0.6, no flat ends).
    /// Expected outcome: "bottom x,z | top x,z | height m" or "none ..." (the loaded terrain has no such cliff).
    [TestHook("stt.findCliffKind")]
    private static string FindCliffKind(float radius, string kind)
    {
        switch (kind)
        {
            case "small": return Search(radius, 2.5f, 4f, 3f, 6f, true, 0f, 4f);
            case "high": return Search(radius, 9f, 15f, 6f, 12f, true, 0f, 4f);
            case "mountain": return Search(radius, 15f, 40f, 15f, 45f, false, 0.6f, 8f);
            default: return Search(radius, 4f, 15f, 6f, 12f, true, 0f, 4f);
        }
    }

    /// The troll followed by stt.followTroll walks its next walk over the LEDGE WALK at once (TrollRecovery.TestForceLedge), so the walk itself is tested on any cliff. Expected outcome: true when it has a recovery to force.
    [TestHook("stt.forceLedge")]
    private static bool ForceLedge()
    {
        ZDO z = TestHooks.FollowedZdo();
        ZNetView view = z != null && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(z) : null;
        TrollRecovery rec = view != null ? view.GetComponent<TrollRecovery>() : null;
        if (rec == null)
        {
            return false;
        }

        rec.TestForceLedge();
        return true;
    }

    /// Have at least `min` ledge walks arrived at their goal this session? Expected outcome: true once they have.
    [TestHook("stt.ledgeArrived")]
    private static bool LedgeArrivedAtLeast(int min)
    {
        return TrollRecovery.LedgeArrived >= min;
    }

    /// The ledge walk counters as text (diagnosis), e.g. "started 1, arrived 1, stalled 0, timed out 0". Expected outcome: the line.
    [TestHook("stt.ledgeStats")]
    private static string LedgeStatsText()
    {
        return TrollRecovery.LedgeStats();
    }

    /// Zeroes the ledge walk counters (a test starts with no walks counted). Expected outcome: true.
    [TestHook("stt.resetLedgeStats")]
    private static bool ResetLedgeStats()
    {
        TrollRecovery.LedgeStarted = TrollRecovery.LedgeArrived = TrollRecovery.LedgeStalledEnds = TrollRecovery.LedgeTimeouts = 0;
        return true;
    }

    // No built piece, rock or trunk at chest height along the line (the ledge walk refuses those steps, so such a cliff cannot prove anything).
    private static bool ChestClear(ZoneSystem zs, Vector3 a, Vector3 b)
    {
        int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");
        float len = Vector3.Distance(a, b);
        for (float t = 0f; t <= len; t += 1.2f)
        {
            Vector3 q = Vector3.Lerp(a, b, len > 0f ? t / len : 0f);
            if (Physics.CheckSphere(new Vector3(q.x, zs.GetGroundHeight(q) + 1.2f, q.z), 1f, mask))
            {
                return false;
            }
        }

        return true;
    }

    private static string Search(float radius, float minRise, float maxRise, float minRun, float maxRun, bool flatEnds, float minSlope, float gridStep)
    {
        ZoneSystem zs = ZoneSystem.instance;
        Player p = Player.m_localPlayer;
        if (zs == null || p == null)
        {
            return "none (no world)";
        }

        Vector3 c = p.transform.position;
        for (float x = -radius; x <= radius; x += gridStep)
        for (float z = -radius; z <= radius; z += gridStep)
        {
            Vector3 a = c + new Vector3(x, 0f, z);
            if (!zs.IsZoneLoaded(ZoneSystem.GetZone(a)))
            {
                continue;
            }

            float ha = zs.GetGroundHeight(a);
            if (ha < zs.m_waterLevel + 1f)
            {
                continue;
            }

            for (int k = 0; k < 8; k++)
            {
                float ang = k * 45f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                for (float m = minRun; m <= maxRun; m += Mathf.Max(3f, (maxRun - minRun) / 3f))
                {
                    Vector3 b = a + dir * m;
                    if (!zs.IsZoneLoaded(ZoneSystem.GetZone(b)))
                    {
                        continue;
                    }

                    float hb = zs.GetGroundHeight(b), rise = hb - ha;
                    if (Mathf.Abs(rise) < minRise || Mathf.Abs(rise) > maxRise || Mathf.Abs(rise) / m < minSlope || hb < zs.m_waterLevel + 1f
                        || (flatEnds && (Mathf.Abs(zs.GetGroundHeight(b + dir * 2f) - hb) > 1.5f || Mathf.Abs(zs.GetGroundHeight(a - dir * 2f) - ha) > 1.5f)))
                    {
                        continue;
                    }

                    if (!ChestClear(zs, a, b))
                    {
                        continue;
                    }

                    bool upHill = rise > 0f;
                    _bottom = new Vector3(upHill ? a.x : b.x, upHill ? ha : hb, upHill ? a.z : b.z);
                    _top = new Vector3(upHill ? b.x : a.x, upHill ? hb : ha, upHill ? b.z : a.z);
                    return _bottom.x.ToString("F0", CultureInfo.InvariantCulture) + "," + _bottom.z.ToString("F0", CultureInfo.InvariantCulture) + " | " +
                           _top.x.ToString("F0", CultureInfo.InvariantCulture) + "," + _top.z.ToString("F0", CultureInfo.InvariantCulture) + " | " + Mathf.Abs(rise).ToString("F1", CultureInfo.InvariantCulture) + " m";
                }
            }
        }

        return "none within " + radius.ToString("F0") + " m";
    }

    /// The player teleports to a spot 8 m before the cliff stt.findCliff found (the game's own TeleportTo: the zones around the cliff load, so a troll spawned there stays).
    /// Expected outcome: the place as text.
    [TestHook("stt.teleportToCliff")]
    private static string TeleportToCliff(bool up)
    {
        Player p = Player.m_localPlayer;
        Vector3 from = up ? _bottom : _top, to = up ? _top : _bottom;
        Vector3 dir = to - from;
        dir.y = 0f;
        Vector3 at = from - dir.normalized * 8f;
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 1f;
        p.TeleportTo(at, p.transform.rotation, true);
        return at.ToString("F0");
    }

    /// A tamed troll at the foot (`up` = true: it must climb) or on the top (it must go down) of the cliff stt.findCliff found. Expected outcome: the troll.
    [TestHook("stt.spawnAtCliff")]
    private static GameObject SpawnAtCliff(bool up)
    {
        Vector3 from = up ? _bottom : _top;
        _goal = up ? _top : _bottom;
        Vector3 dir = (_goal - from);
        dir.y = 0f;
        Vector3 at = from - dir.normalized * 1.5f;
        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.5f;
        return TestHooks.SpawnTrollAt(at);
    }

    /// Flat metres from the followed troll (stt.followTroll, by ZDO) to the far side of the cliff. Expected outcome: the distance (-1 = no followed troll).
    [TestHook("stt.cliffDistance")]
    private static int CliffDistance()
    {
        ZDO z = TestHooks.FollowedZdo();
        if (z == null)
        {
            return -1;
        }

        Vector3 d = z.GetPosition() - _goal;
        d.y = 0f;
        return Mathf.RoundToInt(d.magnitude);
    }
}
