using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>D-189: hooks for the farmed-out troll that explores on (user 2026-11-06: tests at several ranges so the full range is covered).</summary>
internal static class ExploreTestHooks
{
    /// Sets the two search ranges (FarmMaxRange 50 - 200, FarmLongRange 150 - 1000); stt.reset puts the defaults back. Expected outcome: the values now in force.
    [TestHook("stt.farmRanges")]
    private static string FarmRanges(float max, float far)
    {
        ModSettings.FarmMaxRange.Value = max;
        ModSettings.FarmLongRange.Value = far;
        return ModSettings.FarmMaxRange.Value.ToString("F0") + " / " + ModSettings.FarmLongRange.Value.ToString("F0");
    }

    /// The followed troll (by its id, whichever object it has) is doing something purposeful: walking to a source / an exploration point, or working on a source. A troll that stands and waits is false.
    /// Expected outcome: false while not loaded.
    [TestHook("stt.followedPurposeful")]
    private static bool FollowedPurposeful()
    {
        string id = TestHooks.FollowedId();
        Character c = id.Length > 0 ? TrollRegistry.FindLoaded(id) : null;
        TrollFarmer f = c != null ? c.GetComponent<TrollFarmer>() : null;
        return f != null && (f.Travelling || f.CurrentItem.Length > 0 || f.Unloading || f.GoingHome);
    }

    /// Exploration steps taken since stt.reset, and the followed troll's distance to its hub, as text (diagnosis). Expected outcome: the line.
    [TestHook("stt.exploreStats")]
    private static string ExploreStats()
    {
        return "explore steps " + TrollFarmer.ExploreStepsTotal;
    }

    /// At least `min` exploration steps were taken since stt.reset. Expected outcome: true once.
    [TestHook("stt.exploredAtLeast")]
    private static bool ExploredAtLeast(int min)
    {
        return TrollFarmer.ExploreStepsTotal >= min;
    }

    /// stt.reset: the default ranges and a zero counter. Expected outcome: nothing.
    internal static void Reset()
    {
        ModSettings.FarmMaxRange.Reset();
        ModSettings.FarmLongRange.Reset();
        TrollFarmer.ExploreStepsTotal = 0;
    }
}
