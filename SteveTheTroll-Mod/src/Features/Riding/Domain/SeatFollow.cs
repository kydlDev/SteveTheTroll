using System;

namespace SteveTheTroll.Features.Riding.Domain;

/// <summary>
///     D-202 (lab ride-shake-still): how fast the rider's seat follows the animated bone it hangs on. A standing troll plays random idle clips
///     (e.g. "Idle_oooh a piece of Candy") whose spine moves fast and far, so a seat that follows the bone exactly threw the rider up to 22 cm in one frame.
///     A standing troll gets a low-pass (the seat eases towards the bone), a walking troll is followed exactly (the walk bob must not lag behind the rider).
/// </summary>
internal static class SeatFollow
{
    /// Time constant of the low-pass while the troll stands (seconds): a jump of the bone shows as about dt/this of its size in the rider per frame.
    /// D-207: 0.2 -> 0.35. With 0.2 the lab's biggest frame step of the standing rider was 9.9 .. 11.0 cm (limit 10 cm) - the Candy idle moves the seat bone ~45 cm within
    /// a fraction of a second and the lab's vsync gives frames of 16 and 31 ms, so a 31 ms frame took 0.144 of that gap in one go. With 0.35 the same frame takes 0.085 (about 6 cm).
    internal const float StillSeconds = 0.35f;

    /// D-208: the fastest the seat of a standing troll may travel relative to the troll (m/s). The low-pass above scales with the gap to the bone, and the Candy idle opens a gap of
    /// well over a metre, so in the 40x repeat on the D-207 build one first run showed 13 cm in a frame (steady 31 ms frames; limit 10 cm). A speed cap makes the frame step
    /// independent of the gap: cap * dt (3.7 cm at 31 ms, still under 10 cm up to an 80 ms hitch).
    internal const float MaxStillSpeed = 1.2f;

    /// D-214 (user 2026-11-10, photos of the ridden troll attacking): how long (s) after the last frame of an attack or a stagger the seat still follows the bone exactly. The game itself
    /// pins the rider to the saddle point exactly (Player.UpdateAttach) and the Lox's attack animation moves the rider with it; the easing above is only for random idle clips. During
    /// a swing the standing troll's spine leans and the basket (on the bone) goes with it - an eased seat stayed behind and the rider clipped through the basket.
    internal const float DeliberateHoldSeconds = 0.5f;

    /// From this ground speed (m/s) on, the seat follows the bone exactly; below it the share fades linearly with the speed (no jump when the troll starts or stops).
    internal const float ExactFromSpeed = 0.5f;

    /// Share (0..1) of the way from the current seat position to the bone's position that the seat covers this frame. Expected outcomes: 1 for a walking troll
    /// (exact) or a first frame (dt not positive); 1 - e^(-dt/StillSeconds) for a standing one, so the result does not depend on the frame rate.
    internal static float Share(float speed, float dt)
    {
        if (dt <= 0f)
        {
            return 1f;
        }

        float walking = Math.Min(1f, Math.Max(0f, speed / ExactFromSpeed));
        float eased = 1f - (float)Math.Exp(-dt / StillSeconds);
        return eased + (1f - eased) * walking;
    }

    /// D-214: the share while the troll plays a deliberate animation (an attack or a stagger): 1 = exact, whatever the speed. Expected outcome: 1 when deliberate, else Share(speed, dt).
    internal static float Share(float speed, float dt, bool deliberate)
    {
        return deliberate ? 1f : Share(speed, dt);
    }

    /// Longest distance (m) the seat may move towards the bone this frame. Expected outcomes: no limit (infinity) for a walking troll or a first frame (dt not positive);
    /// MaxStillSpeed * dt for a standing one, widening smoothly as the speed approaches ExactFromSpeed so it ends in exact following without a jump.
    internal static float MaxStep(float speed, float dt)
    {
        if (dt <= 0f)
        {
            return float.PositiveInfinity;
        }

        float walking = Math.Min(1f, Math.Max(0f, speed / ExactFromSpeed));
        return walking >= 1f ? float.PositiveInfinity : MaxStillSpeed * dt / (1f - walking);
    }

    /// D-214: the step limit while the troll plays a deliberate animation: none. Expected outcome: infinity when deliberate, else MaxStep(speed, dt).
    internal static float MaxStep(float speed, float dt, bool deliberate)
    {
        return deliberate ? float.PositiveInfinity : MaxStep(speed, dt);
    }
}
