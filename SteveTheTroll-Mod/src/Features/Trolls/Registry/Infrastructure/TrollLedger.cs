using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Registry.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure;

/// <summary>
///     NEXT_STEPS item 4, step 4: the troll ledger, BepInEx/config/SteveTheTroll/&lt;world name&gt;_&lt;world uid&gt;/trolls.json - every own troll with its last known place, health and whether it is
///     frozen, written by the client that plays the troll's owner (every WriteSeconds, only when the content changed, never throws). The file survives a crash and a relog.
///     READ-BACK (2026-11-06): ReconcileDelay seconds after joining, the SERVER compares the file with the trolls that exist in the world (a client does not know every ZDO and does nothing):
///     an entry without a troll is reported once (log + a message) and kept in the file as "missing" (with the time it was first noticed) so the evidence survives. A file of another world
///     (name or uid differs) is never used. The rules are LedgerRules; this part reads and writes the file and the world.
/// </summary>
internal static class TrollLedger
{
    private const float WriteSeconds = 30f, ReconcileDelay = 20f;
    private static float _next, _joinedAt = -1f;
    private static string _last = "", _worldKey = "";
    private static bool _reconciled, _read;
    private static List<LedgerEntry> _previous = new();

    /// The entries the last read-back found missing from the world (the tests read it).
    internal static List<LedgerEntry> LastMissing = new();

    /// TrollRegistry.Tick. Expected outcome: true when the file was written this call.
    internal static bool Tick()
    {
        if (Player.m_localPlayer == null || ZNet.instance == null || !ModSettings.On)
        {
            _joinedAt = -1f;
            return false;
        }

        string key = WorldFolder();
        if (key != _worldKey)
        {
            _worldKey = key; // a new world (or the first one): nothing of the old one is kept
            _joinedAt = Time.time;
            _reconciled = false;
            _read = false;
            _previous = new List<LedgerEntry>();
            _last = "";
        }

        if (!_reconciled && Time.time - _joinedAt >= ReconcileDelay)
        {
            Reconcile(false);
        }

        if (Time.time < _next)
        {
            return false;
        }

        _next = Time.time + WriteSeconds;
        return WriteNow() >= 0 && _last.Length > 0 && _lastWritten;
    }

    private static bool _lastWritten;

    /// Builds and writes the file now when the content changed. Expected outcome: the file size in bytes when written, 0 when unchanged, -1 when it failed.
    internal static int WriteNow()
    {
        _lastWritten = false;
        try
        {
            string json = Build(TrollRegistry.Peek());
            if (json == _last)
            {
                return 0;
            }

            string path = FilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json, new UTF8Encoding(false));
            _last = json;
            _lastWritten = true;
            ModLog.Every("TrollLedger.written", 120d, "troll ledger written to " + Path.GetDirectoryName(path) + " (" + json.Length + " bytes)");
            return json.Length;
        }
        catch (Exception e)
        {
            ModLog.Every("TrollLedger.failed", 60d, "troll ledger not written: " + e.GetType().Name + ": " + e.Message);
            return -1;
        }
    }

    /// The folder name of this world: its name AND the game's unique world id (ZNet.GetWorldUID), so two worlds that share a name never read each other's trolls
    /// (user 2026-11-06). Expected outcome: "Name_UID", e.g. "Valheim_8372649182736".
    internal static string WorldFolder()
    {
        return Safe(ZNet.instance.GetWorldName()) + "_" + ZNet.instance.GetWorldUID();
    }

    /// The full path of this world's ledger file. Expected outcome: .../config/SteveTheTroll/Name_UID/trolls.json
    internal static string FilePath()
    {
        return Path.Combine(Path.Combine(Path.Combine(Paths.ConfigPath, "SteveTheTroll"), WorldFolder()), "trolls.json");
    }

    /// Reads the file of this world. Expected outcome: its entries; empty (with the reason in `why`) when there is no file, it is unreadable or it belongs to another world.
    internal static List<LedgerEntry> ReadFile(out string why)
    {
        why = "";
        string path = FilePath();
        if (!File.Exists(path))
        {
            why = "no ledger file yet";
            return new List<LedgerEntry>();
        }

        try
        {
            string world;
            long uid, player;
            List<LedgerEntry> list = LedgerRules.Parse(File.ReadAllText(path, Encoding.UTF8), out world, out uid, out player);
            if (!LedgerRules.Matches(world, uid, ZNet.instance.GetWorldName(), ZNet.instance.GetWorldUID()))
            {
                why = "the file belongs to another world (file: '" + world + "' uid " + uid + ", this world: '" + ZNet.instance.GetWorldName() + "' uid " + ZNet.instance.GetWorldUID() + ") - ignored";
                return new List<LedgerEntry>();
            }

            return list;
        }
        catch (Exception e)
        {
            why = $"unreadable: {e.GetType().FullName}: {e.Message}";
            ModLog.Warn($"TrollLedger: reading {path} threw {e.GetType().FullName}: {e.Message} - the file is ignored");
            return new List<LedgerEntry>();
        }
    }

    /// The ids of every troll ZDO of this world. Expected outcome: the set on the server / in single player; null on a client (it does not know every ZDO).
    internal static HashSet<string> ExistingIds()
    {
        if (ZNet.instance == null || ZDOMan.instance == null || !ZNet.instance.IsServer())
        {
            return null;
        }

        var all = new List<ZDO>();
        int index = 0;
        while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, all, ref index))
        {
        }

        var ids = new HashSet<string>();
        foreach (ZDO z in all)
        {
            string id = z.GetString(TrollRegistry.IdKey);
            if (id.Length > 0)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// The read-back: the file against the world. `force` skips the join delay and the once-per-world rule (tests). Expected outcome: how many troll(s) of the file are missing; -1 = not due.
    internal static int Reconcile(bool force)
    {
        if (!force && _reconciled)
        {
            return -1;
        }

        _reconciled = true;
        LastMissing = new List<LedgerEntry>();
        string why;
        List<LedgerEntry> file = ReadFile(out why);
        _read = true;
        HashSet<string> ids = ExistingIds();
        if (ids == null)
        {
            ModLog.Info("TrollLedger: read-back skipped - this client does not know every troll ZDO (the server is the truth); " + file.Count + " entr(ies) in the file" + (why.Length > 0 ? " (" + why + ")" : ""));
            return 0;
        }

        _previous = file;
        List<LedgerEntry> missing = LedgerRules.MissingEntries(file, ids);
        LastMissing = missing;
        ModLog.Info("TrollLedger: read-back - " + file.Count + " troll(s) in the file, " + ids.Count + " troll ZDO(s) in the world, " + missing.Count + " missing" + (why.Length > 0 ? " (" + why + ")" : ""));
        foreach (LedgerEntry e in missing)
        {
            ModLog.Info("TrollLedger: " + e.Name + " [" + e.Id + "] last seen at " + e.X.ToString("0") + " / " + e.Z.ToString("0") + (e.Missing ? ", missing since " + e.MissingSince : "") + " is not in this world now");
        }

        if (missing.Count > 0)
        {
            LedgerEntry first = missing[0];
            Notify.Show(Loc.T("stevethetroll_ledger_missing", missing.Count.ToString(), first.Name, first.X.ToString("0"), first.Z.ToString("0")), Notify.Kind.Farm);
        }

        return missing.Count;
    }

    /// The ledger text for the list. Expected outcome: a JSON object with one entry per troll (id, name, place, health, loaded, frozen, dead, missing); trolls of the last session that are gone stay in it.
    internal static string Build(IList<TrollInfo> trolls)
    {
        if (!_read)
        {
            string why;
            _previous = ReadFile(out why); // the file of this world from before (kept evidence); another world's file is ignored
            _read = true;
        }

        var live = new List<LedgerEntry>();
        for (int i = 0; i < trolls.Count; i++)
        {
            TrollInfo t = trolls[i];
            live.Add(new LedgerEntry
            {
                Id = t.Id, Name = t.Name, X = t.X, Y = t.Y, Z = t.Z, Health = t.Health, Loaded = t.Loaded, Frozen = TrollFrozenWatch.IsFrozen(t.Id), Dead = t.DiedTicks > 0
            });
        }

        List<LedgerEntry> all = LedgerRules.Merge(_previous, live, ExistingIds(), DateTime.UtcNow);
        _previous = all;
        return LedgerRules.Format(ZNet.instance != null ? ZNet.instance.GetWorldName() : "", ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L,
                                  Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L, all);
    }

    private static string Safe(string world)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            world = (world ?? "world").Replace(c, '_');
        }

        return string.IsNullOrEmpty(world) ? "world" : world;
    }
}
