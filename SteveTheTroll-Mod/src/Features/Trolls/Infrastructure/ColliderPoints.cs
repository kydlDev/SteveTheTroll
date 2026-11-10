using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Movement.Domain;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Infrastructure;

/// <summary>
///     T-217 O3: the one place that asks a collider for its closest point. Shapes Unity cannot answer (a non-convex mesh such as a rock's base_model, terrain) get the
///     closest point of their bounds instead of Unity's warning and a wrong answer; each such collider is named once in the log with who asked.
/// </summary>
internal static class ColliderPoints
{
    private const int NamedLimit = 64;
    private static readonly HashSet<int> Named = new();

    /// How often the bounds were used instead of Collider.ClosestPoint this session (in-game test / lab).
    internal static int BoundsFallbacks { get; private set; }

    internal static ColliderShape ShapeOf(Collider c)
    {
        switch (c)
        {
            case BoxCollider:
                return ColliderShape.Box;
            case SphereCollider:
                return ColliderShape.Sphere;
            case CapsuleCollider:
                return ColliderShape.Capsule;
            case MeshCollider mesh:
                return mesh.convex ? ColliderShape.ConvexMesh : ColliderShape.ConcaveMesh;
            default:
                return ColliderShape.Other;
        }
    }

    /// Expected outcome: the point of `c` nearest to `from` - exact for the four shapes Unity supports, the nearest point of the collider's bounds otherwise.
    /// `asker` is a constant name ("TrollRecovery.Rage") for the one log line per collider.
    internal static Vector3 Closest(Collider c, Vector3 from, string asker)
    {
        ColliderShape shape = ShapeOf(c);
        if (ClosestPointRules.ExactSupported(shape))
        {
            return c.ClosestPoint(from);
        }

        BoundsFallbacks++;
        Vector3 onBounds = c.bounds.ClosestPoint(from);
        if (Named.Count < NamedLimit && Named.Add(c.GetInstanceID()))
        {
            ModLog.Verbose($"ColliderPoints: {c.name} ({shape}, bounds {c.bounds.size:F1} m) cannot give an exact closest point - its bounds are used ({Vector3.Distance(from, onBounds):F1} m from the asked point), asked by {asker}; {BoundsFallbacks} fallback(s) so far");
        }

        return onBounds;
    }
}
