using KydlCore.Common;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.World.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>NEXT_STEPS item 1: hooks for TrollZones at a small simulation distance (low graphics). Every hook calls the mod's real code; none decides an outcome itself.</summary>
internal static class ZoneTestHooks
{
    /// TrollZones sees the game's simulation area as `near` / `total` zones (a classic square band) - the way low graphics settings make it. -1 = the game's real values.
    /// Expected outcome: the near value now in force.
    [TestHook("stt.zonesSimulation")]
    private static int ZonesSimulation(int near, int total)
    {
        TrollZones.TestNear = near;
        TrollZones.TestTotal = total;
        return TrollZones.TestNear;
    }

    /// Is the zone of this troll kept alive by TrollZones now? Expected outcome: true when TrollZones pokes and simulates it.
    [TestHook("stt.trollZoneKept")]
    private static bool TrollZoneKept(GameObject troll)
    {
        return troll != null && TrollZones.IsZoneKept(troll.transform.position);
    }

    /// Item 1: the zone of the troll stt.followTroll follows (by ZDO, so it works after the game removed the troll's object) is kept by TrollZones now. Expected outcome: true when kept.
    [TestHook("stt.followedZoneKept")]
    private static bool FollowedZoneKept()
    {
        ZDO z = TestHooks.FollowedZdo();
        return z != null && TrollZones.IsZoneKept(z.GetPosition());
    }

    /// Item 4: every own troll counts as "should be travelling" (the frozen-troll watch judges it), or back to the real rule. Expected outcome: the flag now in force.
    [TestHook("stt.forceTravelling")]
    private static bool ForceTravelling(bool on)
    {
        TrollFrozenWatch.TestForceTravelling = on;
        return TrollFrozenWatch.TestForceTravelling;
    }

    /// Item 4: the frozen-troll watch counts this troll as frozen now. Expected outcome: true from the first rung of the ladder on.
    [TestHook("stt.trollFrozen")]
    private static bool TrollFrozen(GameObject troll)
    {
        ZNetView v = troll != null ? troll.GetComponent<ZNetView>() : null;
        return v != null && v.IsValid() && TrollFrozenWatch.IsFrozenZdo(v.GetZDO());
    }

    /// Item 3: is the point `dx`,`dz` metres from the player inside a base area (the game's PlayerBase effect area - workbench, and the Troll Hub's copy of it, where wild creatures do not spawn)?
    /// Expected outcome: true inside the area.
    [TestHook("stt.baseAreaAt")]
    private static bool BaseAreaAt(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        return p != null && EffectArea.IsPointInsideArea(p.transform.position + new Vector3(dx, 0f, dz), EffectArea.Type.PlayerBase, 0f);
    }

    /// How many own trolls without an owner were claimed directly (zone tick + frozen watch) this session. Expected outcome: the count.
    [TestHook("stt.ownTrollClaims")]
    private static int OwnTrollClaims()
    {
        return TrollZones.OwnTrollClaims + TrollFrozenWatch.OwnerClaims;
    }

    /// Who owns the followed troll and why (diagnosis): owner, zones, active-area answers, simulation distance. Expected outcome: one line.
    [TestHook("stt.followedOwnership")]
    private static string FollowedOwnership()
    {
        return TrollOwnerDiag.Describe(TestHooks.FollowedZdo());
    }

    /// Is the followed troll (by ZDO, whichever game object it has) at most `metres` flat from the nearest hub of the player? Expected outcome: false when its ZDO is gone or there is no hub.
    [TestHook("stt.followedWithinHubZone")]
    private static bool FollowedWithinHubZone(int metres)
    {
        ZDO z = TestHooks.FollowedZdo();
        Player p = Player.m_localPlayer;
        if (z == null || p == null)
        {
            return false;
        }

        HubRegistry.All(true);
        float d;
        return HubRegistry.Nearest(p.GetPlayerID(), z.GetPosition(), out d) != null && d <= metres;
    }

    /// Is the followed troll loaded and farming (a TrollFarmer that is not unloading)? Expected outcome: false when it is not loaded.
    [TestHook("stt.followedFarming")]
    private static bool FollowedFarming()
    {
        ZDO z = TestHooks.FollowedZdo();
        ZNetView view = z != null && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(z) : null;
        TrollFarmer f = view != null ? view.GetComponent<TrollFarmer>() : null;
        return f != null && !f.Unloading;
    }

    /// The friend-detour counters as text (diagnosis): "triggered n, sideways legs n, passed n". Expected outcome: the line.
    [TestHook("stt.detourStats")]
    private static string DetourStats()
    {
        return "triggered " + FarmFriendDetour.Triggers + ", sideways legs " + FarmFriendDetour.SidewaysDone + ", passed " + FarmFriendDetour.Passed;
    }

    /// Zeroes the friend-detour counters. Expected outcome: true.
    [TestHook("stt.resetDetourStats")]
    private static bool ResetDetourStats()
    {
        FarmFriendDetour.Triggers = FarmFriendDetour.SidewaysDone = FarmFriendDetour.Passed = 0;
        return true;
    }

    /// Zones TrollZones keeps now. Expected outcome: the count (0 = nothing kept).
    [TestHook("stt.keptZones")]
    private static int KeptZones()
    {
        return TrollZones.KeptCount;
    }
}
