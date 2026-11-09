using System;
using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>
///     2026-11-08 (agent24-full11, far-troll-goes-home-obstacle-route): the troll delivered all 96 stacks, 15 ms later its backpack pickup took one loose Wood from the ground, and a test that
///     read "backpack = 0 stacks" waited 240 s for a state that had already come and gone. The delivery itself is an EVENT, so it is recorded as one: per troll (ZDO id) the stacks moved into
///     depots and the stacks left over by the last unload. Read by the test hooks and by the diagnostic log; nothing in the game logic depends on it. Memory: one small entry per troll ZDO.
/// </summary>
internal static class FarmUnloadLedger
{
    internal struct Entry
    {
        internal int Unloads, Moved, LastMoved, LastLeft;
    }

    private static readonly Dictionary<string, Entry> Entries = new();

    /// Records one unload of the troll `zdoId` (moved N stacks, `left` could not be placed). Expected outcome: the total stacks this troll has delivered.
    internal static int Record(string zdoId, int moved, int left)
    {
        Entry e;
        Entries.TryGetValue(zdoId, out e);
        e.Unloads++;
        e.Moved += Math.Max(0, moved);
        e.LastMoved = moved;
        e.LastLeft = left;
        Entries[zdoId] = e;
        return e.Moved;
    }

    /// The record of `zdoId`. Expected outcome: false (and an empty entry) when that troll never unloaded.
    internal static bool Of(string zdoId, out Entry entry)
    {
        return Entries.TryGetValue(zdoId, out entry);
    }

    /// Forgets every record (a test starts from none). Expected outcome: 0 entries left.
    internal static int Clear()
    {
        Entries.Clear();
        return Entries.Count;
    }
}
