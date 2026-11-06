using NUnit.Framework;
using SteveTheTroll.Features.Trolls.World.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>NEXT_STEPS item 1: far trolls freeze when the game's simulation area is smaller (low graphics) - TrollZones keeps every zone the game does not run.</summary>
[TestFixture]
public class ZoneKeepRulesTests
{
    [TestCase(true, true, true)] [TestCase(true, false, false)] [TestCase(false, true, false)] [TestCase(false, false, false)]
    public void GameRunsZone_OnlyWhenBothCoverIt(bool active, bool near, bool expected)
    {
        Assert.That(ZoneKeepRules.GameRunsZone(active, near), Is.EqualTo(expected));
    }

    // near 1 (low graphics): a zone 2 away is outside the band -> must be kept; near 2 (normal): it is inside
    [TestCase(2, 0, 1, false)] [TestCase(2, 0, 2, true)] [TestCase(-1, 1, 1, true)] [TestCase(1, -2, 1, false)] [TestCase(0, 0, 0, true)]
    public void ClassicBand(int dx, int dz, int radius, bool expected)
    {
        Assert.That(ZoneKeepRules.InClassicBand(dx, dz, radius), Is.EqualTo(expected));
    }

    [Test]
    public void SmallSimulationDistance_LeavesTheZoneBetweenUnsimulatedWithoutTheFix()
    {
        // a zone 2 away: the old active-area test says "game runs it", the near-1 band does not -> now kept
        bool inActive = true, inNear = ZoneKeepRules.InClassicBand(2, 0, 1);
        Assert.That(ZoneKeepRules.GameRunsZone(inActive, inNear), Is.False);
    }
}
