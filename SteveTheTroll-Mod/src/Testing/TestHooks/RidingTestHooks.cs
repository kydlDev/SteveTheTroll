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
