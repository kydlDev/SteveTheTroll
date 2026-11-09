using System;

namespace SteveTheTroll.Features.Trolls.Farming.Domain;

/// <summary>
///     D-151 (user 2026-10-31): two trolls must not go for the same tree (one waiting behind the other), and a troll with
///     a friendly troll right in front of it steps around it
///     instead of pushing. The decisions are game-free (unit-tested); TrollFarmer reads the game and acts on them. Flat
///     distances in metres (x / z).
/// </summary>
internal static class TargetRules
{
    /// How far ahead (m) and how wide (m, each side of the walking line) a troll counts as "in front of me".
    // D-180 (fix12 farm-friend-detour: passing a standing troll the walker never got more than 1.4 m beside the line - the 2.8 m waypoint is inside the two bodies, so it pushed
    // the friend ahead of it up to the tree): the check starts 5 m ahead and the waypoint lies 4 m beside the friend (two troll bodies and a margin)
    // 2026-11-07 (full runs agent24-full1/full3, farm-friend-detour): a friend 2.6 m beside the line was judged "not in the way" and was shoved 2 m, then 10 m, before the tree - two
    // troll bodies need about 3 m between their centres, so the corridor is 3.2 m each side (the detour waypoint, 4 m beside the friend, stays clear of the corridor).
    internal const float Ahead = 5f, HalfWidth = 3.2f;

    /// How far beside the other troll (m) the detour point lies.
    internal const float Side = 4f;

    // 2026-11-07 (agent24-ffd4): a detour leg had a fixed 1.5 s, but the 4 m sideways leg takes a troll (2.2 m/s with the turn and the start) about 1.8 s, so it always ran out of time
    // 0.7 m short and the walker turned forward 3.3 m beside the friend - 0.1 m from touching it. A leg gets the time its length needs at a slow walk, plus a margin; it still ends
    // early on arrival.
    internal const float SlowWalkMetresPerSecond = 1.5f, LegMarginSeconds = 0.5f, MinLegSeconds = 1.5f;

    /// How long (s) a detour leg of `metres` may run before the next leg starts. Expected outcome: at least MinLegSeconds, growing with the length.
    internal static float LegSeconds(float metres)
    {
        return Math.Max(MinLegSeconds, Math.Max(0f, metres) / SlowWalkMetresPerSecond + LegMarginSeconds);
    }

    /// A reservation of a target is kept this long (s) after its holder last refreshed it.
    internal const float HoldSeconds = 6f;

    /// D-174 (spot-return, made to wait for the arrival): a troll walking back to its REMEMBERED farm spot (D-163, user: "remember the spot where they've farmed") does not
    /// take a source near the way - the near search took over 5 m from the depot and the spot was never reached. Within SpotSearchMetres of the spot (= TrollFarmer's arrival
    /// distance: run 172537 showed a 30 m zone still stopping the troll short of the spot) it searches as usual;
    /// a long-range search walk (no remembered spot) keeps taking the first source in range on the way. Expected outcome: true = run the near search now.
    internal const float SpotSearchMetres = 12f;

    /// Another troll stands on the line from me to the goal, in front of me, and not at the goal itself. Expected outcome: false when it is behind, beside, beyond or AT the goal.
    internal static bool Blocked(GroundPoint me, GroundPoint goal, GroundPoint other, float ahead, float halfWidth)
    {
        float mx = me.X, mz = me.Z, gx = goal.X, gz = goal.Z, ox = other.X, oz = other.Z;
        float dx = gx - mx, dz = gz - mz;
        float len = (float)Math.Sqrt(dx * dx + dz * dz);
        if (len < 0.5f)
        {
            return false;
        }

        dx /= len;
        dz /= len;
        float rx = ox - mx, rz = oz - mz;
        float along = rx * dx + rz * dz;
        if (along <= 0f || along > ahead || along >= len - 0.5f)
        {
            return false;
        }

        float lateral = dx * rz - dz * rx;
        return Math.Abs(lateral) <= halfWidth;
    }

    /// The point beside the other troll, on the side AWAY from the line it stands on (exactly on the line: the walker's right), and a metre past it.
    /// Two trolls meeting head on each pick their own right, so they pass each other.
    internal static GroundPoint Waypoint(GroundPoint me, GroundPoint goal, GroundPoint other, float side)
    {
        float mx = me.X, mz = me.Z, gx = goal.X, gz = goal.Z, ox = other.X, oz = other.Z;
        float dx = gx - mx, dz = gz - mz;
        float len = (float)Math.Sqrt(dx * dx + dz * dz);
        if (len < 0.0001f)
        {
            return other;
        }

        dx /= len;
        dz /= len;
        float lateral = dx * (oz - mz) - dz * (ox - mx);
        float sgn = lateral > 0f ? -1f : 1f;
        return new GroundPoint(ox + -dz * sgn * side + dx, oz + dx * sgn * side + dz);
    }

    /// 2026-11-05 (final gate runs, farm-friend-detour: from 3.2 m behind the friend the walker went diagonally to Waypoint and its path passed ~2.2 m from the friend's
    /// centre - inside two troll bodies - so every detour nudged the friend on towards the tree): the FIRST leg of a detour is a step straight sideways, side m off the
    /// walking line on the same side Waypoint uses; the second leg (Waypoint) then runs parallel to the line, side m from the friend.
    internal static GroundPoint SideStep(GroundPoint me, GroundPoint goal, GroundPoint other, float side)
    {
        float mx = me.X, mz = me.Z, gx = goal.X, gz = goal.Z, ox = other.X, oz = other.Z;
        float dx = gx - mx, dz = gz - mz;
        float len = (float)Math.Sqrt(dx * dx + dz * dz);
        if (len < 0.0001f)
        {
            return me;
        }

        dx /= len;
        dz /= len;
        float lateral = dx * (oz - mz) - dz * (ox - mx);
        float sgn = lateral > 0f ? -1f : 1f;
        return new GroundPoint(mx + -dz * sgn * side, mz + dx * sgn * side);
    }

    /// A target reserved by ANOTHER troll that refreshed it recently. holder 0 = nobody.
    internal static bool HeldByOther(int holder, float heldAt, int me, float now, float ttl)
    {
        return holder != 0 && holder != me && now - heldAt < ttl;
    }

    internal static bool NearSearchWhileTravelling(bool toRememberedSpot, float metresToSpot)
    {
        return !toRememberedSpot || metresToSpot <= SpotSearchMetres;
    }
}
