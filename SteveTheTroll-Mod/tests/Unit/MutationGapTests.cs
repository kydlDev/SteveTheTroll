using System;
using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Features.Hubs.Application;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Home.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Application;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Combat.Application;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Features.Trolls.Farming.Application;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Orders.Application;
using SteveTheTroll.Features.Trolls.Orders.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Application;
using SteveTheTroll.Features.Trolls.Recovery.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
///     2026-11-15 (user: every testable file has to reach 80 % in line coverage AND mutation score, Stryker level Advanced): tests for what the Stryker run found that no
///     test noticed - the trace text of the pipelines (names of the pipeline and its steps), the log texts of the input structs, and exact boundaries / geometry.
/// </summary>
public class MutationGapTests
{
    // ---------------------------------------------------------------- pipeline traces: pipeline name + step name

    private static void Stops(List<string> log, int index, string pipeline, string step)
    {
        Assert.That(log[index], Does.StartWith(pipeline + ": stopped at " + step + " - "));
    }

    [Test]
    public void DefencePipeline_Trace_NamesPipelineAndSteps()
    {
        var log = new List<string>();
        var p = new DefencePipeline(log.Add);
        p.Evaluate(new DefenceInput(true, false, false, 5f, 10f));
        p.Evaluate(new DefenceInput(true, true, false, 11f, 10f));
        p.Evaluate(new DefenceInput(false, true, false, 5f, 10f));
        p.Evaluate(new DefenceInput(true, true, false, 5f, 10f));
        Stops(log, 0, "DefencePipeline", "slippers or raid");
        Stops(log, 1, "DefencePipeline", "near the owner");
        Stops(log, 2, "DefencePipeline", "troll owner");
        Assert.That(log[3], Does.StartWith("DefencePipeline: 3 checks passed - "));
    }

    [Test]
    public void FarmingPipeline_Trace_NamesPipelineAndStep()
    {
        var log = new List<string>();
        var p = new FarmingPipeline(log.Add);
        p.Evaluate(new FarmGateInput { Alive = true, IsOwner = false });
        p.Evaluate(new FarmGateInput { Alive = true, IsOwner = true, HasWeapon = true });
        Stops(log, 0, "FarmingPipeline", "owner");
        Assert.That(log[1], Does.StartWith("FarmingPipeline: 1 checks passed - "));
    }

    [Test]
    public void HubAssignmentPipeline_Trace_NamesPipelineAndSteps()
    {
        var log = new List<string>();
        var p = new HubAssignmentPipeline(log.Add);
        p.Evaluate(new HubAssignmentInput(false, 7L, 0, -1f, 2, 300f, true));
        p.Evaluate(new HubAssignmentInput(true, 0L, 0, -1f, 2, 300f, true));
        p.Evaluate(new HubAssignmentInput(true, 7L, 0, -1f, 2, 300f, true));
        Stops(log, 0, "HubAssignmentPipeline", "hubs on");
        Stops(log, 1, "HubAssignmentPipeline", "owner");
        Assert.That(log[2], Does.StartWith("HubAssignmentPipeline: 2 checks passed - "));
    }

    [Test]
    public void HubReturnPipeline_Trace_NamesBothPipelinesAndSteps()
    {
        var log = new List<string>();
        var p = new HubReturnPipeline(log.Add);
        p.Start(new HubReturnInput { TrollOwner = false, HasHub = true });
        p.Step(new HubReturnInput { TrollOwner = false, HasHub = true });
        p.Step(new HubReturnInput { TrollOwner = true, HasHub = false });
        Stops(log, 0, "HubReturnPipeline.Start", "troll owner");
        Stops(log, 1, "HubReturnPipeline.Step", "troll owner");
        Stops(log, 2, "HubReturnPipeline.Step", "hub");
    }

    [Test]
    public void OrderPipeline_Trace_NamesPipelineAndSteps()
    {
        var log = new List<string>();
        var p = new OrderPipeline(log.Add);
        OrderInput ok = new OrderInput { KeyPressed = true, PlayerAlive = true, UiBlocked = false, HasTroll = true, CanSteer = true };
        p.Evaluate(new OrderInput { KeyPressed = false, PlayerAlive = true, HasTroll = true, CanSteer = true });
        p.Evaluate(new OrderInput { KeyPressed = true, PlayerAlive = false, HasTroll = true, CanSteer = true });
        p.Evaluate(new OrderInput { KeyPressed = true, PlayerAlive = true, UiBlocked = true, HasTroll = true, CanSteer = true });
        p.Evaluate(new OrderInput { KeyPressed = true, PlayerAlive = true, HasTroll = false, CanSteer = true });
        p.Evaluate(new OrderInput { KeyPressed = true, PlayerAlive = true, HasTroll = true, CanSteer = false });
        p.Evaluate(ok);
        Stops(log, 0, "OrderPipeline", "key");
        Stops(log, 1, "OrderPipeline", "player");
        Stops(log, 2, "OrderPipeline", "ui");
        Stops(log, 3, "OrderPipeline", "troll");
        Stops(log, 4, "OrderPipeline", "steer");
        Assert.That(log[5], Does.StartWith("OrderPipeline: 5 checks passed - "));
    }

    [Test]
    public void PickupPipeline_Trace_NamesPipelineAndSteps()
    {
        var log = new List<string>();
        var p = new PickupPipeline(log.Add);
        p.Evaluate(new PickupInput(false, false, false, false, true, true));
        p.Evaluate(new PickupInput(true, true, false, false, true, true));
        p.Evaluate(new PickupInput(true, false, true, false, true, true));
        p.Evaluate(new PickupInput(true, false, false, true, true, true));
        p.Evaluate(new PickupInput(true, false, false, false, true, true));
        Stops(log, 0, "PickupPipeline", "troll owner");
        Stops(log, 1, "PickupPipeline", "chest open");
        Stops(log, 2, "PickupPipeline", "full");
        Stops(log, 3, "PickupPipeline", "budget");
        Assert.That(log[4], Does.StartWith("PickupPipeline: 4 checks passed - "));
    }

    [Test]
    public void UnloadPipeline_Trace_NamesPipelineAndSteps()
    {
        var log = new List<string>();
        var p = new UnloadPipeline(log.Add);
        p.Evaluate(new UnloadInput(false, true, true, 0, 3));
        p.Evaluate(new UnloadInput(true, false, true, 0, 3));
        p.Evaluate(new UnloadInput(true, true, false, 1, 3));
        Stops(log, 0, "UnloadPipeline", "troll owner");
        Stops(log, 1, "UnloadPipeline", "backpack data");
        Assert.That(log[2], Does.StartWith("UnloadPipeline: 2 checks passed - "));
    }

    private static RecoveryInput RIn(float now, float stuck = 0f, bool alive = true, bool owner = true, bool headOn = false)
    {
        return new RecoveryInput { Now = now, StuckSeconds = stuck, IsAlive = alive, IsOwner = owner, HeadOn = headOn };
    }

    [Test]
    public void RecoveryPipeline_Trace_NamesPipelineAndSteps()
    {
        var log = new List<string>();
        var p = new RecoveryPipeline(log.Add);
        p.Evaluate(RIn(0f, alive: false));
        p.Evaluate(RIn(0f, owner: false));
        p.Evaluate(RIn(0f));
        Stops(log, 0, "RecoveryPipeline", "alive");
        Stops(log, 1, "RecoveryPipeline", "owner");
        Assert.That(log[2], Does.StartWith("RecoveryPipeline: 2 checks passed - "));
    }

    // ---------------------------------------------------------------- log texts of the input structs / decision

    [Test]
    public void RecoveryInput_ToString_ListsEveryFact()
    {
        var all = new RecoveryInput { Now = 1.5f, StuckSeconds = 2.5f, SustainedSeconds = 3.5f, Rageable = true, HeadOn = true, LegDone = true, IsAlive = true, IsOwner = true };
        Assert.That(all.ToString(), Is.EqualTo("t " + 1.5f.ToString("F1") + " s, stuck " + 2.5f.ToString("F1") + " s, progress " + 3.5f.ToString("F1")
                                              + " s, rageable True, head-on True, leg done True, alive True, owner True"));
        Assert.That(default(RecoveryInput).ToString(), Is.EqualTo("t " + 0f.ToString("F1") + " s, stuck " + 0f.ToString("F1") + " s, progress " + 0f.ToString("F1")
                                                                 + " s, rageable False, head-on False, leg done False, alive False, owner False"));
    }

    [Test]
    public void FarmGateInput_ToString_ListsEveryFact()
    {
        var all = new FarmGateInput { Alive = true, IsOwner = true, FollowOrder = true, Unloading = true, Fighting = true, RecoveryBusy = true, FullCheckDue = true, Home = true, HasWeapon = true };
        Assert.That(all.ToString(), Is.EqualTo("alive True, owner True, follow True, unloading True, fighting True, recovery True, full check True, home True, weapon True"));
        Assert.That(default(FarmGateInput).ToString(), Is.EqualTo("alive False, owner False, follow False, unloading False, fighting False, recovery False, full check False, home False, weapon False"));
    }

    [Test]
    public void OrderInput_ToString_ListsEveryFact()
    {
        var all = new OrderInput { KeyPressed = true, PlayerAlive = true, UiBlocked = true, HasTroll = true, CanSteer = true, HasCreature = true, HasSource = true, SourceIsWall = true };
        Assert.That(all.ToString(), Is.EqualTo("key True, alive True, ui blocked True, troll True, steer True, creature True, source True, wall True"));
        Assert.That(default(OrderInput).ToString(), Is.EqualTo("key False, alive False, ui blocked False, troll False, steer False, creature False, source False, wall False"));
    }

    [Test]
    public void RecoveryDecision_ToString_NamesStateAndHeadOn()
    {
        var plain = new RecoveryPipeline().Evaluate(RIn(0f, StuckRules.StuckTrigger)).Value;
        var head = new RecoveryPipeline().Evaluate(RIn(0f, StuckRules.StuckTrigger, headOn: true)).Value;
        Assert.That(head.HeadOn, Is.True);
        Assert.That(plain.HeadOn, Is.False);
        foreach (RecoveryDecision d in new[] { plain, head })
        {
            Assert.That(d.ToString(), Is.EqualTo(d.Command + " (stage " + d.Stage + ", detour " + d.DetourNumber + " leg " + d.Leg + " " + d.LegSide
                                                 + (d.HeadOn ? ", head-on" : "") + ", raged " + d.Raged + ", detours done " + d.DetoursDone + ")"));
        }

        Assert.That(plain.ToString(), Does.Not.Contain("head-on"));
        Assert.That(head.ToString(), Does.Contain(", head-on, raged "));
        Assert.That(plain.ToString(), Does.Contain(" (stage ").And.Contain(", detour ").And.Contain(" leg ").And.Contain(", raged ").And.Contain(", detours done ").And.EndsWith(")"));
    }

    [Test]
    public void RecoveryDecision_RightFirst_FollowsTheFailedCycles()
    {
        var even = new RecoveryPipeline();
        even.BeginCycle(2);
        var odd = new RecoveryPipeline();
        odd.BeginCycle(1);
        Assert.That(even.Evaluate(RIn(0f, StuckRules.StuckTrigger)).Value.RightFirst, Is.True);
        Assert.That(odd.Evaluate(RIn(0f, StuckRules.StuckTrigger)).Value.RightFirst, Is.False);
    }

    // ---------------------------------------------------------------- exact boundaries

    [Test]
    public void CounterRules_Boundaries()
    {
        Assert.That(CounterRules.Respond(false, 10f, 10f, 0f, false, 60f), Is.EqualTo(CounterResponse.Slam), "now == ignoreUntil is no longer inside the window");
        Assert.That(CounterRules.Respond(false, 9.9f, 10f, 0f, true, 60f), Is.EqualTo(CounterResponse.Ignore));
        Assert.That(CounterRules.Respond(false, 70f, 0f, 10f, true, 60f), Is.EqualTo(CounterResponse.Chase), "exactly calmSeconds is not calm yet");
        Assert.That(CounterRules.Respond(false, 70.5f, 0f, 10f, true, 60f), Is.EqualTo(CounterResponse.Slam));
        Assert.That(CounterRules.Respond(false, 55f, 0f, 10f, true, 60f), Is.EqualTo(CounterResponse.Chase), "45 s since the last response is not calm");
    }

    [Test]
    public void GuardRules_Eligible_Boundaries()
    {
        // exactly at the guard and keep range, owner far, creature outside the defend range: not eligible
        Assert.That(GuardRules.Eligible(true, true, 25f, 400f, 25f, 25f, 4f), Is.False);
        // exactly at the guard and keep range and inside the defend range
        Assert.That(GuardRules.Eligible(true, true, 25f, 400f, 25f, 25f, 25f), Is.True);
        // beyond the keep range only / beyond the guard range only
        Assert.That(GuardRules.Eligible(true, true, 50f, 0f, 100f, 25f, 100f), Is.False);
        Assert.That(GuardRules.Eligible(true, true, 50f, 0f, 25f, 100f, 100f), Is.False);
        // the owner's side of the range: the creature is out of the troll's range, exactly at the owner's
        Assert.That(GuardRules.Eligible(true, true, 30f, 16f, 100f, 100f, 16f), Is.True);
        Assert.That(GuardRules.Eligible(true, true, 30f, 16.5f, 100f, 100f, 16f), Is.False);
        Assert.That(GuardRules.Eligible(true, true, 16f, 400f, 100f, 100f, 16f), Is.True, "exactly at the troll's own defend range");
    }

    [Test]
    public void GuardRules_Nearest_FirstOnTie_AndFirstIsKept()
    {
        Assert.That(GuardRules.Nearest(new[] { 1f, 5f }), Is.EqualTo(0));
        Assert.That(GuardRules.Nearest(new[] { 5f, 1f }), Is.EqualTo(1));
        Assert.That(GuardRules.Nearest(new[] { 2f, 2f }), Is.EqualTo(0));
        Assert.That(GuardRules.Nearest(new[] { 3f, 4f, 3f }), Is.EqualTo(0));
    }

    [Test]
    public void HomeRules_Holds_Boundaries()
    {
        Assert.That(HomeRules.Holds(0f, false, false, 0f, true, 0f, 1f), Is.False, "range 0 = off");
        Assert.That(HomeRules.Holds(10f, false, false, 10f, true, 0f, 1f), Is.True, "exactly at the range still holds");
        Assert.That(HomeRules.Holds(10f, false, false, 10.1f, true, 0f, 1f), Is.False);
        Assert.That(HomeRules.Holds(10f, false, false, 5f, true, 1f, 1f), Is.True, "exactly the grace time still holds");
        Assert.That(HomeRules.Holds(10f, false, false, 5f, true, 1.1f, 1f), Is.False);
        Assert.That(HomeRules.Holds(10f, false, false, 5f, false, 0f, 1f), Is.False);
        Assert.That(HomeRules.Holds(10f, true, false, 5f, true, 0f, 1f), Is.False);
        Assert.That(HomeRules.Holds(10f, false, true, 5f, true, 0f, 1f), Is.False);
    }

    // ---------------------------------------------------------------- TargetRules

    [Test]
    public void TargetRules_LegSeconds_GrowsWithTheLength()
    {
        Assert.That(TargetRules.LegSeconds(10f), Is.EqualTo(10f / TargetRules.SlowWalkMetresPerSecond + TargetRules.LegMarginSeconds).Within(1e-4f));
        Assert.That(TargetRules.LegSeconds(0f), Is.EqualTo(TargetRules.MinLegSeconds));
        Assert.That(TargetRules.LegSeconds(-5f), Is.EqualTo(TargetRules.MinLegSeconds));
        Assert.That(TargetRules.LegSeconds(0.5f), Is.EqualTo(TargetRules.MinLegSeconds), "short legs get the minimum");
    }

    [Test]
    public void TargetRules_Blocked_Boundaries()
    {
        var me = new GroundPoint(0f, 0f);
        var goal = new GroundPoint(10f, 0f);
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(0f, 0f), 20f, 3f), Is.False, "AT me is not ahead of me");
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(5f, 0f), 5f, 3f), Is.True, "exactly `ahead` metres along still blocks");
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(5.1f, 0f), 5f, 3f), Is.False);
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(9.5f, 0f), 20f, 3f), Is.False, "0.5 m before the goal counts as at the goal");
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(9.4f, 0f), 20f, 3f), Is.True);
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(5f, 3f), 20f, 3f), Is.True, "exactly the half width beside the line still blocks");
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(5f, 3.1f), 20f, 3f), Is.False);
        Assert.That(TargetRules.Blocked(me, new GroundPoint(0.2f, 0f), new GroundPoint(0.1f, 0f), 20f, 3f), Is.False, "goal closer than 0.5 m");
        Assert.That(TargetRules.Blocked(me, goal, new GroundPoint(5f, -3f), 20f, 3f), Is.True, "the other side of the line too");
    }

    /// The side step and the waypoint, from the cross product, in doubles (an independent reference for the float code).
    private static void Reference(double mx, double mz, double gx, double gz, double ox, double oz, double side, out double sx, out double sz, out double wx, out double wz)
    {
        double dx = gx - mx, dz = gz - mz;
        double len = Math.Sqrt(dx * dx + dz * dz);
        dx /= len;
        dz /= len;
        double lateral = dx * (oz - mz) - dz * (ox - mx);
        double sgn = lateral > 0 ? -1 : 1;
        sx = mx - dz * sgn * side;
        sz = mz + dx * sgn * side;
        wx = ox - dz * sgn * side + dx;
        wz = oz + dx * sgn * side + dz;
    }

    [Test]
    public void TargetRules_Waypoint_And_SideStep_MatchTheGeometry()
    {
        var rnd = new Random(20261115);
        for (int n = 0; n < 300; n++)
        {
            double mx = rnd.Next(-30, 30), mz = rnd.Next(-30, 30), gx = rnd.Next(-30, 30), gz = rnd.Next(-30, 30), ox = rnd.NextDouble() * 60 - 30, oz = rnd.NextDouble() * 60 - 30;
            if (Math.Abs(gx - mx) + Math.Abs(gz - mz) < 1) continue;
            double side = 1 + rnd.Next(0, 6);
            Reference(mx, mz, gx, gz, ox, oz, side, out double sx, out double sz, out double wx, out double wz);
            GroundPoint w = TargetRules.Waypoint(new GroundPoint((float)mx, (float)mz), new GroundPoint((float)gx, (float)gz), new GroundPoint((float)ox, (float)oz), (float)side);
            GroundPoint s = TargetRules.SideStep(new GroundPoint((float)mx, (float)mz), new GroundPoint((float)gx, (float)gz), new GroundPoint((float)ox, (float)oz), (float)side);
            Assert.That(w.X, Is.EqualTo(wx).Within(1e-2), "waypoint x, case " + n);
            Assert.That(w.Z, Is.EqualTo(wz).Within(1e-2), "waypoint z, case " + n);
            Assert.That(s.X, Is.EqualTo(sx).Within(1e-2), "side step x, case " + n);
            Assert.That(s.Z, Is.EqualTo(sz).Within(1e-2), "side step z, case " + n);
        }
    }

    [Test]
    public void TargetRules_Waypoint_KnownCases()
    {
        // me (1,2) -> goal (7,10): direction (0.6, 0.8); the friend at (5,6) is left of the line, at (3,8) right of it
        GroundPoint a = TargetRules.Waypoint(new GroundPoint(1f, 2f), new GroundPoint(7f, 10f), new GroundPoint(5f, 6f), 4f);
        Assert.That(a.X, Is.EqualTo(2.4f).Within(1e-3f));
        Assert.That(a.Z, Is.EqualTo(9.2f).Within(1e-3f));
        GroundPoint b = TargetRules.Waypoint(new GroundPoint(1f, 2f), new GroundPoint(7f, 10f), new GroundPoint(3f, 8f), 4f);
        Assert.That(b.X, Is.EqualTo(6.8f).Within(1e-3f));
        Assert.That(b.Z, Is.EqualTo(6.4f).Within(1e-3f));
    }

    [Test]
    public void TargetRules_NoDirection_Waypoint_IsTheOther_SideStep_IsMe()
    {
        var me = new GroundPoint(3f, 4f);
        var other = new GroundPoint(8f, 9f);
        GroundPoint w = TargetRules.Waypoint(me, me, other, 4f);
        GroundPoint s = TargetRules.SideStep(me, me, other, 4f);
        Assert.That(w.X, Is.EqualTo(8f));
        Assert.That(w.Z, Is.EqualTo(9f));
        Assert.That(s.X, Is.EqualTo(3f));
        Assert.That(s.Z, Is.EqualTo(4f));
    }

    [Test]
    public void TargetRules_HeldByOther_ExactlyTheTtlIsOver()
    {
        Assert.That(TargetRules.HeldByOther(5, 10f, 6, 15.9f, 6f), Is.True);
        Assert.That(TargetRules.HeldByOther(5, 10f, 6, 16f, 6f), Is.False, "exactly ttl after the refresh the reservation has run out");
    }

    // ---------------------------------------------------------------- RecoveryBook

    [Test]
    public void RecoveryBook_QueriesDoNotCreateEntries()
    {
        var book = new RecoveryBook();
        Assert.That(book.Failed("t", 1, 2), Is.EqualTo(0));
        Assert.That(book.IsBlacklisted("t", 1, 2, 0f), Is.False);
        book.Recovered("t", 1, 2);
        Assert.That(book.Count, Is.EqualTo(0));
    }

    [Test]
    public void RecoveryBook_KeysAreTrollAndBothIds()
    {
        var book = new RecoveryBook();
        book.CycleFailed("a", 1, 2, 0f, false);
        Assert.That(book.Failed("a", 1, 2), Is.EqualTo(1));
        Assert.That(book.Failed("b", 1, 2), Is.EqualTo(0), "another troll");
        Assert.That(book.Failed("a", 9, 2), Is.EqualTo(0), "another first id");
        Assert.That(book.Failed("a", 1, 9), Is.EqualTo(0), "another second id");
        Assert.That(book.Failed(null, 1, 2), Is.EqualTo(0));
        book.CycleFailed(null, 5, 6, 0f, false);
        Assert.That(book.Failed("", 5, 6), Is.EqualTo(1), "a null troll name is the empty name");
    }

    [Test]
    public void RecoveryBook_ForgetsEverythingOnlyAbove2000Entries()
    {
        var book = new RecoveryBook();
        for (int i = 0; i < 2001; i++)
        {
            book.CycleFailed("t", i, 0, 0f, false);
        }

        Assert.That(book.Count, Is.EqualTo(2001), "2000 entries are still kept, the 2001st is added");
        book.CycleFailed("t", 5000, 0, 0f, false);
        Assert.That(book.Count, Is.EqualTo(1), "above 2000 the book starts again");
    }

    [Test]
    public void RecoveryBook_Prune_DropsABlacklistExactlyWhenItEnds()
    {
        var book = new RecoveryBook();
        Assert.That(book.CycleFailed("t", 1, 2, 10f, true), Is.False);
        Assert.That(book.CycleFailed("t", 1, 2, 10f, true), Is.True);
        float until = 10f + StuckRules.BlacklistSeconds;
        Assert.That(book.IsBlacklisted("t", 1, 2, until - 0.1f), Is.True);
        Assert.That(book.IsBlacklisted("t", 1, 2, until), Is.False);
        Assert.That(book.Prune(until - 0.1f), Is.EqualTo(0), "still blacklisted");
        Assert.That(book.Prune(until), Is.EqualTo(1), "the blacklist ends exactly at `until`");
        Assert.That(book.Count, Is.EqualTo(0));
    }
}


