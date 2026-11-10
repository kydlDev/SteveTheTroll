using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Trolls.Backpack.Application;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure;

/// <summary>
///     D-064 (user spec): a troll with a FULL backpack that reaches its owner's bed empties it into the owner's troll
///     depots near the bed.
///     No depot with room -> a new one is built: on top of the latest stack while it is under 5 high, else a new stack at
///     a spot that is
///     near the bed (15 .. 50 m, D-097), not inside a house (nothing built above it), free (no pieces / rocks in the
///     footprint), on ground that
///     is level or that the game's level op flattens first (up to 2.5 m uneven, D-095),
///     and that the troll can walk to within 10 m of (the game's pathfinding for the troll; it unloads from 5 - 10 m away,
///     D-106). Depot chests are the game's reinforced chests
///     marked stt_depot: they need no support (stacks never crumble) and cannot be removed for their materials.
/// </summary>
internal static partial class TrollDepot
{
    internal const string FlagKey = "stt_depot", OwnerKey = "stt_depot_owner";

    // MaxSlope: ground this even is used as is; MaxSlopeLevelled: what the level op (DepotGround) flattens first; MaxSlopeAsIs: the old
    // limit (depot on the highest point) when the game has no level op.
    // Reach: the troll's path must end this close to a new depot (D-106: it unloads from up to 10 m away)
    internal const float RingStep = 2f,
        Reach = 10f,
        MaxSlope = 0.15f,
        MaxSlopeLevelled = 2.5f,
        MaxSlopeRelaxed = 5f,
        MaxSlopeAsIs = 1f,
        ColumnMatch = 0.5f;

    internal const int Angles = 16;
    private const int MaxNewDepots = 6;
    private const float ChestSlack = 1.5f;

    private const float BusyRadius = 10f;

    // D-149 (review: two trolls could pick the same chest before either is within BusyRadius): a troll that gets a chest from Plan reserves it for ReserveSeconds
    // (a stuck / dead / given-up troll frees it by time); Unload frees the troll's reservations.
    private const float ReserveSeconds = 90f;

    private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
        AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

    private static readonly List<Container> PlanChests = new();
    private static readonly List<DepotCandidate> PlanFacts = new();
    private static readonly UnloadPipeline Unloads = new();
    private static readonly Dictionary<int, KeyValuePair<int, float>> Reserved = new();

    private static readonly Dictionary<int, Container> Retry = new();
    private static float _nextRetry;
    private static readonly Dictionary<int, KeyValuePair<Container, float>> WaitingSince = new();

    /// D-095 / D-097 (user: "15(minimum) - 50m(maximum) from the bed"): new depot stacks stand this far from the bed, and a full troll
    /// unloads once within MaxFromBed (Tunables TrollBackpack DepotMinFromBed / DepotMaxFromBed).
    internal static float MinFromBed => ModSettings.DepotMinFromBed.Value;

    internal static float MaxFromBed => Mathf.Max(ModSettings.DepotMaxFromBed.Value, MinFromBed + RingStep);

    private static float SearchRadius => MaxFromBed + 15f + HubData.OuterRadius; // depot chests are found up to here (D-225: the ring of a hub lies OuterRadius further out)

    private const float HubAnchorMetres = 1f;

    /// D-225 (user 2026-11-10: the depot ring counts from the hub's outer edge too): how far the thing the depots stand around reaches from its own middle - a Troll Hub's
    /// outer radius when a hub stands at `anchor`, 0 for the bed. Expected outcome: HubData.OuterRadius or 0.
    internal static float AnchorOuter(Vector3 anchor)
    {
        foreach (HubInfo h in HubRegistry.All())
        {
            if (Flat(h.Pos - anchor) < HubAnchorMetres)
            {
                return HubData.OuterRadius;
            }
        }

        return 0f;
    }

    /// The inner edge of the depot ring around `anchor`, metres from its MIDDLE (bed: MinFromBed; hub: MinFromBed + its outer radius).
    internal static float MinFrom(Vector3 anchor) => DepotRules.FromEdge(MinFromBed, AnchorOuter(anchor));

    /// The outer edge of the depot ring around `anchor`, metres from its MIDDLE.
    internal static float MaxFrom(Vector3 anchor) => DepotRules.FromEdge(MaxFromBed, AnchorOuter(anchor));

    internal static IEnumerable<ZNetView> Near(Vector3 at, string prefab)
    {
        ZNetScene scene = ZNetScene.instance;
        Dictionary<ZDO, ZNetView> all = scene != null ? Instances(scene) : null;
        if (all == null)
        {
            yield break;
        }

        // T-217 O4 / O5: the prefab and the distance are read from the ZDO first (plain fields); only objects of that prefab near the place are touched in the engine.
        // Before, every networked object of the scene (13,000 .. 29,000) paid for a Unity null check, transform.position and a prefab-name string per call
        float r2 = SearchRadius * SearchRadius, coarse2 = (SearchRadius + 4f) * (SearchRadius + 4f);
        int hash = prefab.GetStableHashCode();
        foreach (KeyValuePair<ZDO, ZNetView> kv in all)
        {
            if (kv.Key.GetPrefab() != hash || (kv.Key.GetPosition() - at).sqrMagnitude > coarse2)
            {
                continue;
            }

            ZNetView v = kv.Value;
            if (v == null || !v.IsValid() || (v.transform.position - at).sqrMagnitude > r2)
            {
                continue;
            }

            yield return v;
        }
    }

    private static List<Container> Chests(Vector3 bed, long owner)
    {
        var list = new List<Container>();
        // D-225: new depots of a hub stand 15 .. 50 m from its OUTER edge. Chests that already stand nearer (built while the ring counted from the hub's middle, down to
        // MinFromBed from the middle) are still unloaded into - a player's filled depots must not go out of use with an update; only the far edge moves out
        float ringMax = MaxFrom(bed);
        foreach (ZNetView v in Near(bed, DepotData.ChestPrefab))
        {
            ZDO z = v.GetZDO();
            if (!z.GetBool(FlagKey) || z.GetLong(OwnerKey) != owner)
            {
                continue;
            }

            // D-104 (user: "sure"): depots outside the 15 .. 50 m ring (older ones next to the house) are not used any more
            // (a chest sits up to ~1.2 m from its frame's centre: a little slack on both edges)
            if (!DepotRules.InRing(Flat(v.transform.position - bed), MinFromBed - ChestSlack, ringMax + ChestSlack))
            {
                continue;
            }

            Container c = v.GetComponent<Container>();
            if (c != null)
            {
                list.Add(c);
            }
        }

        return list;
    }

    /// Where the troll walks to unload. Expected outcome: true with a target (a depot chest with room, or a depot built now).
    internal static bool Plan(Character troll, Vector3 bed, long owner, out Vector3 target)
    {
        // D-173 (Architecture V2, P6): this reads the chests, DepotPlanner chooses (nearest with room, nobody else at / walking to it - D-107 / D-149)
        target = bed;
        PlanChests.Clear();
        PlanFacts.Clear();
        int unreachable = 0;
        foreach (Container c in Chests(bed, owner))
        {
            PlanChests.Add(c);
            bool room = c.GetInventory().HaveEmptySlot();
            bool crowded = room && Busy(c.transform.position, troll);
            bool reserved = room && ReservedByOther(c, troll);
            // #30: only a chest the troll could actually walk to is asked about (the path search is the expensive part)
            bool reachable = !room || crowded || reserved || DepotSpots.CanReach(troll, c.transform.position);
            if (!reachable)
            {
                unreachable++;
            }

            PlanFacts.Add(new DepotCandidate(room, crowded, reserved, Vector3.Distance(c.transform.position, troll.transform.position), reachable));
        }

        DepotPlan plan = DepotPlanner.Choose(PlanFacts);
        int busy = plan.Skipped;
        if (!plan.BuildNew)
        {
            Container chosen = PlanChests[plan.Chosen];
            float best = PlanFacts[plan.Chosen].Distance;
            bool chosenReachable = PlanFacts[plan.Chosen].Reachable;
            target = chosen.transform.position;
            Reserve(chosen, troll);
            PlanChests.Clear();
            ModLog.Info($"TrollDepot: {TrollTaming.Tag(troll)} unloads at the depot chest {best:F1} m away" +
                        (busy > 0 ? $" ({busy} chest(s) skipped: another troll there)" : "") +
                        (unreachable > 0 ? $" ({unreachable} chest(s) with room have no path for the troll{(chosenReachable ? "" : " - this one has none either, walking there anyway")})" : ""));
            return true;
        }

        PlanChests.Clear();
        if (busy > 0)
        {
            ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " - every depot with room has another troll at it (" +
                        busy + " chest(s)) - a new stack");
        }

        return Build(troll, bed, owner, out target, plan.AvoidBusy);
    }

    /// D-107: another tamed troll stands within BusyRadius of the point (flat). Expected outcome: false when only `me` is there.
    internal static bool Busy(Vector3 at, Character me)
    {
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c == me || c.IsDead() || !c.IsTamed() || !TrollTaming.IsTroll(c))
            {
                continue;
            }

            if (DepotRules.Crowded(Flat(c.transform.position - at), BusyRadius))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReservedByOther(Container c, Character me)
    {
        KeyValuePair<int, float> r;
        return Reserved.TryGetValue(c.GetInstanceID(), out r) &&
               DepotRules.ReservedByOther(r.Key, r.Value, me.GetInstanceID(), Time.time, ReserveSeconds);
    }

    private static void Reserve(Container c, Character me)
    {
        if (Reserved.Count > 100)
        {
            Reserved.Clear();
        }

        Reserved[c.GetInstanceID()] = new KeyValuePair<int, float>(me.GetInstanceID(), Time.time);
    }

    /// D-160 (test hooks): reserves a chest for a troll that does not exist, as a troll on its way would.
    internal static void ReserveForTest(Container c)
    {
        Reserved[c.GetInstanceID()] = new KeyValuePair<int, float>(-424242, Time.time);
    }

    private static void Release(Character me)
    {
        int id = me.GetInstanceID();
        var mine = new List<int>();
        foreach (KeyValuePair<int, KeyValuePair<int, float>> kv in Reserved)
        {
            if (kv.Value.Key == id)
            {
                mine.Add(kv.Key);
            }
        }

        foreach (int k in mine)
        {
            Reserved.Remove(k);
        }
    }

    /// Moves everything from the troll's backpack into the owner's depot chests near the bed, building more depots when needed.
    /// Expected outcome: the number of stacks moved; `left` = stacks that found no room.
    internal static int Unload(Character troll, Vector3 bed, long owner, out int left)
    {
        ZDO zdo = TrollTaming.Zdo(troll);
        ZNetView view = troll != null ? troll.GetComponent<ZNetView>() : null;
        bool trollOwner = view != null && view.IsValid() && view.IsOwner();
        left = 0;
        // D-173 (Architecture V2, P6): UnloadPipeline decides per stack (owner / data checks, then Put / BuildDepot / Leave); this class reads and moves the items
        OperationResponse<UnloadStep> gate =
            Unloads.Evaluate(new UnloadInput(trollOwner, zdo != null, true, 0, MaxNewDepots));
        if (!gate.IsSuccess)
        {
            ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " unload not done - " + gate.Reason);
            return 0;
        }

        // T-217 O4 (manual test 00:27:45: a 1,538 ms frame 59 ms after "moved 96 stack(s)"; lab full-backpack-unloads 1,594 ms): the depot chests were searched in the whole
        // scene and sorted again for EVERY stack. They are found and sorted once per unload and again only after a depot was built
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Vector3 from = troll.transform.position;
        List<Container> chests = SortedChests(bed, owner, from);
        int moved = 0, built = 0;
        for (int i = 0; i < BackpackStore.ChestCount; i++)
        {
            Inventory inv = BackpackStore.Read(zdo, i);
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                Container into = Room(chests, item);
                UnloadStep step = Unloads.Evaluate(new UnloadInput(trollOwner, true, into != null, built, MaxNewDepots))
                    .Value;
                while (step == UnloadStep.BuildDepot)
                {
                    Vector3 at;
                    if (!Build(troll, bed, owner, out at))
                    {
                        step = UnloadStep.Leave;
                        break;
                    }

                    built++;
                    chests = SortedChests(bed, owner, from);
                    into = Room(chests, item);
                    step = Unloads.Evaluate(new UnloadInput(trollOwner, true, into != null, built, MaxNewDepots)).Value;
                }

                if (step != UnloadStep.Put)
                {
                    left++;
                    continue;
                }

                into.GetInventory().AddItem(item.Clone());
                inv.RemoveItem(item);
                moved++;
            }

            BackpackStore.Write(zdo, i, inv);
        }

        Release(troll);
        double tookMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        ModLog.Info("TrollDepot: " + TrollTaming.Tag(troll) + " moved " + moved + " stack(s) into the depot, " + left +
                    " left, " + built + " depot(s) built, " + chests.Count + " depot chest(s) in reach, took " + tookMs.ToString("F1") + " ms");
        return moved;
    }

    // A depot chest that takes the whole stack, nearest to the troll first (D-107: the stack it walked to), this client takes ownership
    // of it when nobody has it open.
    private static List<Container> SortedChests(Vector3 bed, long owner, Vector3 from)
    {
        List<Container> list = Chests(bed, owner);
        list.Sort((a, b) =>
            (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
        return list;
    }

    // `sorted` = SortedChests of this unload (nearest first)
    private static Container Room(List<Container> sorted, ItemDrop.ItemData item)
    {
        foreach (Container c in sorted)
        {
            if (c == null || c.IsInUse() || !c.GetInventory().CanAddItem(item))
            {
                continue;
            }

            ZNetView v = c.GetComponent<ZNetView>();
            if (!v.IsOwner())
            {
                v.ClaimOwnership();
            }

            if (v.IsOwner())
            {
                return c;
            }
        }

        return null;
    }

    // A new depot: on the latest stack while it is under MaxStack high, else at a new spot. Expected outcome: true + its position.
    private static bool Build(Character troll, Vector3 bed, long owner, out Vector3 at, bool avoidBusy = false)
    {
        at = bed;
        Quaternion rot;
        bool level = false;
        if (!OnStack(bed, owner, out at, out rot, avoidBusy ? troll : null) &&
            !DepotSpots.FindSpot(troll, bed, out at, out rot, out level))
        {
            ModLog.Warn("TrollDepot: no spot for a troll depot near the bed at " + bed.ToString("F0"));
            return false;
        }

        if (level)
        {
            DepotGround.Flatten(at, rot); // D-095: uneven spot -> the ground is levelled first
        }

        // 2026-11-06 (lab runs 144859 / 152341: "depot built" twice at the same spot 10-14 s apart, 8 chest ZDOs on 4 places): the first depot's chests were no longer scene objects (the zone round the player unloads them), Room() found none, so a second frame went on top.
        // Logged here so the log says it was a duplicate and how many chests already stand there as ZDOs.
        int unseen = 0;
        var known = new List<ZDO>();
        int zi = 0, zg = 0;
        while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(DepotData.ChestPrefab, known, ref zi) && zg++ < 10000)
        {
        }

        foreach (ZDO z in known)
        {
            if (z.GetBool(FlagKey) && z.GetLong(OwnerKey) == owner && Flat(z.GetPosition() - at) < 2.5f && ZNetScene.instance.FindInstance(z) == null)
            {
                unseen++;
            }
        }

        if (unseen > 0)
        {
            ModLog.Warn("TrollDepot: " + TrollTaming.Tag(troll) + " builds a depot at " + at.ToString("F1") + " although " + unseen + " depot chest(s) of this owner already exist there as saved objects that are not loaded (the player is too far) - a DUPLICATE, the old chests keep their items");
        }
        if (Place(at, rot, owner) == null)
        {
            return false;
        }

        ModLog.Info("TrollDepot: depot built at " + at.ToString("F1") + " (" + DepotData.Chests.Length +
                    " chests) for " + TrollTaming.Tag(troll));
        return true;
    }

    /// D-160: instantiates the frame and its 4 flagged chests at the spot (the real build code; the test hooks use it too). Expected outcome: the frame, or null when a prefab is missing.
    internal static GameObject Place(Vector3 at, Quaternion rot, long owner)
    {
        GameObject framePrefab = ZNetScene.instance.GetPrefab(DepotPrefab.Name);
        GameObject chestPrefab = ZNetScene.instance.GetPrefab(DepotData.ChestPrefab);
        if (framePrefab == null || chestPrefab == null)
        {
            ModLog.Warn("TrollDepot: prefab missing (frame " + (framePrefab != null) + ", chest " +
                        (chestPrefab != null) + ")");
            return null;
        }

        GameObject frame = Object.Instantiate(framePrefab, at, rot);
        ZDO fz = frame.GetComponent<ZNetView>().GetZDO();
        fz.Set(OwnerKey, owner);
        foreach (DepotData.Part p in DepotData.Chests)
        {
            GameObject chest = Object.Instantiate(chestPrefab, at + rot * new Vector3(p.X, p.Y, p.Z),
                rot * Quaternion.Euler(0f, p.Yaw, 0f));
            Mark(chest, owner);
        }

        return frame;
    }

    /// D-160: the 4 flagged chests of a frame (test hooks).
    internal static List<Container> ChestsOf(GameObject frame)
    {
        var list = new List<Container>();
        if (frame == null)
        {
            return list;
        }

        foreach (DepotData.Part p in DepotData.Chests)
        {
            Vector3 expected = frame.transform.position + frame.transform.rotation * new Vector3(p.X, p.Y, p.Z);
            foreach (ZNetView v in Near(frame.transform.position, DepotData.ChestPrefab))
            {
                if ((v.transform.position - expected).sqrMagnitude > 0.6f * 0.6f || !v.GetZDO().GetBool(FlagKey))
                {
                    continue;
                }

                Container c = v.GetComponent<Container>();
                if (c != null && !list.Contains(c))
                {
                    list.Add(c);
                    break;
                }
            }
        }

        return list;
    }
}
