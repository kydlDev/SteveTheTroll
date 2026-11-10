using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Combat.Domain;

/// <summary>
///     D-171 (P5): D-097 (user: "set no hit zone 15m from bed") - the flat circle around the bed where own tamed
///     trolls never swing. Game-free.
/// </summary>
internal static class NoHitRules
{
    /// (x, z) lies within `radius` m of the bed (bx, bz); radius 0 = off. Expected outcome: false when off.
    internal static bool Inside(float bx, float bz, float x, float z, float radius)
    {
        if (radius <= 0f)
        {
            return radius > 0f;
        }

        float dx = x - bx, dz = z - bz;
        return dx * dx + dz * dz <= radius * radius;
    }

    /// D-214: a hub is a building (HubData.OuterRadius 15.5 m around its middle): its no-hit circle reaches `ring` m beyond that edge, so a troll keeps `ring` m from the WALLS, not from the middle
    /// (user 2026-11-10: Steve 3 farmed logs 14 - 16 m from the middle of HUB 1, at its very edge). Expected outcome: ring + hubRadius; 0 (off) when the ring is 0.
    internal static float HubCircleRadius(float ring, float hubRadius)
    {
        return ring <= 0f ? 0f : ring + hubRadius;
    }

    /// D-214 (user 2026-11-10: "isn't he supposed to not attack within 15 m of the hub?"): (x, z) lies inside ANY of the circles (the bed's and every Troll Hub's, each with its own radius).
    /// Expected outcome: false for no circle (or null) and for a circle of radius 0 (off); true inside any one of several circles.
    internal static bool InsideAny(IReadOnlyList<NoHitCircle> circles, float x, float z)
    {
        if (circles == null)
        {
            return false;
        }

        for (int i = 0; i < circles.Count; i++)
        {
            if (Inside(circles[i].X, circles[i].Z, x, z, circles[i].Radius))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>D-214: one no-hit circle on the ground plane (the bed or a Troll Hub): its middle (world X, Z) and its radius in metres. Game-free.</summary>
internal readonly struct NoHitCircle
{
    internal NoHitCircle(float x, float z, float radius)
    {
        X = x;
        Z = z;
        Radius = radius;
    }

    internal float X { get; }
    internal float Z { get; }
    internal float Radius { get; }
}
