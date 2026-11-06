using System.Globalization;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>Item 5: hooks for the ledge walk over a cliff. Every hook calls the mod's real code (TrollFarmer.StartTravel -> TrollRecovery) on a real troll and the real terrain.</summary>
internal static class CliffTestHooks
{
    private static Vector3 _bottom, _top, _goal;

    /// Looks for a cliff in the loaded terrain within `radius` m of the player: two points 6 - 12 m apart whose ground differs by 4 - 15 m, both on dry flat ground (the goal side too).
    /// Expected outcome: "bottom x,z | top x,z | height m" or "none".
    [TestHook("stt.findCliff")]
    private static string FindCliff(float radius)
    {
        ZoneSystem zs = ZoneSystem.instance;
        Player p = Player.m_localPlayer;
        if (zs == null || p == null)
        {
            return "none (no world)";
        }

        Vector3 c = p.transform.position;
        for (float x = -radius; x <= radius; x += 4f)
        for (float z = -radius; z <= radius; z += 4f)
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
                for (float m = 6f; m <= 12f; m += 3f)
                {
                    Vector3 b = a + dir * m;
                    if (!zs.IsZoneLoaded(ZoneSystem.GetZone(b)))
                    {
                        continue;
                    }

                    float hb = zs.GetGroundHeight(b), rise = hb - ha;
                    if (Mathf.Abs(rise) < 4f || Mathf.Abs(rise) > 15f || hb < zs.m_waterLevel + 1f || Mathf.Abs(zs.GetGroundHeight(b + dir * 2f) - hb) > 1.5f
                        || Mathf.Abs(zs.GetGroundHeight(a - dir * 2f) - ha) > 1.5f)
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

    /// The troll walks to the other side of the cliff the way a farming troll walks to a far place (TrollFarmer.StartTravel, which uses the shared recovery). Expected outcome: true when the walk started.
    [TestHook("stt.cliffTravel")]
    private static bool CliffTravel(GameObject troll)
    {
        TrollFarmer f = troll != null ? troll.GetComponent<TrollFarmer>() : null;
        if (f == null)
        {
            return false;
        }

        f.StartTravel(_goal);
        return true;
    }

    /// The troll stands within 3 m (flat) of the other side of the cliff. Expected outcome: true on arrival.
    [TestHook("stt.cliffArrived")]
    private static bool CliffArrived(GameObject troll)
    {
        if (troll == null)
        {
            return false;
        }

        Vector3 d = troll.transform.position - _goal;
        d.y = 0f;
        return d.magnitude <= 3f;
    }
}
