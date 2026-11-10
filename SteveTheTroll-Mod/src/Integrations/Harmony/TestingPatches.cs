using HarmonyLib;
using SteveTheTroll.Testing.TestHooks;
using UnityEngine;

namespace SteveTheTroll.Integrations.Harmony;
// MIGRATION_DECISIONS A5: every Harmony patch of the Testing feature, declared here; each one only forwards to the feature's own method
// (the logic stays in the feature). PatchAll in Plugin.Awake applies them; PatchTargetsTests checks the targets still exist in the game.

// TreeDamageLog (Testing/TestHooks/TreeDamageLog.cs)
[HarmonyPatch(typeof(TreeBase), "RPC_Damage")]
internal static class TreeDamageLog_Patch
{
    private static void Prefix(TreeBase __instance, HitData hit)
    {
        TreeDamageLog.Prefix(__instance, hit);
    }
}

// CombatTestHooks.HoldStill (Testing/TestHooks/CombatTestHooks.Part2.cs) - after every other UpdateAI postfix (TrollFarming steers in one)
[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
internal static class CombatTestHooks_HoldStill
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(MonsterAI __instance)
    {
        CombatTestHooks.HoldStill(__instance);
    }
}

// MobSpawnBlock (Testing/TestHooks/MobSpawnBlock.cs) - skips the game's own creature spawns near the player while a test has armed it (stt.keepMobsClear); the prefix returns false = no spawn
[HarmonyPatch(typeof(SpawnSystem), "Spawn")]
internal static class MobSpawnBlock_Patch
{
    private static bool Prefix(SpawnSystem.SpawnData critter, Vector3 spawnPoint)
    {
        return MobSpawnBlock.Allows(critter, spawnPoint);
    }
}

// MobSpawnBlock.ReportAppeared (D-207 diagnostics): while the block is armed, every creature that comes into being within the radius is logged with the call chain that created it
[HarmonyPatch(typeof(Character), "Awake")]
internal static class MobSpawnBlock_Appeared
{
    private static void Postfix(Character __instance)
    {
        MobSpawnBlock.ReportAppeared(__instance);
    }
}

// RecoveryTestHooks.CountWallHits (Testing/TestHooks/RecoveryTestHooks.cs)
[HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
internal static class RecoveryTestHooks_CountWallHits
{
    private static void Prefix(WearNTear __instance, HitData hit)
    {
        RecoveryTestHooks.CountWallHits.Prefix(__instance, hit);
    }
}
