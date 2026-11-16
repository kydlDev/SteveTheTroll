using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Movement.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Domain;

namespace SteveTheTroll.Benchmarks;

/// <summary>
///     Coverage suite (skill `microbenchmarking`, use case 1): the game-free rules that run per tick and per troll. Comparison axis: scale (the number of
///     chests / goals) and, after a change, builds (before / after). Inputs are seeded and kept in fields so the JIT cannot fold them.
/// </summary>
[MemoryDiagnoser]
public class DepotPlannerBenchmarks
{
    private List<DepotCandidate> _chests;

    [Params(8, 64, 512)]
    public int Chests { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(1234);
        _chests = new List<DepotCandidate>(Chests);
        for (int i = 0; i < Chests; i++)
        {
            _chests.Add(new DepotCandidate(random.Next(3) > 0, random.Next(4) == 0, random.Next(5) == 0, (float)(random.NextDouble() * 60.0)));
        }
    }

    [Benchmark]
    public int Choose()
    {
        DepotPlan plan = DepotPlanner.Choose(_chests);
        return plan.Chosen + plan.Skipped;
    }
}

[MemoryDiagnoser]
public class TargetSelectionBenchmarks
{
    private readonly float[] _ranges = { 30f, 80f, 150f };
    private int[] _goals;
    private Func<float, int, bool> _findLast;
    private Func<float, int, bool> _findFirst;

    [Params(10, 100, 1000)]
    public int Goals { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _goals = new int[Goals];
        for (int i = 0; i < Goals; i++)
        {
            _goals[i] = i;
        }

        int last = Goals - 1;
        _findLast = (range, goal) => range > 100f && goal == last;
        _findFirst = (range, goal) => true;
    }

    /// Worst case: nothing in the first ranges, the last goal of the last range wins.
    [Benchmark(Baseline = true)]
    public int FirstHit_LastGoalWins()
    {
        return TargetSelection.FirstHit(_ranges, _goals, _findLast).IsSuccess ? 1 : 0;
    }

    /// Best case: the first goal of the first range wins.
    [Benchmark]
    public int FirstHit_FirstGoalWins()
    {
        return TargetSelection.FirstHit(_ranges, _goals, _findFirst).IsSuccess ? 1 : 0;
    }
}

public class ExploreRulesBenchmarks
{
    private Func<float, float, bool> _blockedNone;
    private Func<float, float, bool> _blockedMost;
    private float _px, _pz, _hx, _hz, _heading, _leash;

    [GlobalSetup]
    public void Setup()
    {
        _px = 310f;
        _pz = -120f;
        _hx = 0f;
        _hz = 0f;
        _heading = 20f;
        _leash = 950f;
        _blockedNone = (x, z) => false;
        _blockedMost = (x, z) => x > 0f;
    }

    [Benchmark(Baseline = true)]
    public float Next_NothingBlocked()
    {
        float tx, tz, used;
        return ExploreRules.Next(_px, _pz, _hx, _hz, _heading, _leash, _blockedNone, out tx, out tz, out used) ? tx + tz + used : 0f;
    }

    [Benchmark]
    public float Next_MostDirectionsBlocked()
    {
        float tx, tz, used;
        return ExploreRules.Next(_px, _pz, _hx, _hz, _heading, _leash, _blockedMost, out tx, out tz, out used) ? tx + tz + used : 0f;
    }
}

public class StuckRulesBenchmarks
{
    private List<float> _metres;
    private float _angle, _ix, _iz;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(99);
        _metres = new List<float>();
        for (int i = 0; i < 24; i++)
        {
            _metres.Add((float)(random.NextDouble() * 12.0));
        }

        _angle = 37f;
        _ix = 0.6f;
        _iz = 0.8f;
    }

    [Benchmark]
    public int PickCandidate()
    {
        return StuckRules.PickCandidate(_metres);
    }

    [Benchmark]
    public float Rotate()
    {
        float dx, dz;
        StuckRules.Rotate(_ix, _iz, Side.Left, _angle, out dx, out dz);
        return dx + dz;
    }
}

public class ProgressMeterBenchmarks
{
    private ProgressMeter _meter;
    private float _distance;

    [GlobalSetup]
    public void Setup()
    {
        _meter = new ProgressMeter();
        _distance = 120f;
        _meter.Reset(_distance);
    }

    /// One call per tick of a travelling troll; the distance shrinks slowly and starts over, as a walk does.
    [Benchmark]
    public float Update()
    {
        _distance = _distance > 1f ? _distance - 0.01f : 120f;
        _meter.Update(_distance, 0.1f, true);
        return _meter.StuckSeconds;
    }
}

public class PathRulesBenchmarks
{
    private string[] _prefabs;
    private int _next;
    private float _metres, _angle;

    [GlobalSetup]
    public void Setup()
    {
        _prefabs = new[] { "Birch1", "stone_wall_2x1", "Rock_3", "Greydwarf", "wood_floor", "Pickable_Stone" };
        _metres = 4.5f;
        _angle = 25f;
    }

    [Benchmark]
    public bool PushThrough()
    {
        _next = (_next + 1) % _prefabs.Length;
        return PathRules.PushThrough(_prefabs[_next], true, _next % 2 == 0, _next % 3 == 0, _next % 4 == 0, _next % 5 == 0);
    }

    [Benchmark]
    public float FeelerScore()
    {
        return PathRules.FeelerScore(_metres, _angle, false);
    }
}
