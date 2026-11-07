using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SteveTheTroll.Features.Trolls.Registry.Domain;

/// <summary>One troll of the ledger file (trolls.json). Plain data, no game types.</summary>
internal sealed class LedgerEntry
{
    internal string Id = "", Name = "", MissingSince = "";
    internal float X, Y, Z, Health;
    internal bool Loaded, Frozen, Dead, Missing;
}

/// <summary>
///     NEXT_STEPS item 4, step 4, read-back (2026-11-06): the game-free rules of the troll ledger. The file belongs to ONE world (name AND world uid, user 2026-11-06): a file whose world does not
///     match is never used. On join the server compares the file with the trolls that exist in the world now: an entry without a troll is MISSING (reported once, kept in the file as missing
///     with the time it was first noticed, dropped after KeepDays or when the troll is dead). A client does not know every ZDO, so it never flags anything missing.
/// </summary>
internal static class LedgerRules
{
    internal const int KeepDays = 7;
    private const string StampFormat = "yyyy-MM-ddTHH:mm:ssZ";

    /// Does the file belong to this world? Expected outcome: true only when the world uid is known (not 0) and equal AND the names are equal (ignoring case).
    internal static bool Matches(string fileWorld, long fileUid, string world, long uid)
    {
        return uid != 0L && fileUid == uid && string.Equals(fileWorld ?? "", world ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// The UTC time as the file writes it. Expected outcome: "yyyy-MM-ddTHH:mm:ssZ".
    internal static string Stamp(DateTime utc)
    {
        return utc.ToUniversalTime().ToString(StampFormat, CultureInfo.InvariantCulture);
    }

    /// The file text for a world and its entries (one entry per line - Parse reads it back). Expected outcome: a JSON object with world, worldUid, player and trolls.
    /// <summary>Upper bound for every ledger regex (Sonar S6444): the ledger is a few KB of our own text, so one second means pathological input.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    internal static string Format(string world, long worldUid, long player, IList<LedgerEntry> entries)
    {
        var sb = new StringBuilder("{\n  \"world\": \"").Append(Esc(world)).Append("\",\n  \"worldUid\": ").Append(worldUid.ToString(CultureInfo.InvariantCulture)).Append(",\n  \"player\": ")
            .Append(player.ToString(CultureInfo.InvariantCulture)).Append(",\n  \"trolls\": [");
        for (int i = 0; i < entries.Count; i++)
        {
            LedgerEntry t = entries[i];
            sb.Append(i == 0 ? "\n" : ",\n").Append("    { \"id\": \"").Append(Esc(t.Id)).Append("\", \"name\": \"").Append(Esc(t.Name)).Append("\", \"x\": ").Append(Num(t.X, "0.#")).Append(", \"y\": ")
                .Append(Num(t.Y, "0.#")).Append(", \"z\": ").Append(Num(t.Z, "0.#")).Append(", \"health\": ").Append(Num(t.Health, "0")).Append(", \"loaded\": ").Append(Bool(t.Loaded))
                .Append(", \"frozen\": ").Append(Bool(t.Frozen)).Append(", \"dead\": ").Append(Bool(t.Dead));
            if (t.Missing)
            {
                sb.Append(", \"missing\": true, \"missingSince\": \"").Append(Esc(t.MissingSince)).Append("\"");
            }

            sb.Append(" }");
        }

        return sb.Append("\n  ]\n}\n").ToString();
    }

    /// Reads a ledger text. Expected outcome: the entries (empty for anything that is not a ledger; throws only RegexMatchTimeoutException on pathological input - the caller logs it); world / uid / player are what the header says (null / 0 when missing).
    internal static List<LedgerEntry> Parse(string json, out string world, out long worldUid, out long player)
    {
        world = null;
        worldUid = 0L;
        player = 0L;
        var list = new List<LedgerEntry>();
        if (string.IsNullOrEmpty(json))
        {
            return list;
        }

        Match m = Regex.Match(json, "\"world\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.None, RegexTimeout);
        world = m.Success ? Unesc(m.Groups[1].Value) : null;
        worldUid = Long(json, "worldUid");
        player = Long(json, "player");
        foreach (string line in json.Split('\n'))
        {
            Match id = Regex.Match(line, "\"id\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.None, RegexTimeout);
            if (!id.Success)
            {
                continue;
            }

            list.Add(new LedgerEntry
            {
                Id = Unesc(id.Groups[1].Value), Name = Unesc(Str(line, "name")), X = Flt(line, "x"), Y = Flt(line, "y"), Z = Flt(line, "z"), Health = Flt(line, "health"),
                Loaded = Flag(line, "loaded"), Frozen = Flag(line, "frozen"), Dead = Flag(line, "dead"), Missing = Flag(line, "missing"), MissingSince = Unesc(Str(line, "missingSince"))
            });
        }

        return list;
    }

    /// The new file content: every live troll (flagged missing when `existing` is known and has no such troll), plus the previous entries that are not live (kept; flagged missing when their troll
    /// is gone; dropped when dead or missing for KeepDays). `existing` = the ids of the troll ZDOs of the world, null = not known (a client) - nothing is flagged then.
    /// Expected outcome: live entries first (in order), then the kept previous ones.
    internal static List<LedgerEntry> Merge(IList<LedgerEntry> previous, IList<LedgerEntry> live, ISet<string> existing, DateTime utcNow)
    {
        var result = new List<LedgerEntry>();
        var seen = new HashSet<string>();
        string now = Stamp(utcNow);
        foreach (LedgerEntry e in live)
        {
            seen.Add(e.Id);
            LedgerEntry prev = Find(previous, e.Id);
            e.Missing = !e.Dead && existing != null && !existing.Contains(e.Id);
            e.MissingSince = e.Missing ? (prev != null && prev.Missing && prev.MissingSince.Length > 0 ? prev.MissingSince : now) : "";
            result.Add(e);
        }

        foreach (LedgerEntry p in previous)
        {
            if (seen.Contains(p.Id) || p.Dead)
            {
                continue;
            }

            bool gone = existing != null && !existing.Contains(p.Id);
            if (gone)
            {
                p.Missing = true;
                if (p.MissingSince.Length == 0)
                {
                    p.MissingSince = now;
                }

                DateTime since;
                if (DateTime.TryParseExact(p.MissingSince, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out since) &&
                    utcNow.ToUniversalTime() - since > TimeSpan.FromDays(KeepDays))
                {
                    continue;
                }
            }
            else
            {
                p.Missing = false;
                p.MissingSince = "";
            }

            result.Add(p);
        }

        return result;
    }

    /// The entries of the file whose troll does not exist in the world now. Expected outcome: living entries only; empty when `existing` is null (not known).
    internal static List<LedgerEntry> MissingEntries(IList<LedgerEntry> file, ISet<string> existing)
    {
        var list = new List<LedgerEntry>();
        if (existing == null)
        {
            return list;
        }

        foreach (LedgerEntry e in file)
        {
            if (!e.Dead && !existing.Contains(e.Id))
            {
                list.Add(e);
            }
        }

        return list;
    }

    private static LedgerEntry Find(IList<LedgerEntry> list, string id)
    {
        foreach (LedgerEntry e in list)
        {
            if (e.Id == id)
            {
                return e;
            }
        }

        return null;
    }

    private static string Num(float v, string format)
    {
        return v.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string Bool(bool b)
    {
        return b ? "true" : "false";
    }

    private static long Long(string s, string key)
    {
        Match m = Regex.Match(s, "\"" + key + "\":\\s*(-?\\d+)", RegexOptions.None, RegexTimeout);
        long v;
        return m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0L;
    }

    private static float Flt(string s, string key)
    {
        Match m = Regex.Match(s, "\"" + key + "\":\\s*(-?[0-9.]+)", RegexOptions.None, RegexTimeout);
        float v;
        return m.Success && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
    }

    private static bool Flag(string s, string key)
    {
        return Regex.IsMatch(s, "\"" + key + "\":\\s*true", RegexOptions.None, RegexTimeout);
    }

    private static string Str(string s, string key)
    {
        Match m = Regex.Match(s, "\"" + key + "\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.None, RegexTimeout);
        return m.Success ? m.Groups[1].Value : "";
    }

    private static string Esc(string s)
    {
        return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    private static string Unesc(string s)
    {
        return (s ?? "").Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
