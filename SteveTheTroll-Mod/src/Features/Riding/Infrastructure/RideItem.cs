using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using KydlCore.Common;
using SteveTheTroll.Features.Riding.Domain;
using SteveTheTroll.Integrations.Unity;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-213: the craftable item "Troll Halter and Seat" (RideRecipe: workbench level 3, no upgrades, not consumed). It only has to be in the inventory to
///     mount; the worn look (straps, bit, seat, leash) is HalterVisual and appears on the troll, never on the player.
/// </summary>
internal static class RideItem
{
    /// Vanilla prefabs tried as the base, in order (the first that exists gives the game's own item handling).
    private static readonly string[] BasePrefabs = { "SaddleLox", "Saddle", "Flint" };

    /// The item's shared name (what the inventory counts); empty until the item is added.
    internal static string SharedName { get; private set; } = "";

    /// Expected outcome: the item is queued for when the vanilla prefabs exist (true once subscribed).
    internal static bool Install()
    {
        PrefabManager.OnVanillaPrefabsAvailable += Add;
        ModLog.Verbose($"RideItem: waiting for the vanilla prefabs to add {RideRecipe.PrefabName}");
        return true;
    }

    private static void Add()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= Add;
        var cfg = new ItemConfig
        {
            Name = RideRecipe.DisplayName,
            Description = "A leather halter with a bit and a high seat for a tamed troll. Craft it at a workbench of level 3, keep it in your inventory, then use the alternative interact key on your troll to climb up.",
            CraftingStation = CraftingStations.Workbench,
            MinStationLevel = RideRecipe.StationLevel
        };
        foreach (RecipeLine line in RideRecipe.Lines)
        {
            cfg.AddRequirement(new RequirementConfig(line.Prefab, line.Amount));
        }

        Sprite icon = BundleAssets.Load<Sprite>(BundleAssets.HalterIcon);
        if (icon != null)
        {
            cfg.Icons = new[] { icon };
        }

        CustomItem item = null;
        foreach (string baseName in BasePrefabs)
        {
            GameObject basePrefab = PrefabManager.Cache.GetPrefab<GameObject>(baseName);
            if (basePrefab == null || basePrefab.GetComponent<ItemDrop>() == null)
            {
                ModLog.Verbose($"RideItem: {baseName} is not an item prefab - next");
                continue;
            }

            item = new CustomItem(RideRecipe.PrefabName, baseName, cfg);
            if (item.ItemPrefab != null && item.ItemDrop != null)
            {
                ModLog.Verbose($"RideItem: cloned {baseName}");
                break;
            }

            item = null;
        }

        if (item == null)
        {
            ModLog.Error("RideItem: none of the base prefabs exists - no halter item");
            return;
        }

        ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
        shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
        shared.m_equipStatusEffect = null;
        shared.m_setStatusEffect = null;
        shared.m_maxQuality = RideRecipe.MaxQuality;
        shared.m_maxStackSize = 1;
        shared.m_weight = 6f;
        shared.m_teleportable = true;
        shared.m_canBeReparied = false;
        // 2026-11-06 (user: the saddle is never consumed or damaged): no durability, no repair, it stays in the inventory when used
        shared.m_useDurability = false;
        shared.m_maxDurability = 0f;
        shared.m_durabilityPerLevel = 0f;
        shared.m_destroyBroken = false;
        shared.m_consumeStatusEffect = null;
        SharedName = shared.m_name;
        bool added = ItemManager.Instance.AddItem(item);
        ModLog.Info($"RideItem: {RideRecipe.PrefabName} added {added} (workbench level {RideRecipe.StationLevel}, {RideRecipe.Lines.Count} recipe lines, shared name '{SharedName}')");
        ModLog.Check("RideItem: recipe lines", cfg.Requirements.Length, RideRecipe.Lines.Count, 0, "");
    }

    /// Expected outcome: true when the player carries the halter and seat.
    internal static bool InInventory(Player p) => p != null && SharedName.Length > 0 && p.GetInventory() != null && p.GetInventory().CountItems(SharedName) > 0;
}
