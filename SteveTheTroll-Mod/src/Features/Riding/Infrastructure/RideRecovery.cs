using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Riding.Domain;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-213 relog recovery (user 2026-11-06; built 2026-11-17 - before, the mount file was written on every mount but never read): when the game
///     closed while the player rode a troll, the troll kept "ridden by this player" in its ZDO (another player could not mount it) and nobody sent it
///     back. After a login this reads the mount file, waits until that troll is loaded, frees it (RideSession.Release: marker cleared, follow order
///     dropped, so TrollIdle sends it back to work from its hub) and empties the file. The DECISION is RideRules.OnLogin (game-free, unit-tested);
///     this class only reads the game and acts.
/// </summary>
internal static class RideRecovery
{
    private static Player _player;
    private static string _pending = "";
    private static float _next;

    /// The troll id still waiting to be freed ("" = none). Read by the TestLab hooks.
    internal static string Pending => _pending;

    /// What the last step did (TestLab hooks, diagnosis): "", "waiting", "released", "other rider", "new ride".
    internal static string Last { get; private set; } = "";

    /// Plugin.Update, once a second. Expected outcome: the step taken this time ("" = nothing to do).
    internal static string Tick()
    {
        if (Time.time < _next || !ModSettings.On)
        {
            return "";
        }

        _next = Time.time + 1f;
        Player p = Player.m_localPlayer;
        if (p != _player)
        {
            _player = p;
            _pending = p != null ? StoredTroll(p.GetPlayerID()) : "";
            if (_pending.Length > 0)
            {
                ModLog.Info($"RideRecovery: login of {p.GetPlayerName()} with a mount file for troll {_pending} (the game closed during a ride) - it is freed once it is loaded");
            }
        }

        if (p == null || _pending.Length == 0)
        {
            return "";
        }

        long me = p.GetPlayerID();
        bool newRide = RideSession.Troll != null;
        Character troll = newRide ? null : TrollRegistry.FindLoaded(_pending);
        LoginRecovery step = RideRules.OnLogin(true, newRide, troll != null, troll != null ? RideSession.RiderOf(troll) : 0L, me);
        return Apply(step, troll, me);
    }

    private static string Apply(LoginRecovery step, Character troll, long me)
    {
        switch (step)
        {
            case LoginRecovery.WaitForTroll:
                ModLog.Every("RideRecoveryWait", 60d, $"RideRecovery: troll {_pending} is not loaded yet (its zone is far away) - still waiting");
                Last = "waiting";
                return Last;
            case LoginRecovery.YieldToNewRide:
                ModLog.Info($"RideRecovery: a new ride started before troll {_pending} was loaded - the new ride owns the mount file now (kept)");
                return Done("new ride", step);
            case LoginRecovery.LeaveToOtherRider:
                ModLog.Info($"RideRecovery: troll {_pending} is ridden by another player now - only the mount file is emptied");
                return Done("other rider", step);
            case LoginRecovery.ReleaseTroll:
                bool freed = RideSession.Release(troll, me);
                TrollIdle.ResumeNow("troll freed after a relog during a ride");
                return Done(freed ? "released" : "release failed", step);
            default:
                return Done("", step);
        }
    }

    private static string Done(string what, LoginRecovery step)
    {
        bool cleared = RideRules.EmptiesMountFile(step) && RideSession.ClearMountFile();
        ModLog.Verbose($"RideRecovery: {(what.Length > 0 ? what : "nothing stored")}, mount file emptied {cleared}");
        _pending = "";
        Last = what;
        return what;
    }

    /// The troll id the mount file stores for this player, "" for none (another player's line, damage, empty).
    private static string StoredTroll(long playerId)
    {
        string troll;
        return MountFile.TryRead(RideSession.ReadMountFile(), playerId, out troll) ? troll : "";
    }
}
