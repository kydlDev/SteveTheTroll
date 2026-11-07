using System.IO;
using BepInEx;
using KydlCore.Common;
using SteveTheTroll.Features.Riding.Domain;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-213: what this client's player rides right now (one troll, one rider). Mount / dismount, the mount file for relog recovery, the queries the Harmony patches read.
///     Rules are RideRules (game-free); this class only applies them to the game.
/// </summary>
internal static class RideSession
{
    /// ZDO key on the troll: the player id of its rider (0 / absent = free). Other clients read it to refuse a second rider.
    internal const string RiderKey = "STT_rider";

    private static Character _troll;
    private static TrollSaddle _saddle;
    private static HalterVisual _visual;
    private static bool _ending;

    /// The troll the player rides, null when none.
    internal static Character Troll => _troll != null && !_troll.IsDead() ? _troll : null;

    internal static bool IsRidden(Character c) => c != null && _troll == c;

    /// 0 = none, else the troll's instance id (what RideRules.Decide compares).
    internal static int RidingId() => Troll != null ? Troll.GetInstanceID() : 0;

    /// The player id of the troll's rider from its ZDO, 0 = nobody. Expected outcome: another client's rider is seen here too.
    internal static long RiderOf(Character troll)
    {
        ZDO zdo = TrollTaming.Zdo(troll);
        return zdo != null ? zdo.GetLong(RiderKey, 0L) : 0L;
    }

    private static string FilePath() => Path.Combine(Paths.ConfigPath, MountFile.FileName);

    /// Expected outcome: the troll is ridden by the local player: AI off (patch), halter on, the player attached to the seat and steering through the saddle, the file written.
    /// False with the reason logged otherwise.
    internal static bool Mount(Player p, Character troll)
    {
        if (p == null || troll == null || Troll != null)
        {
            ModLog.Warn($"RideSession.Mount refused: player {p != null}, troll {troll != null}, already riding {Troll != null}");
            return false;
        }

        HalterVisual visual = HalterVisual.Attach(troll, p);
        if (visual == null)
        {
            return false;
        }

        var view = troll.GetComponent<ZNetView>();
        if (view != null && view.IsValid())
        {
            view.ClaimOwnership();
            view.GetZDO().Set(RiderKey, p.GetPlayerID());
        }

        _saddle = troll.GetComponent<TrollSaddle>() ?? troll.gameObject.AddComponent<TrollSaddle>();
        if (!_saddle.Setup(troll, visual.SeatPoint))
        {
            visual.Remove();
            return false;
        }

        _troll = troll;
        _visual = visual;
        p.AttachStart(visual.SeatPoint, troll.gameObject, true, false, false, SteveTheTroll.Configuration.ModSettings.RideAnimations[Mathf.Clamp(SteveTheTroll.Configuration.ModSettings.RideAnimation.Value, 0, 3)], new Vector3(0f, 0.5f, 0f), null);
        p.StartDoodadControl(_saddle);
        string id = TrollRegistry.IdOf(troll);
        File.WriteAllText(FilePath(), MountFile.Write(p.GetPlayerID(), id));
        ModLog.Info($"RideSession: mounted {troll.name} id {id}");
        return true;
    }

    /// Expected outcome: the player is off, the halter and saddle removed, the file emptied; the item stays in the inventory (it was never taken).
    internal static bool End(Player p, EndReason reason)
    {
        if (_troll == null || _ending)
        {
            return false;
        }

        _ending = true;
        try
        {
            EndOutcome outcome = RideRules.End(reason);
            Character troll = _troll;
            _troll = null;
            if (p != null)
            {
                p.StopDoodadControl();
                if (p.IsAttached())
                {
                    p.AttachStop();
                }
            }

            ZNetView view = troll != null ? troll.GetComponent<ZNetView>() : null;
            if (view != null && view.IsValid())
            {
                view.GetZDO().Set(RiderKey, 0L);
            }

            if (_saddle != null)
            {
                Object.Destroy(_saddle);
            }

            if (_visual != null)
            {
                _visual.Remove();
            }

            ModLog.Info($"RideSession: ended ({reason}) - item {outcome}, troll {(troll != null ? troll.name : "gone")}");
            _saddle = null;
            _visual = null;
            File.WriteAllText(FilePath(), "");
            return true;
        }
        finally
        {
            _ending = false;
        }
    }

    /// On login: an old mount file means the client closed while riding; the file is cleared (the troll behaves as normal again since nothing holds the saddle).
    /// Expected outcome: true when a stale ride was found and cleared.
    internal static bool Recover(long playerId)
    {
        string path = FilePath();
        if (!File.Exists(path))
        {
            return false;
        }

        string id;
        bool stale = MountFile.TryRead(File.ReadAllText(path), playerId, out id);
        if (RideRules.OnLogin(stale ? 1 : 0) == LoginRecovery.None)
        {
            return false;
        }

        File.WriteAllText(path, "");
        ModLog.Info($"RideSession: stale mount file for troll {id} cleared on login - the troll stays under normal orders");
        return true;
    }
}
