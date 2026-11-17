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
    public void TooFarInside_At2cm(float into, bool inside)
    { Assert.That(ChairFitRules.TooFarInside(into), Is.EqualTo(inside)); }

    [TestCase(0.05f, 1, false)]   // one pose while the troll starts walking: not yet
    [TestCase(0.05f, 2, false)]
    [TestCase(0.05f, 3, true)]    // 0.75 s in a row
    [TestCase(0.05f, 7, true)]
    [TestCase(0.01f, 9, false)]   // in a row, but not 2 cm inside
    public void Recentre_OnlyWhenItLasts(float into, int inARow, bool again)
    { Assert.That(ChairFitRules.NeedsRecentre(into, inARow), Is.EqualTo(again)); }

    [Test]
    public void Recentre_KeepsTheSidewaysPlaceOfTheMount()
    {
        // repeat run rep-chair, rep02: a re-run that also corrected sideways at the start of a walk left the rider 14 cm off the middle line
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChairFitRules.SideErrorFor(true, -0.15f), Is.EqualTo(0f));
            Assert.That(ChairFitRules.SideErrorFor(false, -0.15f), Is.EqualTo(-0.15f));
        }
    }

    [Test]
    public void Recentre_TheLabCaseOf2026_11_17()
    {
        // run 20261117-180708: centred (back on the face), then 2 cm inside 5 s later - now a new centring starts
        float gap = Hips - 0.02f;
        Assert.That(ChairFitRules.NeedsRecentre(ChairFitRules.IntoBack(gap, Hips), ChairFitRules.RecentreAfterChecks), Is.True);
    }

    [TestCase(0f, 0f, true)]
    [TestCase(0.009f, -0.009f, true)]
    [TestCase(0.01f, 0f, false)]
    [TestCase(0f, -0.01f, false)]
    public void Settled_BelowOneCentimetreBothWays(float side, float forward, bool settled)
    { Assert.That(ChairFitRules.Settled(side, forward), Is.EqualTo(settled)); }
}
