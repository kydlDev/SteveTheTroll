using System;
using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     D-170: TestLab hooks for the farming slice (suite test farm-chops-tree): a free-standing tree near the player, and
///     how many swings the troll's farmer made. Until now no
///     lab test reached the swing ladder (every farming test ended before the troll swung: "attacks 0" in all logs of
///     2026-11-02).
/// </summary>
internal static class FarmingTestHooks
{
    private static readonly List<GameObject> Planted = new();
    private static readonly List<Vector3> PlantedAt = new();
    private const float DebrisRadius = 4f;

    /// A Beech tree `dx`,`dz` metres from the player, on the ground. Expected outcome: the tree (bind it with "as").
    [TestHook("stt.plantTree")]
    private static GameObject PlantTree(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 at = p.transform.position + new Vector3(dx, 0f, dz);
        at.y = ZoneSystem.instance.GetGroundHeight(at);
        GameObject prefab = ZNetScene.instance.GetPrefab("Beech1");
        if (prefab == null)
        {
            throw new Exception("Beech1 prefab missing");
        }

        GameObject tree = Object.Instantiate(prefab, at, Quaternion.identity);
        Planted.Add(tree);
        PlantedAt.Add(at);
        ModLog.Info("FarmingTestHooks: tree planted at " + at.ToString("F0") + " (" +
                    new Vector2(dx, dz).magnitude.ToString("F1") + " m from the player)");
        return tree;
    }

    /// 2026-11-07 (farm-friend-detour): the game has a troll-sized navmesh path between two ground points `dx`,`dz` metres from the player (same offsets as stt.plantTree). Asking also makes the game build the missing navmesh cells. Without a path the game's own movement stops the troll in place, the stuck rule then sends it 20 m sideways, and a test that needs the troll to walk its straight line never sees it, so such a test waits for this before it spawns the troll. Expected outcome: true once the navmesh is there.
    [TestHook("stt.pathReady")]
    private static bool PathReady(float fromDx, float fromDz, float toDx, float toDz)
    {
        Player p = Player.m_localPlayer;
        if (p == null || Pathfinding.instance == null || ZoneSystem.instance == null)
        {
            return false;
        }

        Vector3 from = p.transform.position + new Vector3(fromDx, 0f, fromDz);
        Vector3 to = p.transform.position + new Vector3(toDx, 0f, toDz);
        from.y = ZoneSystem.instance.GetGroundHeight(from);
        to.y = ZoneSystem.instance.GetGroundHeight(to);
        return Pathfinding.instance.HavePath(from, to, Pathfinding.AgentType.TrollSize);
    }

    /// Planted trees still standing untouched (a felled tree is destroyed, so the lab cannot name it any more - this hook needs no tree argument). Expected outcome: 0 once hit.
    [TestHook("stt.plantedUntouched")]
    private static int PlantedUntouched()
    {
        int n = 0;
        foreach (GameObject g in Planted)
        {
            if (g == null)
            {
                continue;
            }

            TreeBase t = g.GetComponent<TreeBase>();
            ZNetView v = g.GetComponent<ZNetView>();
            if (t == null || v == null || !v.IsValid())
            {
                // A measurement hook never passes when it cannot measure (PLAN D3): -1 matches no expected value, so the test fails with this reason in the log.
                ModLog.Warn($"FarmingTestHooks: plantedUntouched cannot read a planted tree (TreeBase {t != null}, ZNetView {v != null}, valid {v != null && v.IsValid()}) - unknown (-1); the hit counter stt.plantedHitAtLeast is the other measure");
                return -1;
            }

            if (v.GetZDO().GetFloat(ZDOVars.s_health, t.m_health) >= t.m_health - 0.01f)
            {
                n++;
            }
        }

        return n;
    }

    /// D-175: is this one of the trees stt.plantTree made. Expected outcome: true for a planted tree.
    internal static bool IsPlanted(GameObject g)
    {
        return g != null && Planted.Contains(g);
    }

    /// Hits a tamed creature (not debris) did to the planted trees since stt.reset. Expected outcome: the count.
    [TestHook("stt.plantedHitByTroll")]
    private static int PlantedHitByTroll()
    {
        return TreeDamageLog.TrollHitsOnPlanted;
    }

    /// T-214 A6: did a tamed creature hit the planted trees at least `min` times since stt.reset (counted by the hit patch, independent of the tree's own ZDO health)? Expected outcome: true once.
    [TestHook("stt.plantedHitAtLeast")]
    private static bool PlantedHitAtLeast(int min)
    {
        return TreeDamageLog.TrollHitsOnPlanted >= min;
    }

    /// Swings the troll's farmer started (StartAttack accepted). Expected outcome: -1 without a farmer.
    [TestHook("stt.farmAttacks")]
    private static int FarmAttacks(GameObject troll)
    {
        TrollFarmer f = troll != null ? troll.GetComponent<TrollFarmer>() : null;
        return f != null ? f.Attacks : -1;
    }

    /// Has the troll's farmer started at least `min` swings (also the one that makes it remember the spot of its source)? Expected outcome: true once.
    [TestHook("stt.farmAttacksAtLeast")]
    private static bool FarmAttacksAtLeast(GameObject troll, int min)
    {
        return FarmAttacks(troll) >= min;
    }

    /// Removes the planted trees (stt.reset). Expected outcome: how many were removed.
    internal static int ClearPlanted()
    {
        TreeDamageLog.TrollHitsOnPlanted = 0;
        int n = 0;
        foreach (GameObject g in Planted)
        {
            if (g == null)
            {
                continue;
            }

            ZNetView v = g.GetComponent<ZNetView>();
            if (v != null && v.IsValid())
            {
                v.ClaimOwnership();
                ZNetScene.instance.Destroy(g);
            }
            else
            {
                Object.Destroy(g);
            }

            n++;
        }

        // D-176 (noweapon-farm, run 222441: the previous test's felled tree left a stump 2.5 m from the same planting spot; it blocked the next troll, recovery raged at it and the rage swing hit the tree):
        // what a planted tree leaves behind (stump, logs) goes with it - only within a few metres of where the test planted, never the world around
        int debris = 0;
        foreach (Vector3 at in PlantedAt)
        {
            foreach (ZNetView v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
            {
                if (v == null || !v.IsValid())
                {
                    continue;
                }

                GameObject g = v.gameObject;
                Vector3 d = g.transform.position - at;
                d.y = 0f;
                if (d.magnitude > DebrisRadius)
                {
                    continue;
                }

                bool leftover = g.GetComponent<TreeLog>() != null || g.GetComponent<TreeBase>() != null ||
                                (g.GetComponent<Destructible>() != null && g.GetComponent<Piece>() == null && g.GetComponent<Character>() == null);
                if (!leftover)
                {
                    continue;
                }

                v.ClaimOwnership();
                ZNetScene.instance.Destroy(g);
                debris++;
            }
        }

        if (PlantedAt.Count > 0)
        {
            ModLog.Info("FarmingTestHooks: " + n + " planted tree(s) removed, " + debris + " stump / log / debris object(s) within " + DebrisRadius.ToString("F0") + " m of the planting spots removed");
        }

        PlantedAt.Clear();
        Planted.Clear();
        return n;
    }
}
