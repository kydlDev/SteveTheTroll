using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using KydlCore.Features.Hub.Contracts;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Orders.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Ui;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>Part of TestHooks: the hooks that read the FOLLOWED troll (split out so the class stays under the size limit).</summary>
internal static partial class TestHooks
{
    /// T-214 B1 / B2: the followed troll's ZDO still exists (its identity, owner, tame state and backpack live there). Expected outcome: true; false once the ZDO was destroyed.
    [TestHook("stt.followedZdoAlive")]
    private static bool FollowedZdoAlive()
    {
        bool alive = FollowedZdo() != null;
        if (!alive)
        {
            ModLog.Warn($"TestHooks: the followed troll's ZDO ({_follow}) is GONE");
        }

        return alive;
    }

    /// T-214 B2: the followed troll has a scene object again (the game builds it from the kept ZDO after a rebuild). Expected outcome: true when loaded.
    [TestHook("stt.followedInstanceLoaded")]
    private static bool FollowedInstanceLoaded() => _followId != null && _followId.Length > 0 && TrollRegistry.FindLoaded(_followId) != null;

    /// T-214 B2: `prefab` items in the followed troll's backpack, read from its ZDO (whichever GameObject it has). Expected outcome: the count; -1 when its ZDO is gone (never 0, so a lost troll cannot pass).
    [TestHook("stt.followedPackCount")]
    private static int FollowedPackCount(string prefab)
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return -1;
        }

        string shared = ObjectDB.instance.GetItemPrefab(prefab).GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
        int n = 0;
        for (int i = 0; i < BackpackStore.ChestCount; i++)
        {
            foreach (ItemDrop.ItemData it in BackpackStore.Read(z, i).GetAllItems())
            {
                if (it.m_shared.m_name == shared)
                {
                    n += it.m_stack;
                }
            }
        }

        return n;
    }

    /// The followed troll holds at most `stacks` stacks (read from its ZDO, whichever GameObject it has). Expected outcome: false when its ZDO is gone.
    [TestHook("stt.followedBackpackAtMost")]
    private static bool FollowedBackpackAtMost(int stacks)
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            ModLog.Every("TestHooks.followedBackpackAtMost", 10.0, "followed troll " + _followId + ": its ZDO (" + _follow + ") is GONE - not a backpack that is too full");
            return z != null;
        }

        int used, slots;
        BackpackStore.Fill(z, out used, out slots);
        if (used > stacks)
        {
            // 2026-11-08 (full11: "hook returned False" could mean a gone ZDO or stacks still inside): say which, and what the unload ledger knows
            ModLog.Every("TestHooks.followedBackpackAtMost", 10.0, "followed troll " + _followId + " holds " + used + " of " + slots + " stack(s), more than the " + stacks + " allowed; " + UnloadLedgerText());
        }

        return used <= stacks;
    }

    /// 2026-11-08 (full11 obstacle-route: the troll delivered all 96 stacks, 15 ms later its backpack pickup took 1 loose Wood, and "backpack = 0" never held again): the stacks the followed
    /// troll has delivered into depots (FarmUnloadLedger, an event that is recorded when it happens). Expected outcome: the count; -1 when its ZDO is gone (never 0, so a lost troll cannot pass).
    [TestHook("stt.followedUnloadedStacks")]
    private static int FollowedUnloadedStacks()
    {
        if (FollowedZdo() == null)
        {
            return -1;
        }

        FarmUnloadLedger.Entry e;
        return FarmUnloadLedger.Of(_follow.ToString(), out e) ? e.Moved : 0;
    }

    /// 2026-11-08: every stack stt.fillBackpack put into the followed troll was delivered into depots (unloaded at least as many stacks as were filled, none left over by the last unload).
    /// Expected outcome: true once delivered; false while not, when nothing was filled, or when its ZDO is gone.
    [TestHook("stt.followedUnloadedAll")]
    private static bool FollowedUnloadedAll()
    {
        if (FollowedZdo() == null)
        {
            return FollowedZdo() != null;
        }

        int filled;
        FilledStacks.TryGetValue(_follow, out filled);
        FarmUnloadLedger.Entry e;
        bool known = FarmUnloadLedger.Of(_follow.ToString(), out e);
        bool all = filled > 0 && known && e.Moved >= filled && e.LastLeft == 0;
        if (!all)
        {
            ModLog.Every("TestHooks.followedUnloadedAll", 10.0, "followed troll " + _followId + ": " + filled + " stack(s) filled; " + UnloadLedgerText());
        }

        return all;
    }

    private static string UnloadLedgerText()
    {
        FarmUnloadLedger.Entry e;
        return FarmUnloadLedger.Of(_follow.ToString(), out e)
            ? "unload ledger: " + e.Unloads + " unload(s), " + e.Moved + " stack(s) delivered, last one moved " + e.LastMoved + " and left " + e.LastLeft
            : "unload ledger: no unload yet";
    }

    /// D-174: one line about the followed troll - loaded or not, ZDO there or not, stacks / slots, distance to its hub. Expected outcome: the text (diagnosis).
    [TestHook("stt.followedState")]
    private static string FollowedState()
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return "followed troll: ZDO gone (" + _follow + ")";
        }

        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        int used, slots;
        BackpackStore.Fill(z, out used, out slots);
        HubInfo hub = c != null ? HubRegistry.For(c) : null;
        string s = "followed troll " + _followId + ": " + (c != null ? "loaded" : "not loaded") + ", at " +
                   z.GetPosition().ToString("F0") + ", backpack " + used + " of " + slots + ", " + UnloadLedgerText()
                   + (hub != null
                       ? ", " + HubGeometry.Flat(hub.Pos - z.GetPosition()).ToString("F0") + " m from " + hub.Name
                       : "");
        ModLog.Info("TestHooks: " + s);
        return s;
    }

    /// D-174: nothing was lost (a farming troll may add more Wood): Wood put in by stt.fillBackpack <= Wood still in the backpack + Wood the owner's depots gained since stt.followTroll.
    /// Expected outcome: true when conserved (the numbers are logged either way).
    [TestHook("stt.followedConserved")]
    private static bool FollowedConserved()
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return z != null;
        }

        int filled;
        Filled.TryGetValue(_follow, out filled);
        int inPack = 0;
        for (int i = 0; i < BackpackStore.ChestCount; i++)
        {
            foreach (ItemDrop.ItemData it in BackpackStore.Read(z, i).GetAllItems())
            {
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == "Wood")
                {
                    inPack += it.m_stack;
                }
            }
        }

        int gained = DepotQuantity("Wood") - _depotWoodAtFollow;
        ModLog.Info("TestHooks: conservation - filled " + filled + " Wood, backpack " + inPack + ", depots gained " +
                    gained + (filled <= inPack + gained ? " - conserved" : " - MISMATCH"));
        return filled > 0 && filled <= inPack + gained;
    }
}
