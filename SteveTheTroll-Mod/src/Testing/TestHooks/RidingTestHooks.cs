using KydlCore.Common;
using SteveTheTroll.Features.Riding.Domain;
using SteveTheTroll.Features.Riding.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>T-213: TestLab hooks of the rideable troll (suite ride-mount-dismount). They drive the REAL alt-key path (RideInteract.Prefix) and read the real session.</summary>
internal static class RidingTestHooks
{
    /// Puts the halter and seat into the player's inventory. Expected outcome: true when it is there afterwards.
    [TestHook("stt.rideGiveHalter")]
    private static bool GiveHalter()
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return false;
        }

        if (!RideItem.InInventory(p))
        {
            ItemDrop.ItemData item = p.GetInventory().AddItem(RideRecipe.PrefabName, 1, 1, 0, 0L, "", false);
            ModLog.Verbose($"RidingTestHooks: halter added {item != null}");
        }

        return RideItem.InInventory(p);
    }

    /// Does the player carry the halter and seat?
    [TestHook("stt.rideHasHalter")]
    private static bool HasHalter() => RideItem.InInventory(Player.m_localPlayer);

    /// The alternative interact key on this troll, through the real handler. Returns what it decided: "mount", "dismount", "blocked" or "game" (the game's own handling would run).
    [TestHook("stt.rideAlt")]
    private static string Alt(GameObject troll)
    {
        var t = troll.GetComponent<Tameable>();
        Player p = Player.m_localPlayer;
        if (t == null || p == null)
        {
            return "no troll / player";
        }

        MountVerdict v = RideInteract.VerdictFor(t, p);
        bool result = false;
        bool runGame = RideInteract.Prefix(t, p, false, true, ref result);
        return runGame ? $"game ({v})" : $"{(v == MountVerdict.Mount ? "mount" : v == MountVerdict.Dismount ? "dismount" : $"blocked ({v})")} result {result}";
    }

    /// The hover prompt verdict for this troll ("Mount", "NoHalter", ...).
    [TestHook("stt.rideVerdict")]
    private static string Verdict(GameObject troll)
    {
        var t = troll.GetComponent<Tameable>();
        return t != null && Player.m_localPlayer != null ? RideInteract.VerdictFor(t, Player.m_localPlayer).ToString() : "none";
    }

    /// Is this client riding that troll now, with the player attached?
    [TestHook("stt.rideRidden")]
    private static bool Ridden(GameObject troll)
    {
        Player p = Player.m_localPlayer;
        var c = troll.GetComponent<Character>();
        return p != null && c != null && RideSession.IsRidden(c) && p.IsAttached();
    }

    /// Centimetres between the player and the seat point (the player sits on the seat).
    [TestHook("stt.rideSeatDelta")]
    private static int SeatDelta(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        Player p = Player.m_localPlayer;
        return s == null || s.Seat == null || p == null ? -1 : Mathf.RoundToInt(Vector3.Distance(p.transform.position, s.Seat.position) * 100f);
    }

    /// How many halter parts are on the troll now (BodyParts + HeadParts children); 0 when none.
    [TestHook("stt.rideParts")]
    private static int Parts(GameObject troll)
    {
        int n = 0;
        foreach (Transform t in troll.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "BodyParts" || t.name == "HeadParts")
            {
                n += t.childCount;
            }
        }

        return n;
    }

    /// Both leashes have a line with 14 points that ends at a hand and starts at the troll (lengths in metres as text).
    [TestHook("stt.rideLeashes")]
    private static string Leashes(GameObject troll)
    {
        var sb = new System.Text.StringBuilder();
        int ok = 0;
        foreach (string n in new[] { "STT_LeashL", "STT_LeashR" })
        {
            Transform a = HalterVisual.FindDeep(troll.transform, n);
            LineRenderer lr = a != null ? a.GetComponent<LineRenderer>() : null;
            if (lr == null || lr.positionCount < 2)
            {
                sb.Append($"{n} missing; ");
                continue;
            }

            float len = Vector3.Distance(lr.GetPosition(0), lr.GetPosition(lr.positionCount - 1));
            sb.Append($"{n} {lr.positionCount} points, {len:F2} m; ");
            if (len > 0.3f && len < 8f)
            {
                ok++;
            }
        }

        return $"{ok} of 2 leashes ok: {sb}";
    }

    /// Starts (on = 1) or stops collecting how far the rider moves inside the troll's frame. Expected outcome: reads back with stt.rideShake.
    [TestHook("stt.rideSample")]
    private static string Sample(GameObject troll, int on)
    {
        var s = troll.GetComponent<TrollSaddle>();
        if (s == null)
        {
            return "no saddle";
        }

        s.Sample(on != 0);
        return $"sampling {on != 0}";
    }

    /// 2026-11-15: the rider has been centred on the chair (the correction is frozen). Expected outcome: true a moment after mounting.
    [TestHook("stt.rideCentred")]
    private static bool RideCentred(GameObject troll)
    {
        var v = troll.GetComponent<HalterVisual>();
        return v != null && v.Centred;
    }

    /// 2026-11-15 (user: the rider sat sideways in the chair): how far the rider's hips are off the chair's middle line, in cm (absolute). Expected outcome: a few cm at most.
    [TestHook("stt.rideSideCm")]
    private static int RideSideCm(GameObject troll)
    {
        var v = troll.GetComponent<HalterVisual>();
        return v == null ? -1 : Mathf.RoundToInt(Mathf.Abs(v.HipsSideCm()));
    }

    /// 2026-11-15 (user: the rider's back goes through the chair): how far (cm) the rider's hips still sit too far back, inside the chair back. Expected outcome: 0.
    [TestHook("stt.rideIntoBackCm")]
    private static int RideIntoBackCm(GameObject troll)
    {
        var v = troll.GetComponent<HalterVisual>();
        return v == null ? -1 : Mathf.RoundToInt(v.HipsIntoBackCm());
    }

    /// 2026-11-15 (user: the name tag flies around): how far (cm) the name tag's anchor (what the game's EnemyHud reads) is from straight above the ridden troll's root. Expected outcome: 0.
    [TestHook("stt.rideTagSideCm")]
    private static int RideTagSideCm(GameObject troll)
    {
        var c = troll.GetComponent<Character>();
        if (c == null)
        {
            return -1;
        }

        Vector3 top = c.GetTopPoint();
        Vector3 root = c.transform.position;
        return Mathf.RoundToInt(new Vector2(top.x - root.x, top.z - root.z).magnitude * 100f);
    }

    /// 2026-11-15: the height (cm) of the name tag's anchor above the troll's root. Expected outcome: the same all ride long.
    [TestHook("stt.rideTagHeightCm")]
    private static int RideTagHeightCm(GameObject troll)
    {
        var c = troll.GetComponent<Character>();
        return c == null ? -1 : Mathf.RoundToInt((c.GetTopPoint().y - c.transform.position.y) * 100f);
    }

    /// The biggest gap between the rider and the seat point since the sampling started, in cm (about 0 when the rider is pinned to the seat).
    [TestHook("stt.rideGap")]
    private static int Gap(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        if (s != null)
        {
            ModLog.Info($"RidingTestHooks: {s.SampleReport()}");
        }

        return s == null ? -1 : Mathf.RoundToInt(s.GapCm);
    }

    /// D-214 (user 2026-11-10: the rider clipped through the seat while the troll attacked, no test saw it): the biggest distance between the rider's seat point and the bone anchor it follows,
    /// since the sampling started, in cm. Expected outcome: 0 when the seat is exactly on the bone, a few cm when idle easing is on.
    /// The fastest ground speed of the ridden troll since the last stt.rideDrive, in cm/s.
    [TestHook("stt.rideSpeedMax")]
    private static int SpeedMax(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.RoundToInt(s.SpeedMaxCm);
    }

    /// D-216: how often something other than the saddle set the ridden troll's move direction since the last stt.rideDrive (the farming brain used to do it every tick).
    [TestHook("stt.rideForeignMoveSets")]
    private static int ForeignMoveSets(GameObject troll)
    {
        return TrollSaddle.ForeignMoveSets;
    }

    /// T-217 R3: the biggest angle (whole degrees, rounded up) between the rider's lean and the seat's lean in attack frames since the sampling started. Expected outcome: a few
    /// degrees when the rider leans with the seat; about the seat's own lean (stt.rideSeatTiltAttack) when the rider stays upright.
    [TestHook("stt.rideTiltGapAttack")]
    private static int TiltGapAttack(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.CeilToInt(s.Tilt.GapDegMax);
    }

    /// User 2026-11-10 10:37 (the rider is not glued to the chair): the biggest angle (whole degrees, rounded up) between the rider's lean and the seat's lean in ANY
    /// sampled frame since stt.rideSample. Expected outcome: about 0 for a rider who sits ON the chair whatever the troll's body does.
    [TestHook("stt.rideTiltGap")]
    private static int TiltGap(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.CeilToInt(s.Tilt.GapDegAllMax);
    }

    /// T-217 R3: how far (whole degrees, rounded down) the seat itself leaned in attack frames - 0 means the attack never leaned the seat and the tilt gap proves nothing.
    [TestHook("stt.rideSeatTiltAttack")]
    private static int SeatTiltAttack(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.FloorToInt(s.Tilt.AnchorTiltDegMax);
    }

    /// T-217 R2 / R5: the biggest flat distance (cm) of the ridden troll's root from the place where it settled, in any period without rider input since the last
    /// stt.rideSample / stt.rideStillReset. Expected outcome: a few cm for a standing troll; metres when something else moves it.
    [TestHook("stt.rideDrift")]
    private static int Drift(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.CeilToInt(s.Still.DriftCm);
    }

    /// T-217 R5: the ridden troll's flat (XZ) speed now, cm/s.
    [TestHook("stt.rideSpeedXZ")]
    private static int SpeedXZ(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.CeilToInt(s.Still.SpeedXZCm);
    }

    /// T-217 R5: what the saddle's standing watch says now: "Driven", "Settling", "Still" or "Drifting".
    [TestHook("stt.rideStill")]
    private static string StillNow(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? "no saddle" : s.Still.Last.ToString();
    }

    /// T-217 R5: forgets the biggest drift (a new standing measurement starts). Expected outcome: 0.
    [TestHook("stt.rideStillReset")]
    private static int StillReset(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.RoundToInt(s.Still.Reset());
    }

    /// T-217 R5: is the saddle's autorun toggle on (1) or off (0)? A latched autorun is "driven", not drift.
    [TestHook("stt.rideAutoRun")]
    private static int AutoRun(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : s.AutoRunOn ? 1 : 0;
    }

    /// T-217 R1: how often an autonomous driver (farming, recovery, idle, return, rage, chase, slam) was refused on a ridden troll since stt.riddenGateReset.
    /// Expected outcome: above 0 when a busy troll is mounted (its brains asked and were refused), together with stt.rideForeignMoveSets == 0 and no drift.
    [TestHook("stt.riddenGateRefusals")]
    private static int GateRefusals() => SteveTheTroll.Features.Trolls.Infrastructure.RiddenGate.Refusals;

    /// T-217 R1: starts both counts again - the gate refusals and the foreign move-direction sets (stt.rideForeignMoveSets). Expected outcome: 0.
    [TestHook("stt.riddenGateReset")]
    private static int GateReset()
    {
        TrollSaddle.ForeignMoveSets = 0;
        return SteveTheTroll.Features.Trolls.Infrastructure.RiddenGate.ResetRefusals();
    }

    /// T-217 R2, negative test only (meta-ride-shove-fails): moves the ridden troll's root `metres` forward by writing transform.position and Rigidbody.position directly,
    /// exactly what TrollRecovery.LedgeStep did under the rider in the manual test. Expected outcome: the metres moved; the standing watch must call it Drifting.
    [TestHook("stt.rideShove")]
    private static float Shove(GameObject troll, float metres)
    {
        Vector3 before = troll.transform.position;
        Vector3 target = before + troll.transform.forward * metres;
        troll.transform.position = target;
        Rigidbody body = troll.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = target;
            body.linearVelocity = Vector3.zero;
        }

        float moved = Vector3.Distance(before, troll.transform.position);
        ModLog.Info($"RidingTestHooks: rideShove moved the root of {troll.name} by {moved:F2} m (asked {metres:F2} m) from {before:F1} - a deliberate foreign root write for the negative test");
        return moved;
    }

    [TestHook("stt.rideAnchorGap")]
    private static int AnchorGap(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.RoundToInt(s.AnchorGapCm);
    }

    /// D-214: the same, counted only in frames where the troll was attacking. Expected outcome: at most 2 cm - the seat follows an attack exactly.
    [TestHook("stt.rideAnchorGapAttack")]
    private static int AnchorGapAttack(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.RoundToInt(s.AnchorGapAttackCm);
    }

    /// D-214: how many sampled frames the ridden troll spent in an attack (a gap of 0 over 0 frames proves nothing).
    [TestHook("stt.rideAttackFrames")]
    private static int AttackFrames(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : s.AttackFrames;
    }

    /// D-214: the saddle presses the attack button by itself (on = 1) or stops (0). Expected outcome: the ridden troll swings again and again while on.
    [TestHook("stt.rideAttack")]
    private static string RideAttack(GameObject troll, int on)
    {
        var s = troll.GetComponent<TrollSaddle>();
        if (s == null)
        {
            return "no saddle";
        }

        s.TestAttack = on != 0;
        return $"attack {s.TestAttack}";
    }

    /// The biggest jump of the rider inside the troll's frame between two frames, in cm (a shaking rider jumps).
    [TestHook("stt.rideStep")]
    private static int Step(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        return s == null ? -1 : Mathf.RoundToInt(s.StepCm);
    }
    /// The rider presses W (local input 0,0,1) while the camera looks along +X (east): the saddle must send the troll east. Returns the troll's move direction as text.
    [TestHook("stt.rideSteerW")]
    private static string SteerW(GameObject troll, int lookEast)
    {
        var s = troll.GetComponent<TrollSaddle>();
        if (s == null)
        {
            return "no saddle";
        }

        s.ApplyControlls(new Vector3(0f, 0f, 1f), lookEast != 0 ? Vector3.right : Vector3.back, false, false, false);
        return s.LastMove.ToString("F2");
    }

    /// S alone (local 0,0,-1): the troll stops instead of walking backwards. Returns the move direction as text.
    [TestHook("stt.rideSteerS")]
    private static string SteerS(GameObject troll)
    {
        var s = troll.GetComponent<TrollSaddle>();
        if (s == null)
        {
            return "no saddle";
        }

        s.ApplyControlls(new Vector3(0f, 0f, -1f), Vector3.right, false, false, false);
        return s.LastMove.ToString("F2");
    }
    /// Steers the ridden troll by itself: 0 = off, 1 = forward (what W does). Expected outcome: the saddle drives the troll while on.
    [TestHook("stt.rideDrive")]
    private static string Drive(GameObject troll, int forward)
    {
        var s = troll.GetComponent<TrollSaddle>();
        if (s == null)
        {
            return "no saddle";
        }

        s.TestDrive = forward != 0;
        s.TestRun = forward == 2;
        TrollSaddle.ForeignMoveSets = 0;
        return $"drive {s.TestDrive}, run {s.TestRun}";
    }
}
