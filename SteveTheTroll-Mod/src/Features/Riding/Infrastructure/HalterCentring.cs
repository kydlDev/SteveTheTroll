using KydlCore.Common;
using SteveTheTroll.Features.Riding.Domain;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     2026-11-15 (user: "force the player's position always to be centered on the chair at the moment of mounting", "the back of the player goes through the chair"):
///     after the sitting pose has blended in, the rider's hips are measured against the chair (Seat / Cantle / Pommel) and the seat point is moved once so the hips sit on
///     the chair's middle line and in front of the chair back; the correction (FixX / FixZ, troll axes, m) is then frozen for the ride.
///     2026-11-17 (user): the back is GLUED a little in front of the chair back's inside face (ChairFitRules.ForwardMarginM, corrected both ways), and a finished
///     centring checks the back every 0.25 s and runs again when the back is 2 cm or more inside the chair back (the sitting animation moves the hips afterwards).
/// </summary>
internal sealed class HalterCentring
{
    private const float CentreFrom = 0.3f, CentreUntil = 2.5f, HipsDepth = 0.17f;
    private readonly Transform _troll, _rider, _body, _seatAnchor;
    private Transform _hips;
    private int _settled;
    private float _runStart = -1f, _nextWatch;

    internal HalterCentring(Transform troll, Transform rider, Transform body, Transform seatAnchor)
    {
        _troll = troll;
        _rider = rider;
        _body = body;
        _seatAnchor = seatAnchor;
    }

    /// The correction (troll axes, m) that centres the rider on the chair; zero until measured.
    internal float FixX { get; private set; }

    internal float FixZ { get; private set; }

    /// The centring is finished.
    internal bool Done { get; private set; }

    /// How often the centring ran again during this ride because the back went into the chair back (lab, diagnosis).
    internal int Recentres { get; private set; }

    /// Lab: the rider's hips off the chair's middle line, in cm (+ = to the troll's right); 0 when there is no hips bone yet.
    internal float SideCm()
    {
        if (!FindHips())
        {
            return 0f;
        }

        float ex, ez, into;
        Measure(out ex, out ez, out into);
        return ex * 100f;
    }

    /// Lab: how far (cm) the rider's back is INSIDE the chair back (behind its inside face); 0 when it is in front of it.
    internal float IntoBackCm()
    {
        if (!FindHips())
        {
            return 0f;
        }

        float ex, ez, into;
        Measure(out ex, out ez, out into);
        return into * 100f;
    }

    /// One frame of the correction (moves the seat point once, smoothly, until the hips are on the middle line and in front of the chair back). `since` = seconds since the
    /// first frame of the ride, negative = not started. Does nothing once Done.
    internal void Step(float since)
    {
        if (since < 0f || _rider == null || since < CentreFrom)
        {
            return;
        }

        if (Done)
        {
            Watch(since);
            return;
        }

        if (!FindHips())
        {
            Done = true;
            ModLog.Warn("HalterVisual: no Hips bone on the rider - not centred on the chair");
            return;
        }

        if (_runStart < 0f)
        {
            _runStart = since;
        }

        float ex, ez, into;
        Measure(out ex, out ez, out into);
        _settled = ChairFitRules.Settled(ex, ez) ? _settled + 1 : 0;
        FixX -= ex * 0.4f;
        FixZ -= ez * 0.4f;
        if (_settled >= 5 || since - _runStart > CentreUntil - CentreFrom)
        {
            Done = true;
            _nextWatch = since + ChairFitRules.WatchEverySeconds;
            string what = Recentres == 0 ? "centred on the chair" : $"centred again (re-run {Recentres})";
            ModLog.Info($"HalterVisual: rider {what} after {since - _runStart:F1} s - correction side {FixX * 100f:F1} cm, back/forward {FixZ * 100f:F1} cm (left over {ex * 100f:F1} / {ez * 100f:F1} cm, back {into * 100f:F1} cm inside the chair back, aim {ChairFitRules.ForwardMarginM * 100f:F1} cm in front of it)");
        }
    }

    // A finished centring: every WatchEverySeconds, the back 2 cm or more inside the chair back -> centre again (the sitting animation moves the hips after the centring).
    private void Watch(float since)
    {
        if (_hips == null || since < _nextWatch)
        {
            return;
        }

        _nextWatch = since + ChairFitRules.WatchEverySeconds;
        float ex, ez, into;
        Measure(out ex, out ez, out into);
        if (!ChairFitRules.NeedsRecentre(into))
        {
            return;
        }

        Recentres++;
        ModLog.Info($"HalterVisual: the rider's back is {into * 100f:F1} cm inside the chair back (limit {ChairFitRules.RecentreAtM * 100f:F0} cm from its inside face) - centring again (re-run {Recentres} this ride)");
        Done = false;
        _settled = 0;
        _runStart = -1f;
    }

    private bool FindHips()
    {
        if (_hips == null && _rider != null)
        {
            _hips = HalterVisual.FindDeep(_rider, "Hips");
        }

        return _hips != null;
    }

    // The extent (m) of a renderer along the troll's forward axis, and its centre, in the troll's frame.
    private bool PartZ(string part, out float centreZ, out float halfDepth, out float centreX)
    {
        Transform part_ = _body.Find(part);
        Renderer r = part_ != null ? part_.GetComponent<Renderer>() : null;
        centreZ = halfDepth = centreX = 0f;
        if (r == null)
        {
            return false;
        }

        Vector3 c = _troll.InverseTransformPoint(r.bounds.center);
        Vector3 e = r.bounds.extents, f = _troll.forward;
        centreZ = c.z;
        centreX = c.x;
        halfDepth = Mathf.Abs(f.x) * e.x + Mathf.Abs(f.y) * e.y + Mathf.Abs(f.z) * e.z;
        return true;
    }

    // How far (m, in the troll's axes) the rider's hips are from the chair's middle line (ex), the forward correction still needed to glue the back to its place
    // in front of the chair back (ez, both ways), and how far the back is INSIDE the chair back (into, 0 = in front of its inside face).
    private void Measure(out float ex, out float ez, out float into)
    {
        Vector3 h = _troll.InverseTransformPoint(_hips.position);
        float seatX = _seatAnchor != null ? _troll.InverseTransformPoint(_seatAnchor.position).x : h.x;
        float sz, sHalf, sx;
        if (PartZ("Seat", out sz, out sHalf, out sx))
        {
            seatX = sx;
        }

        ex = h.x - seatX;
        ez = 0f;
        into = 0f;
        float cz, cHalf, cx, pz, pHalf, px;
        if (PartZ("Cantle", out cz, out cHalf, out cx) && PartZ("Pommel", out pz, out pHalf, out px))
        {
            float f = pz >= cz ? 1f : -1f; // + = towards the troll's head (the cantle is the back of the chair, the pommel the front)
            float gap = f * (h.z - (cz + f * cHalf)); // hips centre to the chair back's INSIDE face
            into = ChairFitRules.IntoBack(gap, HipsDepth);
            ez = -f * ChairFitRules.ForwardError(gap, HipsDepth);
        }
    }
}
