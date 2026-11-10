using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Combat.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Combat.Infrastructure;

/// <summary>
///     D-097 (user: "set no hit zone 15m from bed"): the local player's own tamed trolls never swing within NoHitRadius of
///     the player's bed
///     (flat distance) - no farming, slams, rage or the game's own attacks there - and nothing within that circle is
///     picked as a farm source.
///     D-214 (user 2026-11-10: "isn't he supposed to not attack within 15 m of the hub?"): the same circle around every Troll Hub (the bed alone was checked before - a troll
///     farmed a log 15 m from HUB 1).
///     Wild trolls are not touched.
/// </summary>
internal static class TrollNoHit
{
    private static float _nextLog;
    private static int _blocked;

    /// The local player's bed (false when none is set).
    internal static bool Bed(out Vector3 bed)
    {
        bed = Vector3.zero;
        PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
        if (profile == null || !profile.HaveCustomSpawnPoint())
        {
            return false;
        }

        bed = profile.GetCustomSpawnPoint();
        return true;
    }

    private static readonly List<NoHitCircle> CircleBuffer = new();

    /// Lab only (D-214): stt.reset switches the hub circles off, because most lab tests put HUB 1 25 m in front of the player as a place to walk to, and their trees and trolls stand
    /// inside its 15 m circle. The test hub-no-hit-ring switches them on (stt.hubRing). The game never sets this.
    internal static bool HubRingsOffForTests { get; set; }

    /// D-214: the no-hit circles - the player's bed (NoHitRadius around it, when set) and every Troll Hub (NoHitRadius beyond its walls). Empty when the mod or the radius is off.
    /// The list is shared (rebuilt on every call): read it at once, copy it to keep it.
    internal static List<NoHitCircle> Circles()
    {
        CircleBuffer.Clear();
        float ring = ModSettings.NoHitRadius.Value;
        if (!ModSettings.On || ring <= 0f)
        {
            return CircleBuffer;
        }

        Vector3 bed;
        if (Bed(out bed))
        {
            CircleBuffer.Add(new NoHitCircle(bed.x, bed.z, ring));
        }

        if (HubRingsOffForTests)
        {
            return CircleBuffer;
        }

        float hubRadius = NoHitRules.HubCircleRadius(ring, HubData.OuterRadius);
        foreach (HubInfo hub in HubRegistry.All())
        {
            CircleBuffer.Add(new NoHitCircle(hub.Pos.x, hub.Pos.z, hubRadius));
        }

        return CircleBuffer;
    }

    /// D-214: the position lies inside one of `circles` (see Circles).
    internal static bool InsideAny(IReadOnlyList<NoHitCircle> circles, Vector3 pos)
    {
        return NoHitRules.InsideAny(circles, pos.x, pos.z);
    }

    /// Pure: inside the no-hit circle around the bed (flat, inclusive). Expected outcome: false for radius 0 (off).
    internal static bool Inside(Vector3 bed, Vector3 pos, float radius)
    {
        return NoHitRules.Inside(bed.x, bed.z, pos.x, pos.z, radius); // D-171: the rule is game-free
    }

    /// The position lies in one of the local player's no-hit circles (bed or any Troll Hub, D-214).
    internal static bool InZone(Vector3 pos)
    {
        return InsideAny(Circles(), pos);
    }

    // Harmony hook: src/Integrations/Harmony/TrollsCombatPatches.cs, TrollNoHit_NoSwing (MIGRATION_DECISIONS A5)
    internal static class NoSwing
    {
        internal static bool Prefix(Humanoid __instance, ref bool __result)
        {
            if (__instance == null || !__instance.IsTamed() || !TrollTaming.IsTroll(__instance))
            {
                return true;
            }

            Player p = Player.m_localPlayer;
            if (p == null || TrollTaming.Owner(__instance) != p.GetPlayerID() || !InZone(__instance.transform.position))
            {
                return true;
            }

            __result = false;
            _blocked++;
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 10f;
                ModLog.Verbose("TrollNoHit: " + TrollTaming.Tag(__instance) + " is within " +
                               ModSettings.NoHitRadius.Value.ToString("0") + " m of the bed or " + ModSettings.NoHitRadius.Value.ToString("0") + " m of a hub's walls - no swing (" + _blocked +
                               " swing(s) stopped so far)");
            }

            return false;
        }
    }
}
