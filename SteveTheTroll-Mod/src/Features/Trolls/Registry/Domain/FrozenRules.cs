using System;

namespace SteveTheTroll.Features.Trolls.Registry.Domain;

/// <summary>How far the frozen-troll ladder has gone (NEXT_STEPS item 4): mildest first.</summary>
internal enum FrozenStage
{
    None,

    /// 10 s without moving: the evidence is dumped, the player is told, the troll's zone is kept and its ZDO re-claimed.
    Reclaim,

    /// 30 s: its game object is rebuilt from the ZDO.
    Rebuild,

    /// 60 s: it is moved along its route (repeated every CatchUpEvery seconds while it stays frozen).
    CatchUp
}

/// <summary>
///     Game-free rules of the frozen-troll protection (NEXT_STEPS item 4): when a troll that should be travelling counts as frozen and which rung of the
///     self-heal ladder it is on. Runs outside the AI step (a frozen troll's AI step does not run).
/// </summary>
internal static class FrozenRules
{
    /// Flat metres a troll must have moved between two checks to count as moving.
    internal const float MoveEpsilon = 0.5f;

    internal const float ReclaimSeconds = 10f, RebuildSeconds = 30f, CatchUpSeconds = 60f, CatchUpEvery = 10f, CatchUpMetres = 25f;

    /// Did the troll move? Expected outcome: true when `flatMetres` reaches MoveEpsilon.
    internal static bool Moved(float flatMetres)
    {
        return flatMetres >= MoveEpsilon;
    }

    /// The rung for a troll that has not moved for `stillSeconds`. A troll that is not meant to travel is never frozen. Expected outcome: None below 10 s.
    internal static FrozenStage StageFor(bool travelling, float stillSeconds)
    {
        if (!travelling || stillSeconds < ReclaimSeconds)
        {
            return FrozenStage.None;
        }

        return stillSeconds >= CatchUpSeconds ? FrozenStage.CatchUp :
            stillSeconds >= RebuildSeconds ? FrozenStage.Rebuild : FrozenStage.Reclaim;
    }

    /// How far the catch-up moves the troll along its route: CatchUpMetres, never past the destination. Expected outcome: 0 .. CatchUpMetres.
    internal static float CatchUpStep(float distanceToDestination)
    {
        return Math.Max(0f, Math.Min(CatchUpMetres, distanceToDestination));
    }
}
