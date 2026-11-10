using NUnit.Framework;
using SteveTheTroll.Features.Riding.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>T-217 R3: the rider leans with the seat while the troll attacks or staggers, and sits at the fixed rotation otherwise.</summary>
[TestFixture]
public class SeatTiltTests
{
    [Test]
    public void Weight_FadesInOverTheBlendTime_WhileDeliberate()
    {
        // 0.15 s blend: a 50 ms frame adds a third
        Assert.That(SeatTilt.Weight(0f, true, 0.05f), Is.EqualTo(1f / 3f).Within(0.001f));
        Assert.That(SeatTilt.Weight(0.9f, true, 0.05f), Is.EqualTo(1f), "never above 1");
    }

    [Test]
    public void Weight_FadesOut_WhenTheAnimationIsOver()
    {
        Assert.That(SeatTilt.Weight(1f, false, 0.05f), Is.EqualTo(2f / 3f).Within(0.001f));
        Assert.That(SeatTilt.Weight(0.1f, false, 0.05f), Is.EqualTo(0f), "never below 0");
    }

    [Test]
    public void Weight_ReachesFullLean_WithinTheBlendTime_AtAnyFrameRate()
    {
        foreach (float dt in new[] { 0.008f, 0.016f, 0.031f })
        {
            float w = 0f;
            for (float t = 0f; t < SeatTilt.BlendSeconds + dt; t += dt)
            {
                w = SeatTilt.Weight(w, true, dt);
            }

            Assert.That(w, Is.EqualTo(1f), $"frame time {dt}");
        }
    }

    [TestCase(0f)]
    [TestCase(-0.01f)]
    public void Weight_NoTimePassed_Unchanged(float dt)
    {
        Assert.That(SeatTilt.Weight(0.4f, true, dt), Is.EqualTo(0.4f));
        Assert.That(SeatTilt.Weight(0.4f, false, dt), Is.EqualTo(0.4f));
    }

    [Test]
    public void Weight_OutOfRangeInput_IsClampedFirst()
    {
        Assert.That(SeatTilt.Weight(5f, false, 0.015f), Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(SeatTilt.Weight(-5f, true, 0.015f), Is.EqualTo(0.1f).Within(0.001f));
    }

    [Test]
    public void RestPose_IsFrozen_WhileTheSwingPlays()
    {
        // the lean is measured against the rest pose: if it followed the swing, the lean would shrink to nothing
        Assert.That(SeatTilt.RestShare(true, 0.016f), Is.EqualTo(0f));
        Assert.That(SeatTilt.RestShare(true, 1f), Is.EqualTo(0f));
    }

    [Test]
    public void RestPose_FollowsTheBoneSlowly_Otherwise()
    {
        // 1 - e^(-0.016/1) = 0.0159
        Assert.That(SeatTilt.RestShare(false, 0.016f), Is.EqualTo(0.0159f).Within(0.0005f));
        Assert.That(SeatTilt.RestShare(false, 0f), Is.EqualTo(0f));
        Assert.That(SeatTilt.RestShare(false, 100f), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void GluedRider_LearnsTheChairsRestPose_OnlyRightAfterTheMount()
    {
        // D-222: after GlueSettleSeconds the rider keeps ONE fixed pose relative to the chair (a rest pose that kept following the sway would let the chair leave the rider again)
        // lab run a26rep: any learning time is time in which the rider is not glued - the settle time is 0, the first frame's pose is kept
        Assert.That(SeatTilt.GlueSettleSeconds, Is.EqualTo(0f));
        Assert.That(SeatTilt.GluedRestShare(0f, false, 0.016f), Is.EqualTo(0f));
        Assert.That(SeatTilt.GluedRestShare(-0.01f, false, 0.016f), Is.EqualTo(SeatTilt.RestShare(false, 0.016f)), "before the settle time has passed the eased share applies (only reachable when the constant is raised again)");
        Assert.That(SeatTilt.GluedRestShare(60f, false, 0.016f), Is.EqualTo(0f));
        Assert.That(SeatTilt.GluedRestShare(0.5f, true, 0.016f), Is.EqualTo(0f), "a swing right after the mount is not a rest pose");
        Assert.That(SeatTilt.GluedRestShare(0.5f, false, 0f), Is.EqualTo(0f));
    }

    [TestCase(1f, 1f, 1f)]
    [TestCase(0f, 1f, 0f)]
    [TestCase(1f, 0f, 0f)]
    [TestCase(0.5f, 0.5f, 0.25f)]
    [TestCase(2f, 3f, 1f)]
    [TestCase(-1f, 1f, 0f)]
    public void Applied_IsWeightTimesTuning_Clamped(float weight, float tuning, float expected)
    {
        Assert.That(SeatTilt.Applied(weight, tuning), Is.EqualTo(expected).Within(0.0001f));
    }
}
