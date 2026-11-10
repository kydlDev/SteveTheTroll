using KydlCore.Common;
using KydlCore.Features.Diagnostics.Contracts;
using SteveTheTroll.Features.Riding.Domain;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-217 R2 / R5: watches the ROOT of the ridden troll while its rider asks for nothing (RideStill decides). A troll that leaves the place where it settled is a
///     warning with a flight dump - the saddle's old speed sample could not see a driver that writes transform.position directly. The lab reads the numbers
///     (stt.rideDrift, stt.rideSpeedXZ, stt.rideStill).
/// </summary>
internal sealed class RideStillWatch
{
    private Vector3 _settledAt;
    private float _expectedAt;
    private bool _started, _hasSettled, _dumped;

    /// The biggest flat distance (cm) of the troll's root from its settled place in any still period since Reset.
    internal float DriftCm { get; private set; }

    /// The troll's flat (XZ) speed at the last Tick, cm/s (the total speed includes falling).
    internal float SpeedXZCm { get; private set; }

    internal StillVerdict Last { get; private set; } = StillVerdict.Driven;

    /// Lab: forget the biggest drift (a new measurement starts).
    internal float Reset()
    {
        DriftCm = 0f;
        _hasSettled = false;
        _dumped = false;
        return DriftCm;
    }

    /// One frame. `movementExpected` = the rider drives (or autorun), or the troll attacks / staggers / is off the ground / swims. Expected outcome: the verdict of this frame;
    /// Drifting is logged once per still period as a warning with a flight dump.
    internal StillVerdict Tick(Character troll, bool movementExpected, float now)
    {
        Vector3 pos = troll.transform.position;
        Vector3 v = troll.GetVelocity();
        SpeedXZCm = new Vector2(v.x, v.z).magnitude * 100f;
        // lab run a26-t1, ride-busy-mount: a troll mounted in the middle of its walk (3.6 m/s) was judged at once, its 56 cm roll-out counted as drift. The mount itself is
        // "movement expected": the first frame starts the settle time like a released key does
        if (!_started)
        {
            _started = true;
            movementExpected = true;
        }

        if (movementExpected)
        {
            _expectedAt = now;
            _hasSettled = false;
            _dumped = false;
        }

        float drift = 0f;
        if (!movementExpected && now - _expectedAt >= RideStill.SettleSeconds)
        {
            if (!_hasSettled)
            {
                _settledAt = pos;
                _hasSettled = true;
            }

            drift = new Vector2(pos.x - _settledAt.x, pos.z - _settledAt.z).magnitude;
            DriftCm = Mathf.Max(DriftCm, drift * 100f);
        }

        StillVerdict verdict = RideStill.Judge(movementExpected, now - _expectedAt, drift);
        if (verdict != Last)
        {
            ModLog.State("RideStillWatch " + troll.name, Last.ToString(), verdict.ToString(), $"drift {drift:F2} m from the settled place (limit {RideStill.DriftLimitMetres:F1} m), flat speed {SpeedXZCm:F0} cm/s, {now - _expectedAt:F1} s since movement was last expected");
            Last = verdict;
        }

        if (verdict == StillVerdict.Drifting && !_dumped)
        {
            _dumped = true;
            string what = $"RideStillWatch: the ridden {troll.name} moved {drift:F2} m from {_settledAt:F1} to {pos:F1} while its rider asked for nothing (limit {RideStill.DriftLimitMetres:F1} m, flat speed {SpeedXZCm:F0} cm/s, move dir {troll.GetMoveDir():F2}) - something other than the saddle moves it";
            ModLog.Warn(what);
            FlightRecorder.Dump(what);
        }

        return verdict;
    }
}
