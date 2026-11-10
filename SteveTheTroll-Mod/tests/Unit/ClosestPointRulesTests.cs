using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Movement.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>T-217 O3: Collider.ClosestPoint is only asked for the four shapes Unity answers (1,019 warnings in the manual test 20261110-015153).</summary>
[TestFixture]
public class ClosestPointRulesTests
{
    [TestCase(ColliderShape.Box)]
    [TestCase(ColliderShape.Sphere)]
    [TestCase(ColliderShape.Capsule)]
    [TestCase(ColliderShape.ConvexMesh)]
    public void SupportedShapes_AreAskedExactly(ColliderShape shape)
    {
        Assert.That(ClosestPointRules.ExactSupported(shape), Is.True);
    }

    [TestCase(ColliderShape.ConcaveMesh)]
    [TestCase(ColliderShape.Other)]
    public void UnsupportedShapes_UseTheBounds(ColliderShape shape)
    {
        Assert.That(ClosestPointRules.ExactSupported(shape), Is.False, "a rock's base_model is a non-convex mesh: Unity warns and returns the asked point");
    }
}
