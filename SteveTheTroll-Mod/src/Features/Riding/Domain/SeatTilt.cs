using System;

namespace SteveTheTroll.Features.Riding.Domain;

/// <summary>
///     T-217 R3 (manual test 20261110-015153, shot-002 / shot-003: the rider stayed upright while the basket leaned with the attacking troll's spine): how much of the
///     seat bone's LEAN the rider takes over. D-214 made the seat's position follow the bone exactly during an attack or a stagger; the rotation was still a fixed one
///     relative to the troll's root. The rider now leans with the bone while such a deliberate animation plays, measured against the bone's remembered rest pose, and
///     sits at the fixed rotation otherwise (walk and idle wobble of the spine would shake the rider's head and the camera).
/// </summary>
internal static class SeatTilt
{
    /// Seconds the lean takes to fade in when an attack / stagger starts and to fade out after it (no snap of the rider's head).
    internal const float BlendSeconds = 0.15f;

    /// Time constant (s) with which the remembered rest pose of the bone follows the bone while no deliberate animation plays (it is frozen while one plays).
    internal const float RestSeconds = 1f;

    /// The lean weight after this frame. Expected outcomes: moves linearly towards 1 while `deliberate`, towards 0 otherwise, by dt / BlendSeconds, never outside 0..1;
    /// dt not positive (first frame, paused game) = unchanged.
    internal static float Weight(float current, bool deliberate, float dt)
    {
        float now = Math.Min(1f, Math.Max(0f, current));
        if (dt <= 0f)
        {
            return now;
        }

        float step = dt / BlendSeconds;
        return deliberate ? Math.Min(1f, now + step) : Math.Max(0f, now - step);
    }

    /// Share (0..1) of the way the remembered rest pose moves to the bone's rotation this frame. Expected outcomes: 0 while a deliberate animation plays (the rest pose is
    /// what the lean is measured against, it must not follow the swing) and for dt not positive; 1 - e^(-dt/RestSeconds) otherwise (frame-rate independent).
    internal static float RestShare(bool deliberate, float dt)
    {
        if (deliberate || dt <= 0f)
        {
            return 0f;
        }

        return 1f - (float)Math.Exp(-dt / RestSeconds);
    }

    /// D-222 (user 2026-11-10 10:37: "the player's ass is not glued to the chair"): with the glue on, the chair's rest pose is learned for this long after the mount (eased
    /// with RestSeconds) and then kept for the whole ride - from then on the rider has ONE fixed pose relative to the chair.
    /// Lab run a26rep (rep01-ride-long, 21 degrees): 1.5 s of learning meant 1.5 s in which the rest pose moved under the rider, so he was NOT glued right after the mount.
    /// 0 = the chair's pose in the first frame of the ride is the rest pose: glued from the first frame on.
    internal const float GlueSettleSeconds = 0f;

    /// Share of the way the remembered rest pose moves to the bone's rotation this frame while the rider is glued to the chair. Expected outcomes: the eased share
    /// 1 - e^(-dt/RestSeconds) during the first GlueSettleSeconds of the ride (never during an attack / stagger), 0 afterwards and for dt not positive.
    internal static float GluedRestShare(float secondsSinceMount, bool deliberate, float dt)
    {
        return secondsSinceMount >= GlueSettleSeconds ? 0f : RestShare(deliberate, dt);
    }

    /// The share of the bone's lean applied to the rider: the fade weight times the tuning share (Tunable RideSeatTilt), both clamped to 0..1.
    /// Expected outcomes: 0 when either is 0 (rider at the fixed rotation), 1 when both are 1 (rider leans exactly like the seat).
    internal static float Applied(float weight, float tuningShare)
    {
        return Math.Min(1f, Math.Max(0f, weight)) * Math.Min(1f, Math.Max(0f, tuningShare));
    }
}
