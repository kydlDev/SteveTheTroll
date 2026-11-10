namespace SteveTheTroll.Features.Trolls.Combat.Domain;

/// <summary>
///     2026-11-05 (run1fix-focus, attack-key-creature: a frozen Greydwarf 1.7 m below the troll, 4.8 m away - just inside the 4.9 m reach; the troll stood still and
///     swung 6 times without one hit, forever): how close a chasing troll walks. It stops at its reach; when swings do not hurt the target it closes in, so a still or
///     lower creature at the edge of the reach is not swung at in the air. Game-free and unit-tested; TrollChase counts the swings and reads the target's health.
/// </summary>
internal static class ChaseRules
{
    /// Swings that did not hurt the target before the troll closes in.
    internal const int DrySwingsBeforeClosing = 2;

    /// The share of the reach the troll closes in to after dry swings.
    internal const float CloseFactor = 0.5f;

    /// The distance the troll walks up to: its reach, or half of it once DrySwingsBeforeClosing swings in a row did not hurt the target.
    internal static float ApproachDistance(float reach, int drySwings)
    {
        return drySwings >= DrySwingsBeforeClosing ? reach * CloseFactor : reach;
    }

    /// A target at least this far below the troll's feet gets the downward swing at once (D-180: a frozen creature 4 m below was swung over, 7 swings, no damage).
    internal const float LowTargetMetres = -1.5f;

    /// A target at least this far below (but not LowTargetMetres) gets the downward swing as soon as a swing in a row did not hurt it.
    internal const float SlightlyLowTargetMetres = -0.5f;

    /// D-201 (g16b-2, guard-mob-kills-open: the troll stood 1.2 - 1.5 m above a Greydwarf, i.e. just inside the old -1.5 m limit, and swung horizontally 5 times without one hit; the
    /// chase only stepped closer): which swing the chase uses. heightDifference = target y - troll y. A clearly lower target gets the downward swing; a slightly lower one
    /// gets it after an odd number of dry swings, so a missing swing type is switched, and switched back if the other one misses too.
    internal static bool UseDownwardSwing(float heightDifference, int drySwings)
    {
        if (heightDifference < LowTargetMetres)
        {
            return true;
        }

        return heightDifference < SlightlyLowTargetMetres && drySwings % 2 == 1;
    }

    /// Whether a finished swing counts as dry: the target's health did not drop since the swing started (a hit resets the count).
    internal static bool IsDry(float healthBefore, float healthAfter)
    {
        return healthAfter >= healthBefore - 0.01f;
    }
}
