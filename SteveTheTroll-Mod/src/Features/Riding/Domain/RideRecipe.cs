using System.Collections.Generic;

namespace SteveTheTroll.Features.Riding.Domain;

/// <summary>
///     T-213 (user 2026-11-06): the recipe of the halter + seat. A powerful item that is not there from the start: workbench level 3, no upgrades, not consumed on use.
///     Prefab names are the game's, the display names are English.
/// </summary>
internal static class RideRecipe
{
    internal const string PrefabName = "STT_TrollHalter";
    internal const string DisplayName = "Troll Halter and Seat";
    internal const int StationLevel = 3;
    internal const int MaxQuality = 1;

    /// The cost, in recipe order: prefab, English display name, amount. Expected outcome: 4 lines (Wolf Pelt 15, Linen Thread 15, Iron 10, Round Log 10).
    internal static readonly IReadOnlyList<RecipeLine> Lines = new[]
    {
        new RecipeLine("WolfPelt", "Wolf Pelt", 15),
        new RecipeLine("LinenThread", "Linen Thread", 15),
        new RecipeLine("Iron", "Iron", 10),
        new RecipeLine("RoundLog", "Round Log", 10)
    };
}

internal readonly struct RecipeLine
{
    internal RecipeLine(string prefab, string english, int amount)
    {
        Prefab = prefab;
        English = english;
        Amount = amount;
    }

    internal string Prefab { get; }
    internal string English { get; }
    internal int Amount { get; }
}