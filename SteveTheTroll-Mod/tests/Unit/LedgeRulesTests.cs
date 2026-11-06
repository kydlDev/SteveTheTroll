using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Recovery.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>Item 5 (user 2026-11-06): the ledge walk - a troll goes down / up a cliff on its way to ANY goal when the game's path has no way. Rules and the place of the rung in the ladder.</summary>
[TestFixture]
public class LedgeRulesTests
{
    const float Stuck = StuckRules.StuckTrigger;

    [TestCase(0f, false)] [TestCase(2.4f, false)] [TestCase(2.5f, true)] [TestCase(-2.5f, true)] [TestCase(-30f, true)]
    public void IsCliff(float change, bool expected) { Assert.That(LedgeRules.IsCliff(change), Is.EqualTo(expected)); }

    [TestCase(10f, 8f, true)] [TestCase(10f, -40f, true)] [TestCase(10f, 41f, false)] [TestCase(80f, 5f, true)] [TestCase(80.1f, 5f, false)] [TestCase(0f, 0f, false)]
    public void Possible(float flat, float height, bool expected) { Assert.That(LedgeRules.Possible(flat, height), Is.EqualTo(expected)); }

    [TestCase(0.016f, 50f, 0.0512f)] [TestCase(1f, 0.5f, 0.5f)] [TestCase(1f, 0f, 0f)] [TestCase(-1f, 5f, 0f)]
    public void StepMetres_NeverPastTheGoal(float dt, float toGoal, float expected) { Assert.That(LedgeRules.StepMetres(dt, toGoal), Is.EqualTo(expected).Within(0.0001f)); }

    [TestCase(1.5f, 0f, 0f, true)] [TestCase(20f, 39.9f, 1.9f, false)] [TestCase(20f, 40f, 0f, true)] [TestCase(20f, 5f, 2f, true)]
    public void Done(float toGoal, float elapsed, float stalled, bool expected) { Assert.That(LedgeRules.Done(toGoal, elapsed, stalled), Is.EqualTo(expected)); }

    [TestCase(1.5f, 0f, 0f, "arrived")] [TestCase(20f, 40f, 0f, "timeout")] [TestCase(20f, 5f, 2f, "stalled")] [TestCase(20f, 5f, 0.5f, "running")] [TestCase(1f, 50f, 5f, "arrived")]
    public void Outcome(float toGoal, float elapsed, float stalled, string expected) { Assert.That(LedgeRules.Outcome(toGoal, elapsed, stalled), Is.EqualTo(expected)); }

    [Test] public void CliffAhead_LedgeWalkComesBeforeAnyDetour()
    {
        var m = new RecoveryMachine();
        Assert.That(m.Step(10f, Stuck, 0f, false, false, false, cliffAhead: true, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartLedge));
        Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Ledge));
        Assert.That(m.Step(11f, 0f, 0f, false, false, false), Is.EqualTo(RecoveryCmd.None), "it runs until the adapter says it is done");
        Assert.That(m.Step(12f, 0f, 0f, false, false, false, ledgeDone: true), Is.EqualTo(RecoveryCmd.EndLedge));
        Assert.That(m.Stage, Is.EqualTo(RecoveryStage.Travel));
    }

    [Test] public void RageStillComesFirst_ThenTheLedgeWalk()
    {
        var m = new RecoveryMachine();
        Assert.That(m.Step(10f, Stuck, 0f, true, false, false, cliffAhead: true, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartRage));
        Assert.That(m.Step(15f, 0f, 0f, false, false, false), Is.EqualTo(RecoveryCmd.EndRage));
        Assert.That(m.Step(16f, Stuck, 0f, true, false, false, cliffAhead: true, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartLedge));
    }

    [Test] public void NoCliff_DetoursFirst_TheLedgeWalkIsTheLastRungBeforeTheCycleFails()
    {
        var m = new RecoveryMachine();
        Assert.That(m.Step(10f, Stuck, 0f, false, false, false, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartLeg));
        Assert.That(m.Step(11f, 0f, 0f, false, false, true, ledgeOk: true), Is.EqualTo(RecoveryCmd.EndLeg));
        Assert.That(m.Step(12f, Stuck, 0f, false, false, false, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartLeg));
        Assert.That(m.Step(13f, 0f, 0f, false, false, true, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartLeg), "detour 2, leg 2");
        Assert.That(m.Step(14f, 0f, 0f, false, false, true, ledgeOk: true), Is.EqualTo(RecoveryCmd.EndLeg));
        Assert.That(m.Step(15f, Stuck, 0f, false, false, false, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartLedge));
    }

    [Test] public void LedgeWalkIsUsedOncePerCycle_ThenTheCycleFailsAsBefore()
    {
        var m = new RecoveryMachine();
        m.Step(10f, Stuck, 0f, false, false, false, cliffAhead: true, ledgeOk: true);
        m.Step(11f, 0f, 0f, false, false, false, ledgeDone: true);
        Assert.That(m.LedgeUsed, Is.True);
        Assert.That(m.Step(12f, Stuck, 0f, false, false, false, cliffAhead: true, ledgeOk: true), Is.EqualTo(RecoveryCmd.StartLeg), "a second ledge walk is not started in the same cycle");
        m.BeginCycle(1);
        Assert.That(m.LedgeUsed, Is.False);
    }

    [Test] public void LedgeNotPossible_BehavesExactlyAsBefore()
    {
        var m = new RecoveryMachine();
        Assert.That(m.Step(10f, Stuck, 0f, false, false, false, cliffAhead: true, ledgeOk: false), Is.EqualTo(RecoveryCmd.StartLeg));
    }
}
