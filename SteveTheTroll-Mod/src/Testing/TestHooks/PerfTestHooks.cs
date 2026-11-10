using System.Collections.Generic;
using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     User 2026-11-10 ("I want this mod to reach at least 140 fps on all high ... vsync on, debug log off ... getting 90 fps is too bad"; about 160 fps without the mod):
///     a frame meter the lab can start and read, so the cost of the mod's work is a NUMBER per scene - the same scene first without trolls (the baseline), then with
///     working trolls, then while riding. Inert until a test calls stt.perfStart (the counter object exists only between perfStart and perfStop / the reset).
/// </summary>
internal static class PerfTestHooks
{
    private static PerfMeter _meter;
    private static float _baselineFps;

    private sealed class PerfMeter : MonoBehaviour
    {
        internal readonly List<float> Frames = new List<float>(16384);
        internal float Elapsed, Worst;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Elapsed += dt;
            Worst = Mathf.Max(Worst, dt);
            if (Frames.Count < Frames.Capacity)
            {
                Frames.Add(dt); // never grows while measuring (no allocation in the measured frames)
            }
        }
    }

    /// Starts (or restarts) the frame meter. Expected outcome: 0 frames counted.
    [TestHook("stt.perfStart")]
    private static int Start()
    {
        if (_meter == null)
        {
            var host = new GameObject("STT_PerfMeter");
            Object.DontDestroyOnLoad(host);
            _meter = host.AddComponent<PerfMeter>();
        }

        _meter.Frames.Clear();
        _meter.Elapsed = 0f;
        _meter.Worst = 0f;
        ModLog.Info($"PerfTestHooks: frame meter started - vsync count {QualitySettings.vSyncCount}, target frame rate {Application.targetFrameRate}, display {Screen.currentResolution.width}x{Screen.currentResolution.height} at {Screen.currentResolution.refreshRateRatio.value:F0} Hz, window {Screen.width}x{Screen.height}, quality level {QualitySettings.GetQualityLevel()} ({QualitySettings.names[QualitySettings.GetQualityLevel()]}), debug log {(ModLog.DebugOn ? "ON (its lines cost frames)" : "off")}");
        return _meter.Frames.Count;
    }

    private static float AverageFps() => _meter != null && _meter.Elapsed > 0f ? _meter.Frames.Count / _meter.Elapsed : 0f;

    /// The frames-per-second under which the slowest 1 % of the measured frames ran.
    private static float OnePercentLowFps()
    {
        if (_meter == null || _meter.Frames.Count == 0)
        {
            return 0f;
        }

        var sorted = new List<float>(_meter.Frames);
        sorted.Sort();
        float slow = sorted[Mathf.Min(sorted.Count - 1, Mathf.FloorToInt(sorted.Count * 0.99f))];
        return slow > 0f ? 1f / slow : 0f;
    }

    /// Reads the meter: the average fps since stt.perfStart (whole number). `what` names the scene in the log line. Expected outcome: above 0 once frames were counted.
    [TestHook("stt.perfFps")]
    private static int Fps(string what = "")
    {
        float fps = AverageFps();
        ModLog.Info($"PerfTestHooks: {(what.Length > 0 ? what : "measurement")} - average {fps:F1} fps, 1 % low {OnePercentLowFps():F1} fps, worst frame {(_meter != null ? _meter.Worst * 1000f : 0f):F0} ms, {(_meter != null ? _meter.Frames.Count : 0)} frames in {(_meter != null ? _meter.Elapsed : 0f):F1} s, baseline {(_baselineFps > 0f ? _baselineFps.ToString("F1") + " fps" : "not set")}, debug log {(ModLog.DebugOn ? "ON" : "off")}, vsync count {QualitySettings.vSyncCount}");
        return Mathf.RoundToInt(fps);
    }

    /// Stores the current average as the baseline (the scene without working trolls). Expected outcome: the stored fps.
    [TestHook("stt.perfBaseline")]
    private static int Baseline()
    {
        _baselineFps = AverageFps();
        ModLog.Info($"PerfTestHooks: baseline stored - {_baselineFps:F1} fps");
        return Mathf.RoundToInt(_baselineFps);
    }

    /// The current average as a percentage of the stored baseline (100 = as fast as the scene without working trolls; -1 = no baseline). `what` names the scene in the log.
    [TestHook("stt.perfPercentOfBaseline")]
    private static int PercentOfBaseline(string what = "")
    {
        float fps = AverageFps();
        int percent = _baselineFps > 0f ? Mathf.RoundToInt(fps / _baselineFps * 100f) : -1;
        ModLog.Info($"PerfTestHooks: {(what.Length > 0 ? what : "measurement")} - {fps:F1} fps = {percent} % of the baseline {_baselineFps:F1} fps (1 % low {OnePercentLowFps():F1} fps, worst frame {(_meter != null ? _meter.Worst * 1000f : 0f):F0} ms)");
        return percent;
    }

    /// Removes the meter. Expected outcome: true when none is left.
    [TestHook("stt.perfStop")]
    private static bool Stop()
    {
        if (_meter != null)
        {
            Object.Destroy(_meter.gameObject);
            _meter = null;
        }

        return _meter == null;
    }
}
