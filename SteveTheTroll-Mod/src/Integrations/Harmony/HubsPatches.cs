using HarmonyLib;
using SteveTheTroll.Presentation.Map;
using SteveTheTroll.Features.Hubs.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the Hubs feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// HubPlacement.Visible (Features/Hubs/Infrastructure/HubPlacement.cs)
[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
internal static class HubPlacement_Visible
{
    private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
    {
        HubPlacement.Visible.Postfix(__instance, piece, mode, ref __result);
    }
}

// HubPlacement.Place (Features/Hubs/Infrastructure/HubPlacement.cs)
[HarmonyPatch(typeof(Player), "PlacePiece")]
internal static class HubPlacement_Place
{
    private static bool Prefix(Player __instance, Piece piece, Vector3 pos)
    {
        return HubPlacement.Place.Prefix(__instance, piece, pos);
    }
}

// 2026-11-06 (user: players cannot remove hub map pins): the game's RemovePin (right-click on a pin) does nothing for a hub pin; HubMapPins removes it itself when the hub is gone.
[HarmonyPatch(typeof(Minimap), nameof(Minimap.RemovePin), typeof(Minimap.PinData))]
internal static class HubsPatch_KeepHubPins
{
    private static bool Prefix(Minimap.PinData pin)
    {
        return HubMapPins.Removing || !HubMapPins.IsHubPin(pin);
    }
}
