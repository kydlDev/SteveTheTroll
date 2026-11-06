namespace SteveTheTroll.Features.Trolls.World.Domain;

/// <summary>
///     Game-free rule for TrollZones (NEXT_STEPS item 1): which zones the game already simulates and TrollZones therefore leaves alone.
///     At low graphics settings the game's synced simulation distance shrinks ("near 1, total 3 zones" instead of "near 2, total 4"); a zone
///     that the game's ZNetScene.InActiveArea still lists but the near band no longer covers was skipped and nobody simulated it.
/// </summary>
internal static class ZoneKeepRules
{
    /// <summary>
    ///     Does the game itself run the objects of this zone? Only when BOTH its active-area test and its near simulation band cover the zone -
    ///     a zone outside either one is kept by TrollZones. Expected outcome: true = leave it to the game.
    /// </summary>
    internal static bool GameRunsZone(bool inActiveArea, bool inNearBand)
    {
        return inActiveArea && inNearBand;
    }

    /// <summary>
    ///     2026-11-06: an own troll ZDO that is persistent and has no owner is claimed by this client BEFORE the zone decision (the zone test judges a whole zone, the troll may be at its edge).
    ///     Expected outcome: true only for a persistent troll without an owner.
    /// </summary>
    internal static bool ShouldClaimOwnTroll(bool persistent, bool hasOwner)
    {
        return persistent && !hasOwner;
    }

    /// <summary>
    ///     Classic (square) band: is the zone within `radius` zones of the centre (Chebyshev distance)? Expected outcome: the game's answer for a classic
    ///     simulation distance.
    /// </summary>
    internal static bool InClassicBand(int dx, int dz, int radius)
    {
        return System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)) <= radius;
    }
}
