using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.World.Infrastructure;

/// <summary>Part 2 of TrollZones (split out so the class stays under the size limit).</summary>
internal static partial class TrollZones
{
    // Review figures (D-138): what the zone work costs, every 5 s - the numbers to compare before / after and with [TrollZones] radius 0.
    private static void Stats()
    {
        if (!ModLog.DebugOn || Time.time < _nextStats)
        {
            return;
        }

        _nextStats = Time.time + 5f;
        ModLog.Verbose("TrollZones: last 5 s - Tick " + _tickCount + "x avg " +
                       (_tickCount > 0 ? _tickMs / _tickCount : 0).ToString("F2") + " ms, max " +
                       _tickMaxMs.ToString("F2") + " ms; CreateObjects prefix " + _createCount
                       + "x avg " + (_createCount > 0 ? _createMs / _createCount : 0).ToString("F3") + " ms, max " +
                       _createMaxMs.ToString("F3") + " ms; " + Kept.Count + " zone(s), " + _scanned +
                       " object(s) scanned, "
                       + _claimedTotal + " claimed (" + _reclaimed + " of them again after the game gave them back)");
        ModLog.Verbose("TrollZones: last 5 s - the game's whole CreateObjects (with our prefix) " + _fullCount +
                       "x avg " + (_fullCount > 0 ? _fullMs / _fullCount : 0).ToString("F2") + " ms, max " +
                       _fullMaxMs.ToString("F2") + " ms; zone cache " + CachePool.Count + " entr(ies) for " +
                       Kept.Count + " kept zone(s)");
        _fullMs = _fullMaxMs = 0;
        _fullCount = 0;
        double perMs = 1000.0 / Stopwatch.Frequency;
        ModLog.Verbose("TrollZones: last 5 s - of the Tick time " + (_findTicks * perMs).ToString("F1") +
                       " ms went into FindObjects and " + (_claimTicks * perMs).ToString("F1") +
                       " ms into the claim / cache loop");
        _findTicks = _claimTicks = 0;
        _tickMs = _tickMaxMs = _createMs = _createMaxMs = 0;
        _tickCount = _createCount = _scanned = _claimedTotal = _reclaimed = 0;
    }

    /// Is `zone` inside the game's own ring of `radius` zones around `centre`? Classic distance = a square (the game's IsClassic rule),
    /// else the game's ZonesWithinRadius. Expected outcome: the game's answer for this zone.
    private static bool Within(Vector2s centre, Vector2s zone, int radius, bool classic, bool ghost)
    {
        if (classic)
        {
            return Math.Max(Math.Abs(zone.x - centre.x), Math.Abs(zone.y - centre.y)) <= radius;
        }

        return ZoneSystem.instance.ZonesWithinRadius(centre, zone, radius, ghost);
    }

    /// Does the game itself simulate this zone: inside its active area AND inside the synced near band (smaller at low graphics settings). Expected outcome: true = leave it to the game.
    private static bool GameRuns(Vector2s zone, Vector3 refPos, Vector2s centre, Sim sim)
    {
        return Domain.ZoneKeepRules.GameRunsZone(
            ZNetScene.InActiveArea(ZoneSystem.GetZonePos(zone), refPos),
            Within(centre, zone, sim.Near, sim.Classic, false));
    }

    private static readonly Dictionary<ZDOID, string> LastKnown = new();

    /// A tracked troll leaves the zone keeper's list: why (no ZDO any more / dead / no longer this player's) and where it was last seen with its owner then (hub-walk-back: "the ZDO is gone" said nothing
    /// about when or why). Expected outcome: one line.
    private static void LogGone(ZDOID id, ZDO z, long me)
    {
        string last;
        LastKnown.TryGetValue(id, out last);
        string why = z == null ? "its ZDO no longer exists (destroyed, or no object and no saved data left)" :
            z.GetFloat(ZDOVars.s_health, 1f) <= 0f ? "it is dead" : "it no longer belongs to this player (tamed owner " + z.GetLong(TrollTaming.OwnerKey) + ", this player " + me + ")";
        ModLog.Info("TrollZones: troll " + id + " dropped from the zone keeper - " + why + "; last known: " + (last ?? "never seen here") + (z != null ? "; ZDO now at " + z.GetPosition().ToString("F0") + ", owner " + z.GetOwner() : ""));
        LastKnown.Remove(id);
    }

    /// How many own trolls without an owner the zone tick claimed directly this session (tests read it).
    internal static int OwnTrollClaims;

    /// One line per tracked troll every 3 s: what the zone tick decided and the facts behind it - the troll's position and distance, its zone and the zone's centre, the game's active-area answer for
    /// the troll's OWN position and for the zone centre (they can differ), the near band, the synced simulation distance and the owner. Expected outcome: a line when due, nothing otherwise.
    private static void LogTroll(ZDO z, Vector3 pos, Vector3 refPos, Vector2s centre, Sim sim, string decision)
    {
        string key = "TrollZones.troll." + z.m_uid;
        if (!ModLog.IsDue(key, 3d))
        {
            return;
        }

        Vector2s zone = ZoneSystem.GetZone(pos);
        Vector3 zc = ZoneSystem.GetZonePos(zone);
        ModLog.Every(key, 3d, "TrollZones: troll " + z.m_uid + " decision " + decision + " - at " + pos.ToString("F0") + ", " +
                              Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(refPos.x, refPos.z)).ToString("F0") + " m from the player, zone " + zone.x + "," + zone.y + " (centre " +
                              zc.ToString("F0") + "), inside the game's active area: troll position " + ZNetScene.InActiveArea(pos, refPos) + " / zone centre " + ZNetScene.InActiveArea(zc, refPos) +
                              ", near band " + Within(centre, zone, sim.Near, sim.Classic, false) + ", simulation near " + sim.Near + " total " + sim.Total + (sim.Classic ? " classic" : "") +
                              ", owner " + z.GetOwner() + " (this client " + ZDOMan.GetSessionID() + ")");
    }

    /// Why the zone tick does nothing (every 10 s): no player, the world is not ready, the mod is off, the radius is 0, or the player wears no slippers. Expected outcome: a line when due.
    private static void LogOff(Player p, ZDOMan zm, ZoneSystem zs)
    {
        if (!ModLog.IsDue("TrollZones.off", 10d))
        {
            return;
        }

        string why = p == null ? "no local player" :
            zm == null || zs == null || ZNetScene.instance == null || ZNet.instance == null ? "the world is not ready" :
            !ModSettings.On ? "the mod is switched off" :
            ModSettings.TrollZoneRadius.Value <= 0f ? "TrollZoneRadius is 0" :
            !TrollTaming.Wears(p) ? "the player wears no slippers (skip-because-slippers-off: far trolls are left to the game)" : "unknown";
        ModLog.Every("TrollZones.off", 10d, "TrollZones: idle - " + why + ", " + Trolls.Count + " tracked troll(s)");
    }

    /// Is `at` within sqrt(reach2) metres (flat) of a far troll of this tick? Expected outcome: true when a troll can reach it.
    private static bool NearTroll(Vector3 at, float reach2)
    {
        for (int i = 0; i < Centres.Count; i++)
        {
            float dx = at.x - Centres[i].x, dz = at.z - Centres[i].z;
            if (dx * dx + dz * dz <= reach2)
            {
                return true;
            }
        }

        return false;
    }

    private static int Log(int n)
    {
        if (n != _lastKept)
        {
            _lastKept = n;
            ModLog.Verbose("TrollZones: " + n + " zone(s) kept alive around " + Trolls.Count +
                           " own troll(s) outside the player's area");
        }

        return n;
    }

    // Own tamed trolls: the loaded ones every tick, plus one pass over the world's troll ZDOs (a slice per tick) per world.
    private static void Track(Player p, ZDOMan zm)
    {
        long me = p.GetPlayerID();
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c) || TrollTaming.Owner(c) != me)
            {
                continue;
            }

            ZDO z = TrollTaming.Zdo(c);
            if (z != null)
            {
                Trolls.Add(z.m_uid);
            }
        }

        if (_scanFor != zm)
        {
            _scanFor = zm;
            _scanDone = false;
            _scanIndex = 0;
            Scan.Clear();
            Trolls.Clear();
        }

        if (_scanDone)
        {
            return;
        }

        _scanDone = zm.GetAllZDOsWithPrefabIterative(TrollTaming.TrollPrefab, Scan, ref _scanIndex);
        if (!_scanDone)
        {
            return;
        }

        int found = 0;
        foreach (ZDO z in Scan)
        {
            if (z != null && z.GetBool(ZDOVars.s_tamed) && z.GetLong(TrollTaming.OwnerKey) == me)
            {
                Trolls.Add(z.m_uid);
                found++;
            }
        }

        ModLog.Verbose(
            "TrollZones: world scan done - " + Scan.Count + " troll(s) in the world, " + found + " own tamed");
        Scan.Clear();
    }

    // D-138 (review of the 2026-10-30 test runs: 1 % lows of 11 - 17 fps, hitches up to 417 ms, ~5 000 unowned objects scanned per kept-zone pass): the world is searched
    // ONCE per tick (1 s) and the result is kept per zone; CreateObjects (30+ times a second) only appends it - it used to run FindObjects for every kept zone, with new
    // argument arrays, new visited sets and a new de-duplication set, on every call.
    private sealed class ZoneObjects
    {
        internal readonly List<ZDO> All = new();
        internal readonly List<ZDO> NotDistant = new();
        internal Vector2s Zone;
    }

    /// The kept sectors' objects join the list the game creates / keeps (ZNetScene.CreateDestroyObjects passes the same list to RemoveObjects).
    // Harmony hook: src/Integrations/Harmony/TrollsWorldPatches.cs, TrollZones_Create (MIGRATION_DECISIONS A5)
    internal static class Create
    {
        // TEST-048 / TEST-049: 1.0.21 builds the near list from the player's SIMULATION distance (Settings > graphics; 18 000+ ZDOs in
        // TEST-049), which is larger than the active area the kept zones are chosen against. A kept zone inside that near distance is
        // already listed (adding it again made the game create the troll twice); in the far band the game lists its Distant ZDOs in the
        // distant list. So per call: near-band zones add nothing, far-band zones add only non-Distant ZDOs, zones beyond add all - the
        // lists stay disjoint without a per-frame set of the whole near list (TEST-049: that set cost ~19 000 inserts 30 times a second).
        private static float _nextLog;
        private static long _started;

        // D-141 (review: "the timing covers only this mod's prefix"): the postfix closes the time of the WHOLE game method (prefix + the game's own creating / removing).
        internal static void Postfix()
        {
            if (_started == 0)
            {
                return;
            }

            double ms = (Stopwatch.GetTimestamp() - _started) * 1000.0 / Stopwatch.Frequency;
            _started = 0;
            _fullMs += ms;
            _fullCount++;
            if (ms > _fullMaxMs)
            {
                _fullMaxMs = ms;
            }
        }

        internal static void Prefix(List<ZDO> currentNearObjects)
        {
            _started = Stopwatch.GetTimestamp();
            if (_broken || Cached.Count == 0 || currentNearObjects == null || ZNet.instance == null ||
                ZoneSystem.instance == null)
            {
                return;
            }

            try
            {
                Clock.Reset();
                Clock.Start();
                Sim sim = ReadSim();
                Vector2s centre = ZoneSystem.GetZone(ZNet.instance.GetReferencePosition());
                int notLoaded = 0,
                    nearZones = 0,
                    farZones = 0,
                    outZones = 0,
                    before = currentNearObjects.Count,
                    added = 0;
                // the zone's band is decided on every call (the player moves between ticks); only the search is cached
                foreach (ZoneObjects zo in Cached)
                {
                    if (Within(centre, zo.Zone, sim.Near, sim.Classic, false))
                    {
                        nearZones++;
                        continue;
                    }

                    List<ZDO> list;
                    if (Within(centre, zo.Zone, sim.Total, sim.Classic, true))
                    {
                        farZones++;
                        list = zo.NotDistant;
                    }
                    else
                    {
                        outZones++;
                        list = zo.All;
                    }

                    // D-150 (test run 014829: 134 NullReferenceExceptions in TerrainComp.Load, "Terrain compiler could not find hmap"): objects of a kept zone are only handed to the
                    // game once that zone's terrain is loaded (we poke it every second, it loads a moment later) - a TerrainComp created before its heightmap throws every frame.
                    if (!ZoneSystem.instance.IsZoneLoaded(zo.Zone))
                    {
                        notLoaded++;
                        continue;
                    }

                    for (int k = 0; k < list.Count; k++)
                    {
                        ZDO z = list[k];
                        if (z == null || !z.IsValid())
                        {
                            continue; // destroyed since the last search
                        }

                        currentNearObjects.Add(z);
                        added++;
                    }
                }

                Clock.Stop();
                double ms = Clock.Elapsed.TotalMilliseconds;
                _createMs += ms;
                _createCount++;
                if (ms > _createMaxMs)
                {
                    _createMaxMs = ms;
                }

                if (Time.time >= _nextLog)
                {
                    _nextLog = Time.time + 5f;
                    ModLog.Verbose("TrollZones: CreateObjects - simulation near " + sim.Near +
                                   " / total " + sim.Total + (sim.Classic ? " classic" : "")
                                   + ", game listed " + before + " ZDO(s); kept zones " + Cached.Count + ": " +
                                   nearZones + " already in the game's near list, " + farZones +
                                   " in its far band (Distant ZDOs left to the game), "
                                   + outZones + " beyond; " + notLoaded + " not loaded yet (skipped); added " + added +
                                   " from the cache");
                }

                ModLog.Check("TrollZones: near list grew by the added ZDOs only", currentNearObjects.Count - before,
                    added, 0f, "ZDO(s)");
            }
            catch (Exception e)
            {
                Break("CreateObjects", e);
            }
        }
    }

    /// Item 4 (self-heal, first rung): pokes the zone at `position` and its 8 neighbours so the terrain stays loaded this second. Expected outcome: how many zones were poked.
    internal static int KeepAround(Vector3 position)
    {
        ZoneSystem zs = ZoneSystem.instance;
        if (zs == null || Poke == null)
        {
            return 0;
        }

        int n = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                PokeArgs[0] = ZoneSystem.GetZone(position + new Vector3(dx * ZoneSize, 0f, dz * ZoneSize));
                Poke.Invoke(zs, PokeArgs);
                n++;
            }

        return n;
    }

    /// Is the zone at `position` one of the sectors kept now? Expected outcome: true when TrollZones pokes and simulates it.
    internal static bool IsZoneKept(Vector3 position)
    {
        return KeptSet.Contains(ZoneSystem.GetZone(position));
    }

    /// Sectors kept now (for the in-game check and the log).
    internal static int KeptCount => Kept.Count;

    /// One FindObjects call with reused arguments and a cleared (so equal to fresh) visited set. Expected outcome: `into` holds the zone's ZDOs.
    private static void Find(ZDOMan zm, Vector2s zone, List<ZDO> into)
    {
        if (_visited == null)
        {
            _visited = NewVisited();
        }

        if (VisitedClear != null)
        {
            VisitedClear.Invoke(_visited, null);
        }
        else
        {
            _visited = NewVisited();
        }

        FindArgs[0] = zone;
        FindArgs[1] = into;
        FindArgs[2] = _visited;
        FindObjects.Invoke(zm, FindArgs);
    }

    /// A fresh "visited sectors" set of the type the game's FindObjects wants. Expected outcome: the set, or null when it cannot be made.
    internal static object NewVisited()
    {
        return VisitedType != null ? Activator.CreateInstance(VisitedType) : null;
    }

    /// In-game test: the reflected methods exist with the parameters this class passes, and one FindObjects call on the player's zone works.
    /// Expected outcome: "PASS: ..." / "FAIL: ...".
    internal static string SelfCheck()
    {
        if (Poke == null || FindObjects == null || VisitedType == null)
        {
            return "FAIL: PokeLocalZone " + (Poke != null) + ", FindObjects " + (FindObjects != null) +
                   ", visited type " + (VisitedType != null ? VisitedType.Name : "none");
        }

        Player p = Player.m_localPlayer;
        if (p == null || ZDOMan.instance == null)
        {
            return "SKIP: no player / ZDOMan (methods found, visited " + VisitedType + ")";
        }

        try
        {
            var list = new List<ZDO>();
            FindObjects.Invoke(ZDOMan.instance, new[] { ZoneSystem.GetZone(p.transform.position), list, NewVisited() });
            return "PASS: FindObjects on the player's zone -> " + list.Count + " object(s), visited set " +
                   VisitedType.Name + ", poke " + Poke.GetParameters()[0].ParameterType.Name;
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            ModLog.Warn($"TrollZones: FindObjects self-check threw {inner.GetType().FullName}: {inner.Message}");
            return $"FAIL: FindObjects threw {inner.GetType().FullName}: {inner.Message}";
        }
    }
}
