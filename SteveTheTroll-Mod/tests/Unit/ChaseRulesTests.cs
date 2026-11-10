using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Combat.Domain;

namespace SteveTheTroll.Tests.Unit;

[TestFixture]
public class ChaseRulesTests
{
    [Test]
    public void Approach_is_the_reach_while_swings_hurt()
    {
        Assert.That(ChaseRules.ApproachDistance(4.9f, 0), Is.EqualTo(4.9f).Within(0.001f));
        Assert.That(ChaseRules.ApproachDistance(4.9f, ChaseRules.DrySwingsBeforeClosing - 1), Is.EqualTo(4.9f).Within(0.001f));
    }

    [Test]
    public void Approach_closes_in_after_dry_swings()
    {
        Assert.That(ChaseRules.ApproachDistance(4.9f, ChaseRules.DrySwingsBeforeClosing), Is.EqualTo(2.45f).Within(0.001f));
        Assert.That(ChaseRules.ApproachDistance(4.9f, 10), Is.EqualTo(2.45f).Within(0.001f));
    }

    [Test]
    public void Close_in_point_is_inside_the_swing_range()
    {
        Assert.That(ChaseRules.ApproachDistance(4.9f, 5), Is.LessThan(4.9f));
    }

    // D-201 (g16b-2 guard-mob-kills-open: troll 1.2 - 1.5 m above a Greydwarf, 5 level swings, no hit)
    [TestCase(-4f, 0, true)]      // clearly below: downward at once (D-180)
    [TestCase(-1.6f, 0, true)]
    [TestCase(-1.5f, 0, false)]   // the old limit is exclusive: the first swing is level ...
    [TestCase(-1.4f, 0, false)]
    [TestCase(-1.4f, 1, true)]    // ... and a level swing that missed switches to the downward one
    [TestCase(-1.4f, 2, false)]   // two misses: back to level (the other type missed too)
    [TestCase(-1.4f, 3, true)]
    [TestCase(-1.2f, 1, true)]
    [TestCase(-0.6f, 1, true)]
    [TestCase(-0.4f, 1, false)]   // almost level: never a downward swing
    [TestCase(0f, 1, false)]
    [TestCase(2f, 1, false)]      // a target above: never downward
    [TestCase(-4f, 3, true)]
    public void The_swing_type_follows_the_height_and_switches_after_a_dry_swing(float heightDifference, int drySwings, bool downward)
    {
        Assert.That(ChaseRules.UseDownwardSwing(heightDifference, drySwings), Is.EqualTo(downward));
    }

    [TestCase(40f, 40f, true)]
    [TestCase(40f, 40.5f, true)]
    [TestCase(40f, 33f, false)]
    [TestCase(40f, 39.995f, true)]
    public void A_swing_is_dry_when_the_health_did_not_drop(float before, float after, bool dry)
    {
        Assert.That(ChaseRules.IsDry(before, after), Is.EqualTo(dry));
    }
}
