using HarmonyLib;
using SteveTheTroll.Features.Riding.Infrastructure;
using SteveTheTroll.Features.Trolls.Contracts;

namespace SteveTheTroll.Integrations.Harmony;
// T-213: the Harmony patches of the Riding feature; each only forwards to the feature.

// A ridden troll takes no orders from its own AI: the rider steers it (TrollSaddle).
[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
internal static class RidingPatch_SkipAi
{
    // D-216 (user 2026-11-10: "I pressed Shift, nothing happened"): this prefix must run BEFORE TrollFarming.Steer (another UpdateAI prefix). Harmony skips the later prefixes only when an
    // earlier one returns false; with the default priority the farming brain ran first on the ridden troll and called Movement.Stop() (move dir 0, SetRun(false)) every tick, wiping what the saddle had just set.
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(MonsterAI __instance)
    {
        var c = __instance.GetComponent<Character>();
        return !RideSession.IsRidden(c);
    }
}

// T-213: the alternative interact key mounts / dismounts (RideInteract).
// T-213: the game ends the doodad control on any click / dodge (the Lox saddle has no attacks). The troll's attacks are the mouse buttons here (TrollSaddle reads them), so while a troll is
// ridden the clicks are not passed on to the player (jump still gets you off).
[HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
internal static class RidingPatch_Controls
{
    private static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool dodge)
    {
        if (__instance == Player.m_localPlayer && __instance.GetDoodadController() is TrollSaddle)
        {
            attack = false;
            attackHold = false;
            secondaryAttack = false;
            secondaryAttackHold = false;
            dodge = false;
        }
    }
}

[HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
internal static class RidingPatch_Interact
{
    private static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
    {
        return RideInteract.Prefix(__instance, user, hold, alt, ref __result);
    }
}

// T-213: the hover prompt of a tamed troll shows "Ride" / "Get off" only when the key would do that.
[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
internal static class RidingPatch_Hover
{
    private static void Postfix(Tameable __instance, ref string __result)
    {
        RideInteract.Hover(__instance, ref __result);
    }
}

// T-213: the ride ends when the rider dies, the ridden troll dies, or the rider teleports.
[HarmonyPatch(typeof(Player), "OnDeath")]
internal static class RidingPatch_PlayerDeath
{
    private static void Prefix(Player __instance)
    {
        RideSession.End(__instance, SteveTheTroll.Features.Riding.Domain.EndReason.PlayerDied);
    }
}

[HarmonyPatch(typeof(Character), "OnDeath")]
internal static class RidingPatch_TrollDeath
{
    private static void Prefix(Character __instance)
    {
        if (RideSession.IsRidden(__instance))
        {
            RideSession.End(Player.m_localPlayer, SteveTheTroll.Features.Riding.Domain.EndReason.TrollDied);
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
internal static class RidingPatch_Teleport
{
    private static void Prefix(Player __instance)
    {
        RideSession.End(__instance, SteveTheTroll.Features.Riding.Domain.EndReason.Teleported);
    }
}

// T-213 diagnosis: who sets the ridden troll's move direction besides the saddle? Logged (with the caller) while riding, at most once per second.
[HarmonyPatch(typeof(Character), nameof(Character.SetMoveDir))]
internal static class RidingPatch_MoveDirTrace
{
    private static void Prefix(Character __instance, UnityEngine.Vector3 dir)
    {
        if (!RideSession.IsRidden(__instance) || TrollSaddle.Setting)
        {
            return;
        }

        TrollSaddle.ForeignMoveSets++;
        var st = new System.Diagnostics.StackTrace(2, false);
        string who = "";
        for (int i = 0; i < st.FrameCount && i < 5; i++)
        {
            System.Reflection.MethodBase m = st.GetFrame(i).GetMethod();
            who += (i > 0 ? " <- " : "") + m.DeclaringType + "." + m.Name;
        }
        global::KydlCore.Common.ModLog.Every("RidingPatch.moveDirTrace", 1d, $"RidingPatch: the ridden troll's move direction was set to {dir:F2} by {who}");
    }
}

// T-213: while a troll is ridden nothing but the saddle may move, turn or swing it: the mod's own farming / recovery / return-home / guard code drives the troll through these
// game methods, and they are switched off for the ridden troll here (one place instead of every caller). The saddle itself sets Character.SetMoveDir / SetLookDir directly.
internal static class RidingBlock
{
    internal static bool Ridden(BaseAI ai)
    {
        if (ai == null)
        {
            return false;
        }

        var c = ai.GetComponent<Character>();
        return RideSession.IsRidden(c);
    }
}

[HarmonyPatch(typeof(BaseAI), "MoveTo")]
internal static class RidingPatch_BlockMoveTo
{
    private static bool Prefix(BaseAI __instance, ref bool __result)
    {
        if (!RidingBlock.Ridden(__instance))
        {
            return true;
        }

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
internal static class RidingPatch_BlockMoveTowards
{
    private static bool Prefix(BaseAI __instance) => !RidingBlock.Ridden(__instance);
}

[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.StopMoving))]
internal static class RidingPatch_BlockStop
{
    private static bool Prefix(BaseAI __instance) => !RidingBlock.Ridden(__instance);
}

[HarmonyPatch(typeof(BaseAI), "LookAt")]
internal static class RidingPatch_BlockLookAt
{
    private static bool Prefix(BaseAI __instance) => !RidingBlock.Ridden(__instance);
}

[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.LookTowards))]
internal static class RidingPatch_BlockLookTowards
{
    private static bool Prefix(BaseAI __instance) => !RidingBlock.Ridden(__instance);
}

// Swings: only the saddle (the mouse buttons) starts attacks of a ridden troll.
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
internal static class RidingPatch_BlockAttack
{
    private static bool Prefix(Humanoid __instance, ref bool __result)
    {
        if (TrollSaddle.Attacking || !RideSession.IsRidden(__instance))
        {
            return true;
        }

        __result = false;
        return false;
    }
}
