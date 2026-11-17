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

    /// 2026-11-15 (user: the name tag flies around instead of sitting on the troll's head): while ridden, the tag's anchor (Character.GetTopPoint, read by the game's EnemyHud)
    /// is the troll's root plus the height measured at mount time - it follows nothing but the troll. Expected outcome: `top` is changed only for the ridden troll.
    internal static void NameTag(Character c, ref Vector3 top)
    {
        if (_troll == null || c != _troll || _visual == null)
        {
            return;
        }

        Vector3 glued = c.transform.position + Vector3.up * _visual.TopOffset;
        if (ModLog.IsDue("RideNameTag", 3d))
        {
            ModLog.Every("RideNameTag", 3d, $"RideNameTag: the game's anchor is {top.y - c.transform.position.y:F2} m above the troll's root (sideways {Vector2.Distance(new Vector2(top.x, top.z), new Vector2(c.transform.position.x, c.transform.position.z)):F2} m), the tag is glued {_visual.TopOffset:F2} m above it");
        }

        top = glued;
    }

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

        // T-217 R7: nothing is written to the troll before the saddle is set up; a failed setup leaves no rider id in the ZDO, no saddle component and the body's
        // interpolation as it was (the saddle's OnDestroy puts it back)
        TrollSaddle saddle = troll.GetComponent<TrollSaddle>() ?? troll.gameObject.AddComponent<TrollSaddle>();
        if (!saddle.Setup(troll, visual.SeatPoint))
        {
            ModLog.Warn($"RideSession.Mount rolled back: the saddle found no seat on {troll.name} - halter and saddle removed, the troll's rider id was never written");
            visual.Remove();
            Object.Destroy(saddle);
            return false;
        }

        _saddle = saddle;
        var view = troll.GetComponent<ZNetView>();
        if (view != null && view.IsValid())
        {
            view.ClaimOwnership();
            view.GetZDO().Set(RiderKey, p.GetPlayerID());
        }

        _troll = troll;
        _visual = visual;
        p.AttachStart(visual.SeatPoint, troll.gameObject, true, false, false, SteveTheTroll.Configuration.ModSettings.RideAnimations[Mathf.Clamp(SteveTheTroll.Configuration.ModSettings.RideAnimation.Value, 0, 3)], new Vector3(0f, 0.5f, 0f), null);
        p.StartDoodadControl(_saddle);
        string id = TrollRegistry.IdOf(troll);
        bool fileWritten = WriteMountFile(MountFile.Write(p.GetPlayerID(), id));
        ModLog.Info($"RideSession: mounted {troll.name} id {id} (mount file written {fileWritten})");
        return IsRidden(troll);
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
            WriteMountFile("");
            return _troll == null;
        }
        finally
        {
            _ending = false;
        }
    }

    /// Relog recovery (RideRecovery): the mount file's text, "" when there is none or it cannot be read (logged). Expected outcome: the text.
    internal static string ReadMountFile()
    {
        string path = FilePath();
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch (IOException ex)
        {
            ModLog.Warn($"RideSession.ReadMountFile: {ex.GetType().FullName}: {ex.Message} while reading {MountFile.FileName} - no relog recovery this time");
        }
        catch (System.UnauthorizedAccessException ex)
        {
            ModLog.Warn($"RideSession.ReadMountFile: {ex.GetType().FullName}: {ex.Message} while reading {MountFile.FileName} - no read access to the config folder, no relog recovery");
        }

        return "";
    }

    /// Relog recovery: the stored ride is handled. Expected outcome: true when the file is empty afterwards.
    internal static bool ClearMountFile() => WriteMountFile("");

    /// Relog recovery (user 2026-11-06: "the troll is sent to its hub (normal behaviour)"): the troll this player rode when the game closed is
    /// freed - its rider marker (ours) cleared, its follow order dropped, so TrollIdle sends it back to work from its hub. Takes the ownership
    /// first, like Mount. Expected outcome: true when the troll carries no rider marker of this player any more.
    internal static bool Release(Character troll, long me)
    {
        ZNetView view = troll != null ? troll.GetComponent<ZNetView>() : null;
        if (view == null || !view.IsValid())
        {
            ModLog.Warn($"RideSession.Release: troll {(troll != null ? troll.name : "null")} has no valid ZNetView - its rider marker stays");
            return false;
        }

        view.ClaimOwnership();
        if (view.GetZDO().GetLong(RiderKey, 0L) == me)
        {
            view.GetZDO().Set(RiderKey, 0L);
        }

        TrollTaming.StopFollow(troll.GetComponent<MonsterAI>());
        bool freed = view.GetZDO().GetLong(RiderKey, 0L) != me;
        ModLog.Info($"RideSession: {troll.name} released after a relog - rider marker of this player {(freed ? "cleared" : "STILL THERE")}, follow order dropped");
        return freed;
    }

    /// Writes the mount file (relog recovery). Expected outcome: true when the file holds `text`; a failed write is logged and the ride goes on without the file.
    private static bool WriteMountFile(string text)
    {
        string path = FilePath();
        try
        {
            File.WriteAllText(path, text);
        }
        catch (IOException ex)
        {
            ModLog.Warn($"RideSession.WriteMountFile: {ex.GetType().FullName}: {ex.Message} while writing {text.Length} character(s) to {MountFile.FileName} - the file is in use or the disk refused it; the ride continues, only the relog recovery of this ride is missing");
        }
        catch (System.UnauthorizedAccessException ex)
        {
            ModLog.Warn($"RideSession.WriteMountFile: {ex.GetType().FullName}: {ex.Message} while writing {MountFile.FileName} - no write access to the config folder; the ride continues without relog recovery");
        }

        return File.Exists(path) && new FileInfo(path).Length == text.Length;
    }

}
