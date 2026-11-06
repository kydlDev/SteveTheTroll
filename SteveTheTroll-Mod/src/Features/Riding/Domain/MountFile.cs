using System.Globalization;

namespace SteveTheTroll.Features.Riding.Domain;

/// <summary>
///     T-213 (user 2026-11-06, multiplayer): each client keeps track of which troll its player rides, in a small file in the config folder. Format, one line:
///     `player=&lt;id&gt;;troll=&lt;id&gt;` (ids in the mod's own troll id form, e.g. a hex string). An empty or missing file = not mounted. Game-free so the lab and unit tests read it.
/// </summary>
internal static class MountFile
{
    internal const string FileName = "SteveTheTroll.mount.txt";

    /// Expected outcome: the line the file holds for this player riding this troll; an empty string when nobody rides (the file is then emptied).
    internal static string Write(long playerId, string trollId)
    {
        return string.IsNullOrEmpty(trollId) ? "" : "player=" + playerId.ToString(CultureInfo.InvariantCulture) + ";troll=" + trollId;
    }

    /// Reads a file's text. Expected outcome: true with the ids for a valid line of THIS player; false (and empty outputs) for anything else - another player's line, damage, empty.
    internal static bool TryRead(string text, long playerId, out string trollId)
    {
        trollId = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string line = text.Trim();
        int nl = line.IndexOfAny(new[] { '\r', '\n' });
        if (nl >= 0)
        {
            line = line.Substring(0, nl);
        }

        string[] parts = line.Split(';');
        if (parts.Length != 2 || !parts[0].StartsWith("player=") || !parts[1].StartsWith("troll="))
        {
            return false;
        }

        long who;
        if (!long.TryParse(parts[0].Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out who) || who != playerId)
        {
            return false;
        }

        string troll = parts[1].Substring(6).Trim();
        if (troll.Length == 0)
        {
            return false;
        }

        trollId = troll;
        return true;
    }
}