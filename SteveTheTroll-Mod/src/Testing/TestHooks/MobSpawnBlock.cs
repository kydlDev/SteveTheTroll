using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     2026-11-09 (il-mob1..4, the user: "why does the mob-clearing guard not prevent mobs from spawning?"): TEST ONLY. MobClearWatch removes wild creatures AFTER the game's SpawnSystem
///     has put them there (every sweep, 0.5 s), so a Boar could still exist - and be chosen as a target by a troll - for up to half a second. While this block is armed
///     (stt.keepMobsClear, until stt.reset) SpawnSystem.Spawn (patch MobSpawnBlock_Patch in Integrations/Harmony/TestingPatches.cs) is skipped for every spawn point within
///     the radius of the local player, so the creature is never created. Troll spawns made by the test hooks (Object.Instantiate) do not go through SpawnSystem and are not affected;
///     nothing is armed outside a test that asks for it, so the game and the lab's other tests spawn as usual.
/// </summary>
internal static class MobSpawnBlock
{
    /// The block lifts itself after this long (analysis 2026-11-09, 8.2: "guaranteed cleanup"): stt.reset is the normal end, but a test that fails or is aborted must not leave the game's spawns
    /// off. Longer than the longest test that arms it (instruction-latency: 90 s).
    internal const float MaxSeconds = 180f;

    private static float _radius;
    private static float _until;

    /// Spawns skipped since the block was armed.
    internal static int Blocked { get; private set; }

    internal static bool Active => _radius > 0f && Time.time < _until;

    /// Starts skipping spawns within `radius` m of the local player for MaxSeconds (a second call changes the radius and starts the time again). Expected outcome: Active.
    internal static void Arm(float radius)
    {
        if (!Active)
        {
            Blocked = 0;
            _reports = 0;
        }

        _radius = radius;
        _until = Time.time + MaxSeconds;
    }

    /// Stops skipping spawns (stt.reset). Expected outcome: not Active.
    internal static void Disarm()
    {
        _radius = 0f;
    }

    private const int MaxReports = 40;
    private static int _reports;

    /// D-207 diagnostics (ledger-readback / ledge-forced-medium-up: a wild creature stood in the arena although the spawn block was armed and nothing was logged as skipped): while the block is
    /// armed, logs every non-player creature that comes into being within the radius of the player - name, spot, distance, frame and the managed call chain that created it (SpawnSystem.Spawn,
    /// CreatureSpawner, SpawnArea, ZNetScene.CreateObject for a stored ZDO, a test hook ...). At most MaxReports lines per arming. Expected outcome: a log line or nothing.
    internal static void ReportAppeared(Character c)
    {
        Player player = Player.m_localPlayer;
        if (!Active || player == null || c == null || c.IsPlayer() || _reports >= MaxReports)
        {
            return;
        }

        Vector3 d = c.transform.position - player.transform.position;
        d.y = 0f;
        if (d.magnitude > _radius)
        {
            return;
        }

        _reports++;
        var frames = new System.Text.StringBuilder();
        var trace = new System.Diagnostics.StackTrace(2, false);
        int shown = 0;
        foreach (System.Diagnostics.StackFrame f in trace.GetFrames() ?? new System.Diagnostics.StackFrame[0])
        {
            System.Reflection.MethodBase m = f.GetMethod();
            if (m == null)
            {
                continue;
            }

            if (shown++ > 0)
            {
                frames.Append(" <- ");
            }

            frames.Append(m.DeclaringType?.Name).Append('.').Append(m.Name);
            if (shown >= 9)
            {
                break;
            }
        }

        ModLog.Info($"MobSpawnBlock: creature appeared while armed: {c.name} at {c.transform.position:F0}, {d.magnitude:F0} m from the player, frame {Time.frameCount}, created by {frames}");
    }

    /// The patch's decision for one spawn. Expected outcome: false = the game must not spawn it (armed, a local player, the point within the radius of the player); true otherwise.
    internal static bool Allows(SpawnSystem.SpawnData critter, Vector3 spawnPoint)
    {
        Player player = Player.m_localPlayer;
        if (!Active || player == null)
        {
            return true;
        }

        Vector3 d = spawnPoint - player.transform.position;
        d.y = 0f;
        if (d.magnitude > _radius)
        {
            return true;
        }

        Blocked++;
        ModLog.Info($"MobSpawnBlock: spawn of {(critter?.m_prefab != null ? critter.m_prefab.name : "?")} at {spawnPoint:F0} skipped, {d.magnitude:F0} m from the player ({Blocked} since armed)");
        return false;
    }
}
