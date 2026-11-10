using System.Collections.Generic;
using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     2026-11-09 (g16a-3, instruction-latency): TEST ONLY. stt.clearMobs removes the wild creatures that are there at that moment; the game's SpawnSystem keeps spawning
///     (Boar, Deer, Greyling ... a few seconds after the arena was verified clean) and a troll that has a target creature fights it before it follows any order (by design,
///     FarmGate: YieldToFight comes before GoHome). This component sits on the local player (armed by stt.keepMobsClear, removed by stt.reset) and twice a second removes
///     every wild creature within the radius of the player, and logs what it removed so a lab run shows it. Tests that spawn creatures themselves (stt.spawnMob) must not arm it.
/// </summary>
internal sealed class MobClearWatch : MonoBehaviour
{
    private const float PeriodSeconds = 0.5f;
    private readonly List<string> _names = new();
    private float _expires;
    private float _next;
    private float _radius;

    /// Wild creatures removed since the watch was armed.
    internal int Removed { get; private set; }

    /// Puts the watch on the player (a second call only changes the radius) and sweeps once at once. Expected outcome: the number removed by that first sweep.
    internal static int Arm(Player player, float radius)
    {
        var watch = player.GetComponent<MobClearWatch>();
        if (watch == null)
        {
            watch = player.gameObject.AddComponent<MobClearWatch>();
        }

        watch._radius = radius;
        watch._expires = Time.time + MobSpawnBlock.MaxSeconds;
        return watch.Sweep();
    }

    /// Takes the watch off the player (stt.reset). Expected outcome: nothing is removed any more.
    internal static void Disarm(Player player)
    {
        var watch = player.GetComponent<MobClearWatch>();
        if (watch != null)
        {
            Destroy(watch);
        }
    }

    private void Update()
    {
        if (Time.time > _expires)
        {
            // analysis 2026-11-09, 8.2: a watch that deletes creatures must end by itself when the test that armed it did not reach stt.reset (failed, aborted)
            ModLog.Info($"MobClearWatch: expired after {MobSpawnBlock.MaxSeconds:F0} s without stt.reset - {Removed} creature(s) removed in all");
            Destroy(this);
            return;
        }

        if (Time.time < _next)
        {
            return;
        }

        _next = Time.time + PeriodSeconds;
        Sweep();
    }

    private int Sweep()
    {
        _names.Clear();
        Player player = Player.m_localPlayer;
        if (player == null)
        {
            return 0;
        }

        int n = ArenaTestHooks.RemoveWildMobs(player, _radius, _names);
        if (n > 0)
        {
            Removed += n;
            ModLog.Info($"MobClearWatch: {n} wild creature(s) removed within {_radius:F0} m of the player ({string.Join(", ", _names)}; {Removed} since armed)");
        }

        return n;
    }
}
