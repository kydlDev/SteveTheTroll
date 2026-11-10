namespace SteveTheTroll.Features.Trolls.Movement.Domain;

/// <summary>The kinds of collider the mod asks for a closest point (T-217 O3).</summary>
public enum ColliderShape
{
    Box,
    Sphere,
    Capsule,
    ConvexMesh,
    ConcaveMesh,

    /// Terrain, wheel, character controller or anything else.
    Other
}

/// <summary>
///     T-217 O3 (manual test 20261110-015153: 1,019 Unity warnings "Physics.ClosestPoint can only be used with a BoxCollider, SphereCollider, CapsuleCollider and a convex
///     MeshCollider"): Unity answers Collider.ClosestPoint only for these four shapes; for every other one it logs that warning and returns the asked point itself,
///     so the troll "reached" a rock it was metres away from. The mod asks here first and uses the collider's bounds for the rest.
/// </summary>
internal static class ClosestPointRules
{
    /// Expected outcome: true for Box, Sphere, Capsule and ConvexMesh (Collider.ClosestPoint is exact), false for ConcaveMesh and Other (the bounds are used).
    internal static bool ExactSupported(ColliderShape shape)
    {
        return shape == ColliderShape.Box || shape == ColliderShape.Sphere || shape == ColliderShape.Capsule || shape == ColliderShape.ConvexMesh;
    }
}
