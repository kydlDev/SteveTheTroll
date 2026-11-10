using System.Collections.Generic;
using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     #29 (agent 24 attachment P2, user 2026-11-09 "do #29 and #30"): TEST ONLY. stt.clearRoute checks terrain and water only; the trees, stumps, logs, rocks and bushes
///     on the way were left to chance, and a troll walking its 120 m way home chopped one in its stuck rage (full11: a beech, whose loose Wood then landed in the depot).
///     The zones near the hub are not loaded when the test starts, so one clearing at the start cannot reach them. This component sits on the local player
///     (armed by stt.keepRouteClear, removed by stt.reset) and once a second removes every farm source - what the troll could target, TrollFarming.Gather - within
///     the half-width of the line from the player to the arena spot (the hub), in whatever part of that line is loaded at that moment. Player-built pieces are no farm
///     sources and are never touched.
/// </summary>
internal sealed class RouteClearWatch : MonoBehaviour
{
    private const float PeriodSeconds = 1f;
    private const float MaxSeconds = 600f; // ends by itself when the test did not reach stt.reset (failed, aborted); longer than the longest far-troll test
    private readonly List<TrollFarming.Candidate> _found = new();
    private readonly HashSet<ZNetView> _gone = new();
    private float _expires;
    private float _halfWidth;
    private float _next;

    /// Sources removed since the watch was armed.
    internal int Removed { get; private set; }

    /// Puts the watch on the player (a second call only changes the half-width) and sweeps once at once. Expected outcome: the number removed by that first sweep.
    internal static int Arm(Player player, float halfWidth)
    {
        var watch = player.GetComponent<RouteClearWatch>();
        if (watch == null)
        {
            watch = player.gameObject.AddComponent<RouteClearWatch>();
        }

        watch._halfWidth = halfWidth;
        watch._expires = Time.time + MaxSeconds;
        return watch.Sweep();
    }

    /// Takes the watch off the player (stt.reset). Expected outcome: nothing is swept any more.
    internal static void Disarm(Player player)
    {
        var watch = player.GetComponent<RouteClearWatch>();
        if (watch != null)
        {
            Destroy(watch);
        }
    }

    /// Flat distance of `point` from the segment a-b.
    internal static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 p = new(point.x, point.z), s = new(a.x, a.z), e = new(b.x, b.z);
        Vector2 line = e - s;
        float length2 = line.sqrMagnitude;
        float t = length2 < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - s, line) / length2);
        return (p - (s + line * t)).magnitude;
    }

    private void Update()
    {
        if (Time.time > _expires)
        {
            ModLog.Info($"RouteClearWatch: expired after {MaxSeconds:F0} s without stt.reset - {Removed} farm source(s) removed in all");
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
        if (!ArenaTestHooks.Known)
        {
            return 0;
        }

        Vector3 from = transform.position, to = ArenaTestHooks.Spot;
        Vector3 middle = (from + to) * 0.5f;
        float reach = Vector3.Distance(from, to) * 0.5f + _halfWidth + 1f;
        TrollFarming.Gather(middle, reach, _found);
        _gone.Clear();
        int n = 0;
        foreach (TrollFarming.Candidate c in _found)
        {
            if (c.G == null || DistanceToSegment(c.G.transform.position, from, to) > _halfWidth)
            {
                continue;
            }

            ZNetView view = c.G.GetComponentInParent<ZNetView>();
            if (view == null || !view.IsValid() || !_gone.Add(view))
            {
                continue;
            }

            view.ClaimOwnership();
            ZNetScene.instance.Destroy(view.gameObject);
            n++;
        }

        if (n > 0)
        {
            Removed += n;
            ModLog.Info($"RouteClearWatch: {n} farm source(s) removed within {_halfWidth:F0} m of the way to the hub ({Removed} since armed)");
        }

        return n;
    }
}
