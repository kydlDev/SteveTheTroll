using NUnit.Framework;
using SteveTheTroll.Features.Riding.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-202: how fast the rider's seat follows the animated bone (standing troll: eased, walking troll: exact).</summary>
[TestFixture]
public class SeatFollowTests
{
    [TestCase(0.5f, 0.016f)]
    [TestCase(1.5f, 0.016f)]
    [TestCase(4f, 0.031f)]
    public void WalkingTroll_IsFollowedExactly(float speed, float dt)
    { Assert.That(SeatFollow.Share(speed, dt), Is.EqualTo(1f)); }

    [Test]
    public void StandingTroll_Eases_ByTheTimeConstant()
    {
        // one 16 ms frame: 1 - e^(-0.016/0.35) = 0.0447
        Assert.That(SeatFollow.Share(0f, 0.016f), Is.EqualTo(0.0447f).Within(0.001f));
    }

    [Test]
    public void StandingTroll_AFrameHitch_PassesOnlyAFewPercentOfTheBoneJump()
    {
        // the 31 ms hitch frame of the lab: a 22 cm bone jump reaches the rider as at most 22 * 0.085 = 1.9 cm (the limit is 10 cm)
        float share = SeatFollow.Share(0f, 0.031f);
        Assert.That(share * 22f, Is.LessThan(2.5f));
    }

    [Test]
    public void StandingTroll_TheLabsBiggestGap_StaysUnderTheLimitEvenInAWorseHitch()
    {
        // D-207: the Candy idle opens a gap of about 55 cm between bone and seat; one frame may take at most 10 cm of it - also for a 50 ms frame (3 vsync frames)
        Assert.That(SeatFollow.Share(0f, 0.031f) * 55f, Is.LessThan(6f));
        Assert.That(SeatFollow.Share(0f, 0.050f) * 55f, Is.LessThan(8.5f));
    }

    [Test]
    public void SlowCreep_IsBetweenEasedAndExact()
    {
        float eased = SeatFollow.Share(0f, 0.016f);
        float creeping = SeatFollow.Share(0.25f, 0.016f);
        Assert.That(creeping, Is.GreaterThan(eased).And.LessThan(1f));
    }

    [Test]
    public void ShareGrowsWithSpeed_WithoutAJump()
    {
        float last = SeatFollow.Share(0f, 0.016f);
        for (float v = 0.05f; v <= 0.5f; v += 0.05f)
        {
            float s = SeatFollow.Share(v, 0.016f);
            Assert.That(s, Is.GreaterThanOrEqualTo(last));
            Assert.That(s - last, Is.LessThan(0.1f), "speed " + v);
            last = s;
        }
    }

    [TestCase(0f)]
    [TestCase(-0.01f)]
    public void NoTime_MeansNoEasing(float dt)
    { Assert.That(SeatFollow.Share(0f, dt), Is.EqualTo(1f)); }

    [TestCase(0.016f)]
    [TestCase(0.031f)]
    [TestCase(0.050f)]
    [TestCase(0.080f)]
    public void StandingTroll_FrameStep_IsCapped_WhateverTheGap(float dt)
    {
        // D-208: even a metre-wide gap (the Candy idle) moves the seat at most MaxStillSpeed * dt in one frame - under the lab's 10 cm up to an 80 ms hitch
        float step = SeatFollow.MaxStep(0f, dt);
        Assert.That(step, Is.EqualTo(SeatFollow.MaxStillSpeed * dt).Within(1e-6f));
        Assert.That(step * 100f, Is.LessThan(10f));
    }

    [TestCase(0.5f)]
    [TestCase(2f)]
    public void WalkingTroll_HasNoStepCap(float speed)
    { Assert.That(float.IsPositiveInfinity(SeatFollow.MaxStep(speed, 0.016f)), Is.True); }

    [TestCase(0f)]
    [TestCase(-0.01f)]
    public void NoTime_MeansNoStepCap(float dt)
    { Assert.That(float.IsPositiveInfinity(SeatFollow.MaxStep(0f, dt)), Is.True); }

    [Test]
    public void StepCap_WidensWithSpeed_WithoutAJump()
    {
        float last = SeatFollow.MaxStep(0f, 0.016f);
        for (float v = 0.05f; v < 0.45f; v += 0.05f)
        {
            float s = SeatFollow.MaxStep(v, 0.016f);
            Assert.That(s, Is.GreaterThanOrEqualTo(last));
            last = s;
        }

        // just under the exact-following speed the cap is already far above any real bone step (a walking bob is a few cm per frame)
        Assert.That(SeatFollow.MaxStep(0.49f, 0.016f), Is.GreaterThan(0.5f));
    }

    [Test]
    public void SameDistanceCovered_WhateverTheFrameRate()
    {
        // one 32 ms step equals two 16 ms steps (exponential low-pass)
        float one = SeatFollow.Share(0f, 0.032f);
        float half = SeatFollow.Share(0f, 0.016f);
        float two = 1f - (1f - half) * (1f - half);
        Assert.That(two, Is.EqualTo(one).Within(1e-4f));
    }

    // ---- D-214: an attack or a stagger is followed exactly (user 2026-11-10: the rider clipped through the basket while the troll swung) ---------------------------------------------

    [TestCase(0f, 0.016f)]
    [TestCase(0.2f, 0.031f)]
    [TestCase(0.49f, 0.083f)]
    public void DeliberateAnimation_IsFollowedExactly_WhateverTheSpeed(float speed, float dt)
    {
        Assert.That(SeatFollow.Share(speed, dt, true), Is.EqualTo(1f));
        Assert.That(SeatFollow.MaxStep(speed, dt, true), Is.EqualTo(float.PositiveInfinity));
    }

    [TestCase(0f, 0.016f)]
    [TestCase(0.3f, 0.031f)]
    public void NoDeliberateAnimation_KeepsTheIdleEasing(float speed, float dt)
    {
        Assert.That(SeatFollow.Share(speed, dt, false), Is.EqualTo(SeatFollow.Share(speed, dt)));
        Assert.That(SeatFollow.MaxStep(speed, dt, false), Is.EqualTo(SeatFollow.MaxStep(speed, dt)));
    }

    /// One seat axis in the troll's frame: the bone leans forward 1 m in 0.3 s (an attack swing), the seat follows with the same rule HalterVisual uses.
    private static float WorstGapDuringSwing(bool deliberate, float dt)
    {
        float bone = 0f, seat = 0f, worst = 0f;
        for (float t = 0f; t < 0.6f; t += dt)
        {
            bone = t < 0.3f ? t / 0.3f : 1f;
            float eased = seat + (bone - seat) * SeatFollow.Share(0f, dt, deliberate);
            float max = SeatFollow.MaxStep(0f, dt, deliberate);
            float delta = eased - seat;
            seat += System.Math.Abs(delta) <= max ? delta : System.Math.Sign(delta) * max;
            worst = System.Math.Max(worst, System.Math.Abs(bone - seat));
        }

        return worst;
    }

    [TestCase(0.008f)]
    [TestCase(0.016f)]
    [TestCase(0.031f)]
    public void SeatStaysOnTheBone_WhileTheTrollSwings(float dt)
    {
        // the rider must not lag behind the basket: no more than a millimetre (rounding)
        Assert.That(WorstGapDuringSwing(true, dt), Is.LessThan(0.001f));
    }

    [Test]
    public void IdleEasing_WouldHaveLeftTheSeatFarBehindTheSwing()
    {
        // the case the lab never measured: with the idle low-pass alone the seat trails the bone by well over 30 cm during a 1 m swing
        Assert.That(WorstGapDuringSwing(false, 0.016f), Is.GreaterThan(0.3f));
    }

    [Test]
    public void ExactFollowing_HoldsHalfASecondAfterTheAttack()
    {
        Assert.That(SeatFollow.DeliberateHoldSeconds, Is.EqualTo(0.5f).Within(1e-4f));
    }
}
