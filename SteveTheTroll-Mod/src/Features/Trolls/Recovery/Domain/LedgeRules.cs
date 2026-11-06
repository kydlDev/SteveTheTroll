using System;

namespace SteveTheTroll.Features.Trolls.Recovery.Domain;

/// <summary>
///     Item 5 (user 2026-11-06: "very very important: also in their farming paths (anywhere), and allow them to go down / up cliffs that are in meadows after a tree, not just
///     when pathfinding the way home"): the game-free rules of the LEDGE WALK - the last rung of the recovery ladder. When the game's path search finds no way (the navmesh has no
///     link over a steep step) the troll walks the straight line to its goal itself, one short step at a time with its feet on the ground under it, so it can go down or up a
///     cliff instead of giving its tree up. Flat metres / metres / seconds. TrollRecovery reads the ground and moves; nothing here knows Unity or Valheim.
/// </summary>
internal static class LedgeRules
{
    /// The ground jumps this much within CliffProbeMetres along the line to the goal: a step the troll cannot simply walk up / down (StuckRules.StepMaxRise is 0.9 m per metre).
    internal const float CliffRise = 2.5f, CliffProbeMetres = 3f;

    /// A ledge walk is only started for a goal at most this far (flat) and this much higher / lower: meadow cliffs, not mountain faces.
    internal const float MaxFlatMetres = 80f, MaxHeightDifference = 40f;

    /// Walking speed of the assisted steps (m/s) and the longest one ledge walk may take.
    internal const float Speed = 3.2f, MaxSeconds = 40f;

    /// The walk is over when the troll is this close to the goal (flat), or moved less than StallMetres per second for StallSeconds (something solid is in the way).
    internal const float ArriveMetres = 1.5f, StallSeconds = 2f, StallMetres = 0.3f;

    /// Is this height change over the probe distance a cliff? Expected outcome: true for a rise or fall of CliffRise or more.
    internal static bool IsCliff(float heightChange)
    {
        return Math.Abs(heightChange) >= CliffRise;
    }

    /// May a ledge walk to this goal start? Expected outcome: true for a goal within MaxFlatMetres and MaxHeightDifference.
    internal static bool Possible(float flatDistance, float heightDifference)
    {
        return flatDistance > 0f && flatDistance <= MaxFlatMetres && Math.Abs(heightDifference) <= MaxHeightDifference;
    }

    /// The length of the next assisted step: the speed times dt, never past the goal. Expected outcome: 0 .. Speed * dt.
    internal static float StepMetres(float dt, float flatDistanceToGoal)
    {
        return Math.Max(0f, Math.Min(Speed * Math.Max(dt, 0f), flatDistanceToGoal));
    }

    /// The ledge walk ends: the troll arrived, it ran out of time, or it stalled. Expected outcome: true when any of the three holds.
    internal static bool Done(float flatDistanceToGoal, float elapsedSeconds, float stalledSeconds)
    {
        return flatDistanceToGoal <= ArriveMetres || elapsedSeconds >= MaxSeconds || stalledSeconds >= StallSeconds;
    }
}
