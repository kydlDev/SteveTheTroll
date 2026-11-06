using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>D-189: hooks for the farmed-out troll that explores on (user 2026-11-06: tests at several ranges so the full range is covered).</summary>
internal static class ExploreTestHooks
{
    /// Sets the two search ranges (FarmMaxRange 50 - 200, FarmLongRange 150 - 1000); stt.reset puts the defaults back. Expected outcome: the values now in force.
    [TestHook("stt.farmRanges")]
    private static string FarmRanges(float max, float far)
    {
        ModSettings.FarmMaxRange.Value = max;
        ModSettings.FarmLongRange.Value = far;
        return ModSettings.FarmMaxRange.Value.ToString("F0") + " / " + ModSettings.FarmLongRange.Value.ToString("F0");
    }

    /// The followed troll (by its id, whichever object it has) is doing something purposeful: walking to a source / an exploration point, or working on a source. A troll that stands and waits is false.
    /// Expected outcome: false while not loaded.
    [TestHook("stt.followedPurposeful")]
    private static bool FollowedPurposeful()
    {
        string id = TestHooks.FollowedId();
        Character c = id.Length > 0 ? TrollRegistry.FindLoaded(id) : null;
        TrollFarmer f = c != null ? c.GetComponent<TrollFarmer>() : null;
        return f != null && (f.Travelling || f.CurrentItem.Length > 0 || f.Unloading || f.GoingHome);
    }

    /// Exploration steps taken since stt.reset, and the followed troll's distance to its hub, as text (diagnosis). Expected outcome: the line.
    [TestHook("stt.exploreStats")]
    private static string ExploreStats()
    {
        return "explore steps " + TrollFarmer.ExploreStepsTotal;
    }

    /// At least `min` exploration steps were taken since stt.reset. Expected outcome: true once.
    [TestHook("stt.exploredAtLeast")]
    private static bool ExploredAtLeast(int min)
    {
        return TrollFarmer.ExploreStepsTotal >= min;
    }

    /// Destroys every source the troll's farmer could target (trees, stumps, logs, rocks, bushes - what TrollFarming.Gather finds) within `radius` m of the player, in the loaded scene; stt.clearAround leaves
    /// stumps, bushes and old logs (run 143626: the "farmed out" troll still found them). Expected outcome: how many were destroyed.
    [TestHook("stt.clearSources")]
    private static int ClearSources(float radius)
    {
        Player p = Player.m_localPlayer;
        var list = new System.Collections.Generic.List<TrollFarming.Candidate>();
        int n = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            TrollFarming.Gather(p.transform.position, radius, list);
            foreach (TrollFarming.Candidate c in list)
            {
                if (c.G == null)
                {
                    continue;
                }

                ZNetView v = c.G.GetComponentInParent<ZNetView>();
                if (v != null && v.IsValid())
                {
                    v.ClaimOwnership();
                    ZNetScene.instance.Destroy(v.gameObject);
                    n++;
                }
            }
        }

        ModLog.Info("ExploreTestHooks: " + n + " farm source(s) destroyed within " + radius.ToString("F0") + " m of the player");
        return n;
    }

    private static float _depotTeleportAt = -100f;

    /// Run 152341/152847: a depot built 100 m from the player has no scene objects (zones load round the PLAYER), its chest ZDOs hold no items string, and stt.followedConserved reads 0 for the whole wait - while the next test, standing near, finds 4800 Wood in them.
    /// Brings the player within 40 m of the nearest depot chest of this player (the game's own TeleportTo, once per 15 s) and returns true when that chest is a live scene object, so a `wait until` reads loaded chests instead of guessing. Expected outcome: true within ~10 s; false (logged) when no depot chest exists.
    [TestHook("stt.nearDepot")]
    private static bool NearDepot()
    {
        Player p = Player.m_localPlayer;
        var list = new System.Collections.Generic.List<ZDO>();
        int idx = 0, guard = 0;
        while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(DepotData.ChestPrefab, list, ref idx) && guard++ < 10000)
        {
        }

        ZDO best = null;
        float bestD = float.MaxValue;
        foreach (ZDO z in list)
        {
            if (!z.GetBool(TrollDepot.FlagKey) || z.GetLong(TrollDepot.OwnerKey) != p.GetPlayerID())
            {
                continue;
            }

            float d = Vector3.Distance(p.transform.position, z.GetPosition());
            if (d < bestD)
            {
                bestD = d;
                best = z;
            }
        }

        if (best == null)
        {
            ModLog.Info("ExploreTestHooks: nearDepot - this player has no depot chest ZDO");
            return false;
        }

        ZNetView inst = ZNetScene.instance.FindInstance(best);
        bool live = inst != null;
        if (bestD > 40f && Time.time - _depotTeleportAt > 15f)
        {
            _depotTeleportAt = Time.time;
            Vector3 to = best.GetPosition() + new Vector3(6f, 0f, 0f);
            ModLog.Info("ExploreTestHooks: nearDepot - the nearest depot chest is " + bestD.ToString("F0") + " m away (scene object " + live + "): the player teleports to " + to.ToString("F1"));
            p.TeleportTo(to, Quaternion.identity, false);
            return false;
        }

        ModLog.Info("ExploreTestHooks: nearDepot - nearest depot chest " + bestD.ToString("F0") + " m away, scene object " + live);
        return bestD <= 40f && live;
    }
    private static float _emptySince = -1f;

    /// Run 152341: stt.clearSources destroyed 11 sources and 0.5 s later 43 were back in Gather (rocks and a stump 16-44 m away, owned here): the loaded scene fills up in the seconds after a teleport, so one clearing is not "empty".
    /// Clears again on every call and returns true once Gather has found NOTHING for `seconds` in a row. Use it in a `wait until`. Expected outcome: true within a few seconds; the log says how many were destroyed per call.
    [TestHook("stt.areaEmpty")]
    private static bool AreaEmpty(float radius, float seconds)
    {
        Player p = Player.m_localPlayer;
        var list = new System.Collections.Generic.List<TrollFarming.Candidate>();
        TrollFarming.Gather(p.transform.position, radius, list);
        int alive = 0;
        foreach (TrollFarming.Candidate c in list)
        {
            if (c.G != null)
            {
                alive++;
            }
        }

        if (alive > 0)
        {
            ClearSources(radius);
            _emptySince = -1f;
            ModLog.Info("ExploreTestHooks: area not empty yet - " + alive + " source(s) found and cleared again");
            return false;
        }

        if (_emptySince < 0f)
        {
            _emptySince = Time.time;
        }

        float quiet = Time.time - _emptySince;
        ModLog.Info("ExploreTestHooks: area empty for " + quiet.ToString("F1") + " s (needs " + seconds.ToString("F1") + " s)");
        return quiet >= seconds;
    }
    /// How many farm sources (what TrollFarming.Gather finds, so stumps, bushes and old logs too) are still within `radius` m of the player, in the loaded scene; the first five are logged with name, position and ZDO owner.
    /// Run 144859: a stump was found by the troll right after stt.clearSources - this tells whether it survived the clearing or appeared afterwards. Expected outcome: 0 after stt.clearSources.
    [TestHook("stt.sourcesLeft")]
    private static int SourcesLeft(float radius)
    {
        Player p = Player.m_localPlayer;
        var list = new System.Collections.Generic.List<TrollFarming.Candidate>();
        TrollFarming.Gather(p.transform.position, radius, list);
        int shown = 0;
        foreach (TrollFarming.Candidate c in list)
        {
            if (c.G == null || shown >= 5)
            {
                continue;
            }

            shown++;
            ZNetView v = c.G.GetComponentInParent<ZNetView>();
            ModLog.Info("ExploreTestHooks: source left: " + c.G.name + " (" + TrollFarming.KindOf(c.G) + ") at " + c.Pos.ToString("F0") + ", " + Vector3.Distance(p.transform.position, c.Pos).ToString("F0") + " m from the player, ZNetView " +
                        (v == null ? "none" : v.IsValid() ? "valid, owner here " + v.IsOwner() : "invalid"));
        }

        ModLog.Info("ExploreTestHooks: " + list.Count + " farm source(s) left within " + radius.ToString("F0") + " m of the player");
        return list.Count;
    }
    /// stt.reset: the default ranges and a zero counter. Expected outcome: nothing.
    internal static void Reset()
    {
        ModSettings.FarmMaxRange.Reset();
        ModSettings.FarmLongRange.Reset();
        TrollFarmer.ExploreStepsTotal = 0;
    }
}
