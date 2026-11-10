using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Farming.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-189: the exploration point of a troll that has farmed everything around its hub (user 2026-11-06).</summary>
[TestFixture]
public class ExploreRulesTests
{
    [Test]
    public void GoesStraightOn_ByStepMetres()
    {
        bool ok = ExploreRules.Next(100f, 0f, 0f, 0f, 0f, 950f, null, out float x, out float z, out float h);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ok, Is.True);
            Assert.That(x, Is.EqualTo(250f).Within(0.01f));
            Assert.That(z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(h, Is.EqualTo(0f));
        }
    }

    [Test]
    public void PulledBackOntoTheLeashCircle()
    {
        ExploreRules.Next(800f, 0f, 0f, 0f, 0f, 950f, null, out float x, out float z, out float h);
        Assert.That(x, Is.EqualTo(925f).Within(0.01f), "950 m leash minus the 25 m margin");
    }

    [Test]
    public void AtTheEdge_TurnsAway()
    {
        // at 925 m heading outwards the straight step gains nothing -> it turns (+60 deg is the first alternative after 0)
        bool ok = ExploreRules.Next(925f, 0f, 0f, 0f, 0f, 950f, null, out float x, out float z, out float h);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ok, Is.True);
            Assert.That(h, Is.Not.EqualTo(0f));
            Assert.That(System.Math.Sqrt(x * x + z * z), Is.LessThanOrEqualTo(925.01));
        }
    }

    [Test]
    public void BlockedPlaceIsSkipped()
    {
        bool ok = ExploreRules.Next(100f, 0f, 0f, 0f, 0f, 950f, (x, z) => x > 200f && System.Math.Abs(z) < 1f, out _, out _, out float h);
        Assert.That(ok && h != 0f, Is.True, "the straight place was given up, another direction is taken");
    }

    [Test]
    public void EverythingBlocked_IsExhausted()
    {
        Assert.That(ExploreRules.Next(100f, 0f, 0f, 0f, 0f, 950f, (x, z) => true, out _, out _, out _), Is.False);
    }

    [Test]
    public void NeverLeavesTheLeash_FromAnyAngle()
    {
        for (int a = 0; a < 360; a += 15)
        {
            if (ExploreRules.Next(900f, 0f, 0f, 0f, a, 950f, null, out float x, out float z, out _))
            {
                Assert.That(System.Math.Sqrt(x * x + z * z), Is.LessThanOrEqualTo(925.01), "heading " + a);
            }
        }
    }

    [TestCase(10f, 0f, 0f, 0f, 90f, 0f)]
    [TestCase(0f, -50f, 0f, 0f, 0f, -90f)]
    [TestCase(2f, 2f, 0f, 0f, 33f, 33f)]
    public void StartHeading_AwayFromTheHub(float px, float pz, float hx, float hz, float forward, float expected)
    {
        Assert.That(ExploreRules.StartHeading(px, pz, hx, hz, forward), Is.EqualTo(expected).Within(0.01f));
    }
}
