using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Orders.Domain;

/// <summary>
///     D-214 (user 2026-11-10: "when manual attacks are given, display the English name of the target, not the prefab"): turns a prefab name of a thing a troll can be sent to
///     (a tree, log, stump, rock, bush ...) into the English words a player knows, when the game has no name for it (no hover text). Pure, no game types.
/// </summary>
internal static class ReadableName
{
    private static readonly string[] Ores = { "copper", "tin", "silver", "iron", "obsidian", "scrap", "coal" };
    private static readonly string[] Leads = { "small", "old", "big", "large", "tiny", "young" };
    private static readonly string[] Species = { "beech", "birch", "oak", "pine", "fir", "yggdrasil", "swamp", "ancient", "bush", "shrub" };

    /// Expected outcome: "Beech_small1" -> "Small beech tree", "FirTree_oldLog" -> "Old fir log", "stubbe" -> "Stump", "Rock_3" -> "Rock", "MineRock_Copper" -> "Copper rock",
    /// "SwampTree1_log" -> "Swamp log", an unknown name -> its words split at "_" and capitals, lower case with a capital first letter ("SomeThing_x2" -> "Some thing x"); "" -> "".
    internal static string FromPrefab(string prefab)
    {
        if (string.IsNullOrEmpty(prefab))
        {
            return "";
        }

        string p = prefab.Replace("(Clone)", "").Trim();
        string lower = p.ToLowerInvariant();
        if (lower.StartsWith("stubbe") || lower.Contains("stump"))
        {
            return "Stump";
        }

        if (lower.Contains("rock") || lower.Contains("boulder"))
        {
            foreach (string ore in Ores)
            {
                if (lower.Contains(ore))
                {
                    return Upper(ore) + " rock";
                }
            }

            return "Rock";
        }

        List<string> words = Split(p);
        if (words.Count == 0)
        {
            return p;
        }

        var lead = new List<string>();
        var rest = new List<string>();
        foreach (string w in words)
        {
            (System.Array.IndexOf(Leads, w) >= 0 ? lead : rest).Add(w);
        }

        // "FirTree_oldLog": "old" in front, then "fir tree log" -> "fir log" (a log is not a tree)
        if (rest.Contains("log") && rest.Contains("tree") && rest.Count > 2)
        {
            rest.Remove("tree");
        }

        bool named = rest.Contains("tree") || rest.Contains("log") || rest.Contains("bush") || rest.Contains("shrub");
        if (!named && rest.Count > 0 && System.Array.IndexOf(Species, rest[0]) >= 0 && rest[0] != "bush" && rest[0] != "shrub")
        {
            rest.Add("tree");
        }

        lead.AddRange(rest);
        return Upper(string.Join(" ", lead.ToArray()));
    }

    private static List<string> Split(string p)
    {
        var words = new List<string>();
        var cur = new System.Text.StringBuilder();
        for (int i = 0; i < p.Length; i++)
        {
            char ch = p[i];
            bool sep = ch == '_' || ch == ' ' || ch == '-' || char.IsDigit(ch);
            bool capital = char.IsUpper(ch) && cur.Length > 0 && !char.IsUpper(cur[cur.Length - 1]);
            if (sep || capital)
            {
                if (cur.Length > 0)
                {
                    words.Add(cur.ToString().ToLowerInvariant());
                    cur.Length = 0;
                }

                if (sep)
                {
                    continue;
                }
            }

            cur.Append(ch);
        }

        if (cur.Length > 0)
        {
            words.Add(cur.ToString().ToLowerInvariant());
        }

        return words;
    }

    private static string Upper(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
