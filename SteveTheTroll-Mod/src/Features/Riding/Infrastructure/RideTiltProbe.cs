using SteveTheTroll.Features.Riding.Domain;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-217 R3, lab only: does the rider LEAN with the seat during an attack? Measured on the two real transforms (the rider's root and the seat anchor on the bone), each
///     against its own pose at the first sampled frame (the standing troll), in the troll's frame - not against HalterVisual's own numbers (a check whose two sides come from the code
///     under test cannot fail: MISTAKES 2026-11-10, rideGap).
/// </summary>
internal sealed class RideTiltProbe
{
    private Quaternion _riderRef = Quaternion.identity, _anchorRef = Quaternion.identity;
    private bool _hasRef, _wasAttacking;
    private float _attackStart;

    /// The biggest angle (degrees) between the rider's lean and the seat's lean in attack frames, after the lean's fade-in.
    internal float GapDegMax { get; private set; }

    /// The biggest lean (degrees) of the seat anchor itself in attack frames (0 = the bone never leaned, so the gap proves nothing).
    internal float AnchorTiltDegMax { get; private set; }

    /// User 2026-11-10 10:37 ("the player's ass is not glued to the chair ... the chair follows the troll but the player goes too much forward and slides into the troll"):
    /// the biggest angle (degrees) between the rider's lean and the seat's lean in ANY sampled frame - standing, idle sway, walking, attacking.
    internal float GapDegAllMax { get; private set; }

    /// The biggest lean (degrees) of the rider in attack frames.
    internal float RiderTiltDegMax { get; private set; }

    internal float Reset()
    {
        GapDegMax = 0f;
        GapDegAllMax = 0f;
        AnchorTiltDegMax = 0f;
        RiderTiltDegMax = 0f;
        _hasRef = false;
        _wasAttacking = false;
        return GapDegMax;
    }

    /// One sampled frame (after the rider was pinned for the frame). Expected outcome: the maxima grow only while `attacking`.
    internal void Frame(Transform troll, Transform rider, Transform anchor, bool attacking, float now)
    {
        Quaternion toTroll = Quaternion.Inverse(troll.rotation);
        Quaternion riderNow = toTroll * rider.rotation, anchorNow = toTroll * anchor.rotation;
        // lab run a26-t2 (gap 9.4 deg at a seat lean of 95 deg; a26-t1: 4.8 deg): the reference poses were taken again at the start of EVERY swing, in the middle of the
        // recovery from the swing before - the rider was then compared with a pose the seat only passes through. ONE reference per measurement: the first sampled frame
        // (the troll stands, nothing deliberate plays). What is left in the gap is the idle sway of the spine between that frame and the swing, which the rider does not
        // follow by design (SeatTilt)
        if (!_hasRef)
        {
            _hasRef = true;
            _riderRef = riderNow;
            _anchorRef = anchorNow;
        }

        if (attacking && !_wasAttacking)
        {
            _attackStart = now;
        }

        _wasAttacking = attacking;
        Quaternion riderLean = riderNow * Quaternion.Inverse(_riderRef), anchorLean = anchorNow * Quaternion.Inverse(_anchorRef);
        GapDegAllMax = Mathf.Max(GapDegAllMax, Quaternion.Angle(riderLean, anchorLean));
        if (!attacking)
        {
            return;
        }

        AnchorTiltDegMax = Mathf.Max(AnchorTiltDegMax, Quaternion.Angle(Quaternion.identity, anchorLean));
        RiderTiltDegMax = Mathf.Max(RiderTiltDegMax, Quaternion.Angle(Quaternion.identity, riderLean));
        if (now - _attackStart >= SeatTilt.BlendSeconds * 2f)
        {
            GapDegMax = Mathf.Max(GapDegMax, Quaternion.Angle(riderLean, anchorLean));
        }
    }
}
