using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Combat.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-214 (user 2026-11-10: Steve 3 farmed logs 14 - 16 m from the middle of HUB 1 - the ring was only checked around the bed): the no-hit circles of the bed and of every Troll Hub.</summary>
[TestFixture]
public class NoHitHubRingTests
{
    const float Ring = 15f, HubRadius = 15.5f;
    static readonly NoHitCircle Bed = new(0f, 0f, Ring);
    static readonly NoHitCircle Hub1 = new(334f, -311f, NoHitRules.HubCircleRadius(Ring, HubRadius));

    [Test]
    public void HubCircle_ReachesTheRingBeyondItsWalls()
    {
        Assert.That(NoHitRules.HubCircleRadius(Ring, HubRadius), Is.EqualTo(30.5f).Within(1e-4f));
    }

    [Test]
    public void RingOff_MeansNoHubCircleEither()
    {
        Assert.That(NoHitRules.HubCircleRadius(0f, HubRadius), Is.EqualTo(0f));
    }

    // the three spots Steve 3 swung from at 02:00 in the user's session: (321, -304) 14.8 m, (318, -310) 16 m and (320, -303) 16.1 m from the hub's middle
    [TestCase(321f, -304f)]
    [TestCase(318f, -310f)]
    [TestCase(320f, -303f)]
    public void TheSpotsOfTheReport_AreInsideTheHubCircle(float x, float z)
    {
        Assert.That(NoHitRules.InsideAny(new[] { Bed, Hub1 }, x, z), Is.True);
    }

    [Test]
    public void BedAlone_DidNotCoverTheHub()
    {
        // what the old code checked: only the bed's circle - the report's spot was 450 m from it
        Assert.That(NoHitRules.InsideAny(new[] { Bed }, 321f, -304f), Is.False);
    }

    [TestCase(334f, -276f)]   // 35 m north of the hub's middle: outside 30.5 m
    [TestCase(300f, -340f)]   // 44 m away
    public void FarFromBedAndHub_IsOutside(float x, float z)
    {
        Assert.That(NoHitRules.InsideAny(new[] { Bed, Hub1 }, x, z), Is.False);
    }

    [Test]
    public void Several_HubsAndTheBed_AnyOneIsEnough()
    {
        var hub2 = new NoHitCircle(-500f, 200f, NoHitRules.HubCircleRadius(Ring, HubRadius));
        var all = new[] { Bed, Hub1, hub2 };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(NoHitRules.InsideAny(all, 5f, 5f), Is.True, "bed");
            Assert.That(NoHitRules.InsideAny(all, 330f, -300f), Is.True, "hub 1");
            Assert.That(NoHitRules.InsideAny(all, -490f, 210f), Is.True, "hub 2");
            Assert.That(NoHitRules.InsideAny(all, 100f, 100f), Is.False, "between them");
        }
    }

    [Test]
    public void NoCircles_NothingIsProtected()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(NoHitRules.InsideAny(null, 0f, 0f), Is.False);
            Assert.That(NoHitRules.InsideAny(new NoHitCircle[0], 0f, 0f), Is.False);
        }
    }

    [Test]
    public void CircleOfRadiusZero_IsOff()
    {
        Assert.That(NoHitRules.InsideAny(new[] { new NoHitCircle(0f, 0f, 0f) }, 0f, 0f), Is.False);
    }
}
