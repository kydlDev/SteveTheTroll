using NUnit.Framework;
using SteveTheTroll.Features.Riding.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
///     T-217 R2 / R5: "input stopped" is not "the troll stands". Manual test 20261110-015153: 6.5 m of root displacement in 2 s under a rider who asked for nothing.
/// </summary>
[TestFixture]
public class RideStillTests
{
    [Test]
    public void WhileMovementIsExpected_NothingIsDrift()
    {
        Assert.That(RideStill.Judge(true, 0f, 0f), Is.EqualTo(StillVerdict.Driven));
        Assert.That(RideStill.Judge(true, 100f, 50f), Is.EqualTo(StillVerdict.Driven), "driving 50 m is not drifting");
    }

    [Test]
    public void RightAfterTheInputStops_TheTrollMayRollOut()
    {
        Assert.That(RideStill.Judge(false, 0f, 0f), Is.EqualTo(StillVerdict.Settling));
        Assert.That(RideStill.Judge(false, RideStill.SettleSeconds - 0.01f, 3f), Is.EqualTo(StillVerdict.Settling));
    }

    [Test]
    public void AfterSettling_AStandingTroll_IsStill()
    {
        Assert.That(RideStill.Judge(false, RideStill.SettleSeconds, 0f), Is.EqualTo(StillVerdict.Still));
        Assert.That(RideStill.Judge(false, 60f, RideStill.DriftLimitMetres), Is.EqualTo(StillVerdict.Still), "exactly the limit is still fine");
    }

    [Test]
    public void TheLedgeWalkOfTheManualTest_IsDrifting()
    {
        // 6.5 m from the settled place with no input
        Assert.That(RideStill.Judge(false, 4f, 6.5f), Is.EqualTo(StillVerdict.Drifting));
        Assert.That(RideStill.Judge(false, RideStill.SettleSeconds, RideStill.DriftLimitMetres + 0.01f), Is.EqualTo(StillVerdict.Drifting));
    }

    [Test]
    public void Limits_AreTheDocumentedOnes()
    {
        Assert.That(RideStill.SettleSeconds, Is.EqualTo(2f));
        Assert.That(RideStill.DriftLimitMetres, Is.EqualTo(0.5f));
    }
}
