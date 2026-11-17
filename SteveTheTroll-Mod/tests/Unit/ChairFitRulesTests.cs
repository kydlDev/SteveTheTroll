using NUnit.Framework;
using SteveTheTroll.Features.Riding.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>2026-11-17 (user): the rider's back glued a little in front of the chair back, a new centring when it goes 2 cm inside (game-free rules).</summary>
[TestFixture]
public class ChairFitRulesTests
{
    private const float Hips = 0.17f;

    [Test]
    public void Target_IsTheBackJustInFrontOfTheChairBack()
    {
        Assert.That(ChairFitRules.TargetGap(Hips), Is.EqualTo(Hips + ChairFitRules.ForwardMarginM).Within(1e-6f));
        Assert.That(ChairFitRules.ForwardMarginM, Is.GreaterThan(0f).And.LessThan(ChairFitRules.RecentreAtM));
    }

    [TestCase(0.17f, 0f)]       // the back touches the inside face
    [TestCase(0.20f, 0f)]       // in front of it
    [TestCase(0.15f, 0.02f)]    // 2 cm inside
    [TestCase(0.10f, 0.07f)]
    public void IntoBack_CountsFromTheInsideFace(float gap, float into)
    { Assert.That(ChairFitRules.IntoBack(gap, Hips), Is.EqualTo(into).Within(1e-5f)); }

    [Test]
    public void ForwardError_GluesBothWays()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChairFitRules.ForwardError(ChairFitRules.TargetGap(Hips), Hips), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(ChairFitRules.ForwardError(0.15f, Hips), Is.GreaterThan(0f), "too far back -> move forward");
            Assert.That(ChairFitRules.ForwardError(0.25f, Hips), Is.LessThan(0f), "too far forward -> move back (glued, not only pushed out)");
        }
    }

    [TestCase(0f, false)]
    [TestCase(0.019f, false)]     // 1 mm short of the limit
    [TestCase(0.02f, true)]
    [TestCase(0.05f, true)]
    public void Recentre_At2cmInside(float into, bool again)
    { Assert.That(ChairFitRules.NeedsRecentre(into), Is.EqualTo(again)); }

    [Test]
    public void Recentre_TheLabCaseOf2026_11_17()
    {
        // run 20261117-180708: centred (back on the face), then 2 cm inside 5 s later - now a new centring starts
        float gap = Hips - 0.02f;
        Assert.That(ChairFitRules.NeedsRecentre(ChairFitRules.IntoBack(gap, Hips)), Is.True);
    }

    [TestCase(0f, 0f, true)]
    [TestCase(0.009f, -0.009f, true)]
    [TestCase(0.01f, 0f, false)]
    [TestCase(0f, -0.01f, false)]
    public void Settled_BelowOneCentimetreBothWays(float side, float forward, bool settled)
    { Assert.That(ChairFitRules.Settled(side, forward), Is.EqualTo(settled)); }
}
