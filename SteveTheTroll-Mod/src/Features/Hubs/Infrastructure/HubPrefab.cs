using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Integrations.Unity;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Features.Hubs.Infrastructure;

/// <summary>
///     D-135: the Troll Hub as our own networked prefab "STT_TrollHub" and as a hammer piece (category KY-Items, recipe
///     HubData.Recipe, every item
///     recoverable: removing it gives back exactly the recipe). Built from the game's own pieces once their prefabs exist
///     (meshes + solid colliders,
///     nothing that wears: no WearNTear, so nothing in the world can damage it) plus the sign (the game's sign board with
///     HubSign instead of Sign).
/// </summary>
internal static class HubPrefab
{
    internal const string IconResource = "stevethetroll_hub_icon.png";

    /// The user's model has four lanterns (prefab Lantern_DN, a dropped item - the build dump only lists pieces) hanging in the air.
    private const string LanternPrefab = "Lantern_DN";

    internal static void Install()
    {
        PrefabManager.OnVanillaPrefabsAvailable += Build;
    }

    private static void Build()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Build;
        try
        {
            Make();
        }
        catch (Exception e)
        {
            ModLog.Error("HubPrefab: building the Troll Hub failed: " + e);
        }
    }

    private static void Make()
    {
        GameObject go = PrefabManager.Instance.CreateEmptyPrefab(HubData.PrefabName, false);
        foreach (Collider c in go.GetComponents<Collider>())
        {
            Object.DestroyImmediate(c);
        }

        foreach (MeshRenderer r in go.GetComponents<MeshRenderer>())
        {
            Object.DestroyImmediate(r);
        }

        foreach (MeshFilter f in go.GetComponents<MeshFilter>())
        {
            Object.DestroyImmediate(f);
        }

        ZNetView view = go.GetComponent<ZNetView>();
        if (view == null)
        {
            view = go.AddComponent<ZNetView>();
        }

        view.m_persistent = true;
        int parts = 0, cols = 0, missing = 0;
        foreach (HubData.Part p in HubData.Parts)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(p.Prefab);
            if (prefab == null)
            {
                missing++;
                ModLog.Warn("HubPrefab: piece '" + p.Prefab + "' not found");
                continue;
            }

            int r;
            GameObject copy = BackpackVisual.VisualCopy(prefab, out r);
            copy.transform.SetParent(go.transform, false);
            copy.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
            copy.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
            copy.transform.localScale = Vector3.one * p.Scale;
            cols += PieceCopy.Colliders(prefab, copy.transform);
            parts++;
        }

        int lanterns = AddLanterns(go);
        bool sign = AddSign(go);
        bool noSpawn = AddNoSpawnArea(go, HubRules.NoSpawnRadius(ModSettings.HubNoSpawnRadius.Value));
        go.AddComponent<TrollHubPiece>();
        Piece piece = go.AddComponent<Piece>();
        piece.m_name = "Troll Hub";
        piece.m_canBeRemoved = true;
        piece.m_groundOnly = true;
        piece.m_canRotate = true;
        // T-200 (user 2026-11-03: "a better hub icon ... or simply the word HUB"): the word HUB on a wooden plaque, embedded in the DLL; the stone floor icon only as a fallback
        Sprite icon = EmbeddedImages.Sprite(IconResource);
        piece.m_icon = icon != null ? icon : IconOf("stone_floor_2x2");
        var cfg = new PieceConfig
        {
            Name = Loc.Token("stevethetroll_hub_piece"),
            Description = Loc.Token("stevethetroll_hub_piece_desc"),
            PieceTable = PieceTables.Hammer,
            Category = KyCategory.For(HubData.PrefabName),
            Icon = piece.m_icon
        };
        var reqs = new RequirementConfig[HubData.Recipe.Length];
        for (int i = 0; i < reqs.Length; i++)
        {
            reqs[i] = new RequirementConfig(HubData.Recipe[i].Item, HubData.Recipe[i].Amount);
        }

        cfg.Requirements = reqs;
        PieceManager.Instance.AddPiece(new CustomPiece(go, false, cfg));
        ModLog.Info("HubPrefab: " + HubData.PrefabName + " built: " + parts + " of " + HubData.Parts.Length +
                    " part(s), " + cols + " collider(s), " + missing + " missing, " + lanterns + " lantern(s), sign " +
                    sign + ", no-spawn area " + noSpawn);
        ModLog.Check("HubPrefab: parts", parts, HubData.Parts.Length, 0, "");
    }

    private static int AddLanterns(GameObject go)
    {
        GameObject lantern = PrefabManager.Instance.GetPrefab(LanternPrefab);
        if (lantern == null)
        {
            ModLog.Warn("HubPrefab: lantern '" + LanternPrefab + "' not found");
            return 0;
        }

        int n = 0;
        foreach (HubData.Part p in HubData.Lanterns)
        {
            int r;
            GameObject copy = BackpackVisual.VisualCopy(lantern, out r);
            copy.transform.SetParent(go.transform, false);
            copy.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
            copy.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f);
            n++;
        }

        return n;
    }

    /// User 2026-11-06 ("similar to workbench, can literally copy the workbench method, just set this range"): the game's workbench keeps wild creatures from spawning around it with an
    /// EffectArea of type PlayerBase (a trigger sphere child). That child is copied under the hub and its sphere set to `radius` metres. Expected outcome: true when the area exists.
    private static bool AddNoSpawnArea(GameObject hub, float radius)
    {
        if (radius <= 0f)
        {
            ModLog.Info("HubPrefab: NoSpawnRadius 0 - no no-spawn area around the hub");
            return false;
        }

        GameObject bench = PrefabManager.Instance.GetPrefab("piece_workbench");
        EffectArea source = null;
        if (bench != null)
        {
            foreach (EffectArea a in bench.GetComponentsInChildren<EffectArea>(true))
            {
                if ((a.m_type & EffectArea.Type.PlayerBase) != 0)
                {
                    source = a;
                    break;
                }
            }
        }

        if (source == null)
        {
            ModLog.Warn("HubPrefab: the workbench's PlayerBase effect area was not found - no no-spawn area on the hub");
            return false;
        }

        GameObject copy = Object.Instantiate(source.gameObject, hub.transform);
        copy.name = "hub_no_spawn_area";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = Vector3.one;
        // The workbench's area is a trigger of some shape (test run 20261106-112543: no SphereCollider on it). EffectArea only needs A trigger collider on its object,
        // so whatever the copy came with is replaced by one sphere of the wanted radius.
        string had = "";
        foreach (Collider old in copy.GetComponents<Collider>())
        {
            had += old.GetType().Name + " ";
            Object.DestroyImmediate(old);
        }

        var sphere = copy.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;
        ModLog.Info("HubPrefab: no-spawn area " + radius.ToString("F0") + " m (copy of the workbench's PlayerBase area, type " + source.m_type + ", its own collider: " +
                    (had.Length > 0 ? had.Trim() : "none") + ", layer " + LayerMask.LayerToName(copy.layer) + ")");
        return true;
    }

    private static Sprite IconOf(string pieceName)
    {
        GameObject p = PrefabManager.Instance.GetPrefab(pieceName);
        Piece piece = p != null ? p.GetComponent<Piece>() : null;
        return piece != null ? piece.m_icon : null;
    }

    // The game's sign, copied under the hub: networking / piece / wear parts out, HubSign in. Its post is not needed: it hangs on the roof beam.
    private static bool AddSign(GameObject hub)
    {
        GameObject src = PrefabManager.Instance.GetPrefab("sign");
        if (src == null)
        {
            ModLog.Warn("HubPrefab: the game's 'sign' prefab was not found - no sign on the hub");
            return false;
        }

        GameObject copy = Object.Instantiate(src, hub.transform);
        copy.name = "hub_sign";
        foreach (Component c in copy.GetComponents<Component>())
        {
            if (c is Sign || c is ZNetView || c is Piece || c is WearNTear)
            {
                Object.DestroyImmediate(c);
            }
        }

        copy.transform.localPosition = new Vector3(HubData.SignX, HubData.SignY, HubData.SignZ);
        copy.transform.localRotation = Quaternion.Euler(0f, HubData.SignYaw, 0f);
        copy.AddComponent<HubSign>();
        return copy.GetComponentInChildren<TMP_Text>(true) != null;
    }
}
