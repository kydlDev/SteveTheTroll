using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>
///     2026-11-09 (agent24, full12 + the spot-return history): tests that POLL a state (travelling, backpack = 0, unloading) miss anything that comes and goes between two looks - the troll
///     unloads 1 s after it is full, a pickup puts one item back 15 ms after the delivery. A trip is a sequence of EVENTS, so it is recorded as one: per troll (ZDO id) an ordered, bounded
///     list of what happened (Full, DepotWalk, Unloaded, SpotWalk / SpotSkipped / TripDone, SpotArrived / SpotGaveUp / SpotCancelled / SpotSource). Read by the test hooks (a test waits for an
///     event or checks the order with <see cref="TripGrammar" />) and by the debug log; nothing in the game logic depends on it. Memory: at most MaxPerTroll small entries for MaxTrolls trolls.
///     Unity-free on purpose (unit tests link this file); callers pass the time.
/// </summary>
internal static class TrollJournal
{
    internal const int MaxPerTroll = 64, MaxTrolls = 128;

    internal struct Item
    {
        internal int Seq;
        internal float Time;
        internal string Name, Detail;
    }

    private static readonly Dictionary<string, List<Item>> ByTroll = new();
    private static int _seq;

    /// Records one event. An empty key (a troll without a ZDO) records nothing. Expected outcome: the sequence number of the event, 0 when nothing was recorded.
    internal static int Add(string key, float time, string name, string detail = "")
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(name))
        {
            return 0;
        }

        List<Item> list;
        if (!ByTroll.TryGetValue(key, out list))
        {
            if (ByTroll.Count >= MaxTrolls)
            {
                ByTroll.Clear(); // diagnostics only: starting over beats growing
            }

            list = new List<Item>();
            ByTroll[key] = list;
        }

        if (list.Count >= MaxPerTroll)
        {
            list.RemoveAt(0);
        }

        _seq++;
        list.Add(new Item { Seq = _seq, Time = time, Name = name, Detail = detail ?? "" });
        return _seq;
    }

    /// The event names of one troll, oldest first (empty list for an unknown troll).
    internal static List<string> Names(string key)
    {
        var names = new List<string>();
        List<Item> list;
        if (key != null && ByTroll.TryGetValue(key, out list))
        {
            foreach (Item i in list)
            {
                names.Add(i.Name);
            }
        }

        return names;
    }

    /// How many events with this name the troll has.
    internal static int Count(string key, string name)
    {
        int n = 0;
        List<Item> list;
        if (key != null && ByTroll.TryGetValue(key, out list))
        {
            foreach (Item i in list)
            {
                if (i.Name == name)
                {
                    n++;
                }
            }
        }

        return n;
    }

    /// The newest event of one troll ("" name when there is none).
    internal static Item Last(string key)
    {
        List<Item> list;
        if (key != null && ByTroll.TryGetValue(key, out list) && list.Count > 0)
        {
            return list[list.Count - 1];
        }

        return new Item { Name = "", Detail = "" };
    }

    /// Detail of the newest event with this name ("" when none).
    internal static string DetailOf(string key, string name)
    {
        List<Item> list;
        if (key != null && ByTroll.TryGetValue(key, out list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Name == name)
                {
                    return list[i].Detail;
                }
            }
        }

        return "";
    }

    /// One line per event for a log or a failing hook: "Full | DepotWalk (12.0 m) | Unloaded (96 moved, 0 left)".
    internal static string Text(string key)
    {
        List<Item> list;
        if (key == null || !ByTroll.TryGetValue(key, out list) || list.Count == 0)
        {
            return "(no events)";
        }

        var parts = new List<string>();
        foreach (Item i in list)
        {
            parts.Add(i.Name + (i.Detail.Length > 0 ? " (" + i.Detail + ")" : ""));
        }

        return string.Join(" | ", parts.ToArray());
    }

    /// Forgets everything (stt.reset). Expected outcome: 0.
    internal static int Clear()
    {
        ByTroll.Clear();
        _seq = 0;
        return ByTroll.Count;
    }
}
