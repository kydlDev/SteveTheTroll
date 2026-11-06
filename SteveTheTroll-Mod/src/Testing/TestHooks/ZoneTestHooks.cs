using KydlCore.Common;
using SteveTheTroll.Features.Trolls.World.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>NEXT_STEPS item 1: hooks for TrollZones at a small simulation distance (low graphics). Every hook calls the mod's real code; none decides an outcome itself.</summary>
internal static class ZoneTestHooks
{
    /// TrollZones sees the game's simulation area as `near` / `total` zones (a classic square band) - the way low graphics settings make it. -1 = the game's real values.
    /// Expected outcome: the near value now in force.
    [TestHook("stt.zonesSimulation")]
    private static int ZonesSimulation(int near, int total)
    {
        TrollZones.TestNear = near;
        TrollZones.TestTotal = total;
        return TrollZones.TestNear;
    }

    /// Is the zone of this troll kept alive by TrollZones now? Expected outcome: true when TrollZones pokes and simulates it.
    [TestHook("stt.trollZoneKept")]
    private static bool TrollZoneKept(GameObject troll)
    {
        return troll != null && TrollZones.IsZoneKept(troll.transform.position);
    }

    /// Zones TrollZones keeps now. Expected outcome: the count (0 = nothing kept).
    [TestHook("stt.keptZones")]
    private static int KeptZones()
    {
        return TrollZones.KeptCount;
    }
}
