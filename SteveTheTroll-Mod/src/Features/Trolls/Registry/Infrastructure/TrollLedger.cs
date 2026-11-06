using System;
using System.IO;
using System.Text;
using BepInEx;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure;

/// <summary>
///     NEXT_STEPS item 4, step 4: the troll ledger, BepInEx/config/SteveTheTroll/&lt;world&gt;/trolls.json - every own troll with its last known place, health and whether it is
///     frozen, written by the client that plays the troll's owner. It is evidence for ghost entries and frozen trolls (the file survives a crash and a relog) and complements
///     the frozen-troll watch. Written every WriteSeconds, and only when the content changed. Never throws: a failed write is logged once per minute.
/// </summary>
internal static class TrollLedger
{
    private const float WriteSeconds = 30f;
    private static float _next;
    private static string _last = "";

    /// TrollRegistry.Tick. Expected outcome: true when the file was written this call.
    internal static bool Tick()
    {
        if (Time.time < _next)
        {
            return false;
        }

        _next = Time.time + WriteSeconds;
        if (Player.m_localPlayer == null || ZNet.instance == null || !ModSettings.On)
        {
            return false;
        }

        try
        {
            string json = Build(TrollRegistry.Peek());
            if (json == _last)
            {
                return false;
            }

            string dir = Path.Combine(Path.Combine(Paths.ConfigPath, "SteveTheTroll"), Safe(ZNet.instance.GetWorldName()));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "trolls.json"), json, new UTF8Encoding(false));
            _last = json;
            ModLog.Every("TrollLedger.written", 120d, "troll ledger written to " + dir + " (" + json.Length + " bytes)");
            return true;
        }
        catch (Exception e)
        {
            ModLog.Every("TrollLedger.failed", 60d, "troll ledger not written: " + e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    /// The ledger text for the list. Expected outcome: a JSON object with one entry per troll (id, name, place, health, loaded, frozen, dead).
    internal static string Build(System.Collections.Generic.IList<TrollInfo> trolls)
    {
        var sb = new StringBuilder("{\n  \"player\": ").Append(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L).Append(",\n  \"trolls\": [");
        for (int i = 0; i < trolls.Count; i++)
        {
            TrollInfo t = trolls[i];
            sb.Append(i == 0 ? "\n" : ",\n").Append("    { \"id\": \"").Append(Esc(t.Id)).Append("\", \"name\": \"").Append(Esc(t.Name)).Append("\", \"x\": ")
                .Append(t.X.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(", \"y\": ").Append(t.Y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))
                .Append(", \"z\": ").Append(t.Z.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(", \"health\": ")
                .Append(t.Health.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(", \"loaded\": ").Append(t.Loaded ? "true" : "false")
                .Append(", \"frozen\": ").Append(TrollFrozenWatch.IsFrozen(t.Id) ? "true" : "false").Append(", \"dead\": ").Append(t.DiedTicks > 0 ? "true" : "false").Append(" }");
        }

        return sb.Append("\n  ]\n}\n").ToString();
    }

    private static string Esc(string s)
    {
        return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
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
