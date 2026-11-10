using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Backpack.Domain;

/// <summary>
///     D-173 (Architecture V2, P6): one depot chest as the planner sees it (read by TrollDepot.Plan). Readonly
///     struct, game-free.
/// </summary>
internal readonly struct DepotCandidate
{
    internal bool HasEmptySlot { get; }

    /// Another tamed troll stands within the busy radius of it (D-107).
    internal bool Crowded { get; }

    /// Another troll reserved it and the reservation is still running (D-149).
    internal bool ReservedByOther { get; }

    /// Metres from the troll.
    internal float Distance { get; }

    /// #30: the game's path search finds a way to within reach of the chest. A candidate that is not asked about is reachable.
    internal bool Reachable { get; }

    internal DepotCandidate(bool hasEmptySlot, bool crowded, bool reservedByOther, float distance, bool reachable = true)
    {
        HasEmptySlot = hasEmptySlot;
        Crowded = crowded;
        ReservedByOther = reservedByOther;
        Distance = distance;
        Reachable = reachable;
    }
}

/// <summary>
///     D-173 (P6): the planner's answer - a chest to walk to, or a new depot (away from crowded stacks when chests
///     were skipped for another troll).
/// </summary>
internal readonly struct DepotPlan
{
    /// Index into the candidates, -1 = none (build a new depot).
    internal int Chosen { get; }

    /// Chests with room that were skipped because another troll is at / walks to them.
    internal int Skipped { get; }

    internal bool BuildNew => Chosen < 0;

    /// The new depot avoids stacks with another troll at them (D-107: "start a new one").
    internal bool AvoidBusy => Chosen < 0 && Skipped > 0;

    internal DepotPlan(int chosen, int skipped)
    {
        Chosen = chosen;
        Skipped = skipped;
    }
}

/// <summary>
///     D-173 (P6, spec "DepotPlanner"): where a full troll unloads - the nearest depot chest with room that no other troll
///     is at or walks to (D-064 / D-107 / D-149), #30: among the chests the troll can find a way to; when none of them can be
///     reached, the nearest of the others (as before - the walk has its own recovery and a depot is never abandoned),
///     else a new depot. Game-free, unit-tested.
/// </summary>
internal static class DepotPlanner
{
    /// Expected outcome: the nearest usable reachable chest (first of equal distances); with no reachable one the nearest usable chest; or Chosen -1 with the count of chests
    /// skipped for another troll.
    internal static DepotPlan Choose(IList<DepotCandidate> chests)
    {
        int nearestReachable = -1, nearestAny = -1, skipped = 0;
        float bestReachable = float.MaxValue, bestAny = float.MaxValue;
        if (chests == null)
        {
            return new DepotPlan(-1, 0);
        }

        for (int i = 0; i < chests.Count; i++)
        {
            DepotCandidate c = chests[i];
            if (!c.HasEmptySlot)
            {
                continue;
            }

            if (c.Crowded || c.ReservedByOther)
            {
                skipped++;
                continue;
            }

            if (c.Distance < bestAny)
            {
                bestAny = c.Distance;
                nearestAny = i;
            }

            if (c.Reachable && c.Distance < bestReachable)
            {
                bestReachable = c.Distance;
                nearestReachable = i;
            }
        }

        return new DepotPlan(nearestReachable >= 0 ? nearestReachable : nearestAny, skipped);
    }
}

/// <summary>D-173 (P6): what the unload does with one backpack stack.</summary>
internal enum UnloadStep
{
    /// A depot chest takes the whole stack: move it.
    Put,

    /// No chest has room and fewer than the allowed new depots were built: build one, then try again.
    BuildDepot,

    /// No room and no more new depots allowed: it stays in the backpack.
    Leave
}

/// <summary>
///     D-173 (P6, spec "UnloadPipeline" decision): per stack - a chest with room, a new depot (at most `maxNew` per
///     unload), or left over. Game-free.
/// </summary>
internal static class UnloadRules
{
    internal static UnloadStep ForStack(bool chestWithRoom, int built, int maxNew)
    {
        if (chestWithRoom)
        {
            return UnloadStep.Put;
        }

        return built < maxNew ? UnloadStep.BuildDepot : UnloadStep.Leave;
    }
}
