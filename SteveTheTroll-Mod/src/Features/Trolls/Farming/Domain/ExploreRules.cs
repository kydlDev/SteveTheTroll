using System;

namespace SteveTheTroll.Features.Trolls.Farming.Domain;

/// <summary>
///     D-189 (user 2026-11-06 14:10: when everything within the hub's farm range is farmed, the troll "continues walking forward, loads zones as it goes, and is stopped only at 950 m from the hub"):
///     the game-free choice of the next exploration point. The troll walks StepMetres along its heading; at the leash circle it turns (60 degrees, alternating sides) until every direction is spent.
/// </summary>
internal static class ExploreRules
{
    internal const float StepMetres = 150f, EdgeMargin = 25f, MinProgress = 40f, TurnDegrees = 60f;
    internal const int Candidates = 6;

    /// The next flat point to walk to from (px, pz) with a hub at (hx, hz): along `headingDeg` (0 = +x, 90 = +z), StepMetres long, pulled back onto the circle of leash - EdgeMargin m round the hub.
    /// A direction is skipped when the point is blocked (a place the troll gave up) or the walk gains less than MinProgress m. Tries the heading, then +60, -60, +120, -120, 180.
    /// Expected outcome: true with the point and the heading that worked; false when every direction is spent (the troll waits).
    internal static bool Next(float px, float pz, float hx, float hz, float headingDeg, float leash, Func<float, float, bool> blocked, out float tx, out float tz, out float usedHeading)
    {
        tx = px;
        tz = pz;
        usedHeading = headingDeg;
        float limit = leash - EdgeMargin;
        for (int i = 0; i < Candidates; i++)
        {
            float turn = (i + 1) / 2 * TurnDegrees * (i % 2 == 1 ? 1f : -1f);
            if (i == 0)
            {
                turn = 0f;
            }

            float h = headingDeg + turn;
            float rad = h * (float)Math.PI / 180f;
            float x = px + (float)Math.Cos(rad) * StepMetres, z = pz + (float)Math.Sin(rad) * StepMetres;
            float dx = x - hx, dz = z - hz;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist > limit && dist > 0f)
            {
                x = hx + dx / dist * limit;
                z = hz + dz / dist * limit;
            }

            float gx = x - px, gz = z - pz;
            if ((float)Math.Sqrt(gx * gx + gz * gz) < MinProgress || (blocked != null && blocked(x, z)))
            {
                continue;
            }

            tx = x;
            tz = z;
            usedHeading = h;
            return true;
        }

        return false;
    }

    /// The heading (degrees, 0 = +x, 90 = +z) a troll at (px, pz) starts exploring in: away from the hub; standing on the hub (within 5 m) it takes `forwardDeg`. Expected outcome: any angle.
    internal static float StartHeading(float px, float pz, float hx, float hz, float forwardDeg)
    {
        float dx = px - hx, dz = pz - hz;
        return dx * dx + dz * dz < 25f ? forwardDeg : (float)Math.Atan2(dz, dx) * 180f / (float)Math.PI;
    }
}
