using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Riding.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Riding.Infrastructure;

/// <summary>
///     T-213: the alternative interact key (Shift + E) on a tamed troll mounts it when the halter is in the inventory, and dismounts when already riding it. In every
///     other case the key does what it did before (rename), except while riding another troll (nothing). Hover prompt: shown only when the key would mount / dismount.
/// </summary>
// Harmony hooks: src/Integrations/Harmony/RidingPatches.cs (MIGRATION_DECISIONS A5)
internal static class RideInteract
{
    /// What the alt key would do on this troll for this player (RideRules.Decide with the game's facts).
    internal static MountVerdict VerdictFor(Tameable t, Player p)
    {
        var c = t.GetComponent<Character>();
        if (c == null)
        {
            return MountVerdict.NotRideable;
        }

        long rider = RideSession.RiderOf(c);
        bool other = rider != 0L && rider != p.GetPlayerID();
        return RideRules.Decide(true, RideItem.InInventory(p), t.IsTamed(), true, c.IsDead(), other, RideSession.RidingId(), c.GetInstanceID());
    }

    /// Tameable.Interact prefix. Expected outcome: false (the game's own handling skipped) only when the key mounted, dismounted or is blocked by another ride.
    internal static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
    {
        if (!alt || hold || !ModSettings.TrollsOn || !TrollTaming.IsTroll(__instance))
        {
            return true;
        }

        var p = user as Player;
        if (p == null || p != Player.m_localPlayer)
        {
            return true;
        }

        MountVerdict v = VerdictFor(__instance, p);
        ModLog.Verbose($"RideInteract: alt key on {__instance.name} -> {v}");
        var c = __instance.GetComponent<Character>();
        switch (v)
        {
            case MountVerdict.Mount:
                __result = RideSession.Mount(p, c);
                return false;
            case MountVerdict.Dismount:
                __result = RideSession.End(p, EndReason.PlayerDismounted);
                return false;
            case MountVerdict.RidingAnother:
                __result = false;
                return false;
            default:
                return true;
        }
    }

    /// Tameable.GetHoverText postfix: the rename line gives way to the ride line when the key would mount / dismount.
    internal static void Hover(Tameable __instance, ref string __result)
    {
        Player p = Player.m_localPlayer;
        if (p == null || string.IsNullOrEmpty(__result) || !ModSettings.TrollsOn || !TrollTaming.IsTroll(__instance) || !__instance.IsTamed())
        {
            return;
        }

        MountVerdict v = VerdictFor(__instance, p);
        if (!RideRules.PromptVisible(v))
        {
            return;
        }

        string key = Localization.instance.Localize("[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] ");
        string line = key + (v == MountVerdict.Mount ? "Ride" : "Get off");
        string rename = Localization.instance.Localize("$hud_rename");
        string[] lines = __result.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (rename.Length > 0 && lines[i].Contains(rename))
            {
                lines[i] = line;
                __result = string.Join("\n", lines);
                return;
            }
        }

        __result += $"\n{line}";
    }
}
