using NUnit.Framework;
using SteveTheTroll.Features.Hubs.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
///     D-217 (user 2026-11-10: "yes, everything should be from the outer limit"): every hub distance counts from the hub's outer edge (HubData.OuterRadius 15.5 m
///     from its middle), also the long ones (500 / 950 / 1000 / 1250 m) and the spacing between two hubs (edge to edge).
/// </summary>
[TestFixture]
public class HubEdgeDistanceTests
{
    private const float Outer = HubData.OuterRadius;

    [Test]
    public void OuterRadius_IsTheBuildingsEdge()
    {
        Assert.That(Outer, Is.EqualTo(15.5f));
    }

    [TestCase(-1f, -1f)]
    [TestCase(0f, 0f)]
    [TestCase(10f, 0f)]
    [TestCase(15.5f, 0f)]
    [TestCase(16.5f, 1f)]
    [TestCase(515.5f, 500f)]
    public void EdgeDistance_IsMiddleDistanceMinusTheOuterRadius(float fromMiddle, float expected)
    {
        Assert.That(HubRules.EdgeDistance(fromMiddle), Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(-1f, -1f)]
    [TestCase(20f, 0f)]
    [TestCase(31f, 0f)]
    [TestCase(1031f, 1000f)]
    public void EdgeToEdge_TakesBothBuildingsOff(float middles, float expected)
    {
        Assert.That(HubRules.EdgeToEdge(middles), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Serves_AtExactly500mFromTheEdge_NotOneMetreBeyond()
    {
        Assert.That(HubRules.Serves(HubRules.EdgeDistance(HubRules.ServeRadius + Outer), HubRules.ServeRadius), Is.True, "515.5 m from the middle = 500 m from the edge");
        Assert.That(HubRules.Serves(HubRules.EdgeDistance(HubRules.ServeRadius + Outer + 1f), HubRules.ServeRadius), Is.False);
        Assert.That(HubRules.Serves(HubRules.EdgeDistance(HubRules.ServeRadius + 1f), HubRules.ServeRadius), Is.True, "501 m from the middle was outside before D-217, it is inside now");
    }

    [Test]
    public void SpeedFactor_HalfSpeedStartsAt1250mFromTheEdge()
    {
        float tamed = 1.25f;
        Assert.That(HubRules.SpeedFactor(tamed, true, true, HubRules.EdgeDistance(HubRules.FarRadius + Outer)), Is.EqualTo(tamed));
        Assert.That(HubRules.SpeedFactor(tamed, true, true, HubRules.EdgeDistance(HubRules.FarRadius + Outer + 1f)), Is.EqualTo(tamed * HubRules.FarSpeedShare));
        Assert.That(HubRules.SpeedFactor(tamed, true, true, HubRules.EdgeDistance(Outer - 1f)), Is.EqualTo(HubRules.HubSpeedFactor), "inside the building");
    }

    [Test]
    public void FarmLeash_Is950mFromTheEdge()
    {
        Assert.That(HubRules.FarmAllowed(true, HubRules.EdgeDistance(HubRules.FarmLeash + Outer)), Is.True);
        Assert.That(HubRules.FarmAllowed(true, HubRules.EdgeDistance(HubRules.FarmLeash + Outer + 0.5f)), Is.False);
        Assert.That(HubRules.RadiusFromMiddle(HubRules.FarmLeash), Is.EqualTo(965.5f), "the explore ring around the hub's middle");
    }

    [Test]
    public void WalkBack_StartsBeyond1000mFromTheEdge_EndsInside950m()
    {
        Assert.That(HubRules.ShouldReturn(true, HubRules.EdgeDistance(HubRules.AutoReach + Outer), false), Is.False, "exactly 1000 m from the edge: stays");
        Assert.That(HubRules.ShouldReturn(true, HubRules.EdgeDistance(HubRules.AutoReach + Outer + 1f), false), Is.True);
        Assert.That(HubRules.ShouldReturn(true, HubRules.EdgeDistance(HubRules.FarmLeash + Outer + 1f), true), Is.True, "still walking back");
        Assert.That(HubRules.ShouldReturn(true, HubRules.EdgeDistance(HubRules.FarmLeash + Outer), true), Is.False);
        Assert.That(HubRules.ShouldReturn(false, HubRules.EdgeDistance(-1f), false), Is.False, "no hub");
    }

    [Test]
    public void Pick_ReachIs1000mFromTheEdge()
    {
        Assert.That(HubRules.Pick(2, HubRules.EdgeDistance(HubRules.ManualReach + Outer), 1, 5f, true), Is.EqualTo(2));
        Assert.That(HubRules.Pick(2, HubRules.EdgeDistance(HubRules.ManualReach + Outer + 1f), 1, 5f, true), Is.EqualTo(1), "the manual hub is out of reach, the nearest takes over");
        Assert.That(HubRules.Pick(0, -1f, 1, HubRules.EdgeDistance(HubRules.AutoReach + Outer + 1f), true), Is.EqualTo(0));
    }

    [Test]
    public void NewHub_NeedsA1000mGapBetweenTheEdges()
    {
        Assert.That(HubRules.Check(1, HubRules.EdgeToEdge(HubRules.MinSpacing + 2f * Outer)), Is.EqualTo(HubRules.Verdict.Ok), "middles 1031 m apart");
        Assert.That(HubRules.Check(1, HubRules.EdgeToEdge(HubRules.MinSpacing + 2f * Outer - 1f)), Is.EqualTo(HubRules.Verdict.TooClose));
        Assert.That(HubRules.Check(1, HubRules.EdgeToEdge(HubRules.MinSpacing + 1f)), Is.EqualTo(HubRules.Verdict.TooClose), "1001 m between the middles was allowed before D-217");
        Assert.That(HubRules.Check(1, HubRules.EdgeToEdge(-1f)), Is.EqualTo(HubRules.Verdict.Ok), "no other hub");
    }
}
