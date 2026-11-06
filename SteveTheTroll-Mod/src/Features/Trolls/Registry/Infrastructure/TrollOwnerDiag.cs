using System.Globalization;
using SteveTheTroll.Features.Trolls.World.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure;

/// <summary>
///     Diagnosis of "who owns this troll and why" (2026-11-06, farm-friend-detour: the farmer lost its ZDO owner a few metres from its tree). One line with the owner, the zone of the troll and of the player,
///     whether the zone is loaded and inside the game's active area, the synced simulation distance and the distance to the player. Used by the farmer's stall dump and the frozen watch.
/// </summary>
internal static class TrollOwnerDiag
{
    /// One line about the ownership of a troll ZDO. Expected outcome: never null; "no ZDO" for a missing one.
    internal static string Describe(ZDO z)
    {
        if (z == null)
        {
            return "ownership: no ZDO";
        }

        ZNet net = ZNet.instance;
        ZoneSystem zs = ZoneSystem.instance;
        if (net == null || zs == null)
        {
            return "ownership: owner " + z.GetOwner() + ", no world";
        }

        Vector3 reference = net.GetReferencePosition(), at = z.GetPosition();
        Vector2s tz = ZoneSystem.GetZone(at), pz = ZoneSystem.GetZone(reference);
        bool active = ZNetScene.InActiveArea(ZoneSystem.GetZonePos(tz), reference);
        SimulationDistance sim = net.GetSyncedSimulationDistance();
        float flat = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(reference.x, reference.z));
        return "ownership: owner " + z.GetOwner() + " (this client " + ZDOMan.GetSessionID() + (z.IsOwner() ? ", it owns it" : ", NOT the owner") + "), troll zone " + tz.x + "," + tz.y +
               " loaded " + zs.IsZoneLoaded(tz) + ", player zone " + pz.x + "," + pz.y + ", inside the game's active area " + active + ", simulation near " + sim.NearSimulationDistance +
               " total " + sim.TotalSimulationDistance + (sim.IsClassic ? " classic" : "") + ", " + flat.ToString("F0", CultureInfo.InvariantCulture) + " m from the player";
    }
}
