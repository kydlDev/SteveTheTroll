using System.Globalization;
using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     2026-11-14 (user: "keep 100 m of flat area and do cliffs beyond that"): the KydlFlat world template holds the four cliffs itself (small, medium, high, mountain; baked
///     by KydlTools' WorldBaker, see Claude\shared\TestLab\templates\KydlFlat\bake.txt). They stand 119 m south of the lab start (50, 1), rise towards -z and are 100 m apart in x.
///     The cliff tests take their cliff from this table instead of searching the loaded terrain (D-229 revised: it replaces stt.buildCliff, which built the cliff at run time).
/// </summary>
internal static partial class CliffTestHooks
{
    /// World z of every baked cliff's foot (lab start z 1 minus 119 m) and the flat ground height of the template.
    private const float BakedFootZ = -118f, BakedGround = 53f;

    /// Takes the baked cliff of the given KIND as THE cliff of the following hooks (stt.teleportToCliff / stt.spawnAtCliff / stt.addHubAtCliff): bottom = 2 m in front of the foot,
    /// top = the first point of the flat top (Run + 2 m past the foot, towards -z). The heights are not read here (the cliff is 120 m from the arena, its zones are not loaded yet):
    /// stt.bakedCliffOk checks them once the player stands there.
    /// Expected outcome: "bottom x,z | top x,z | height m", or "none (...)" for an unknown kind.
    [TestHook("stt.bakedCliff")]
    private static string BakedCliff(string kind)
    {
        float x, rise, run;
        switch (kind)
        {
            case "small": x = -100f; rise = 3.2f; run = 3f; break;
            case "medium": x = 0f; rise = 7f; run = 6f; break;
            case "high": x = 100f; rise = 12f; run = 8f; break;
            case "mountain": x = 200f; rise = 20f; run = 25f; break;
            default: return "none (unknown cliff kind '" + kind + "')";
        }

        _bottom = new Vector3(x, BakedGround, BakedFootZ + 2f);
        _top = new Vector3(x, BakedGround + rise, BakedFootZ - (run + 2f));
        _bakedRise = rise;
        return Format(_bottom) + " | " + Format(_top) + " | " + rise.ToString("F1", CultureInfo.InvariantCulture) + " m";
    }

    private static float _bakedRise;

    /// After the player stands at the cliff and its zones are loaded: the terrain at the baked bottom and top differs by the baked rise (the template really holds the cliff).
    /// Expected outcome: true; false (with the measured heights in the log) when the world is not the KydlFlat template with cliffs.
    [TestHook("stt.bakedCliffOk")]
    private static bool BakedCliffOk()
    {
        float low = Height(_bottom), high = Height(_top), measured = high - low;
        ModLog.Info("CliffTestHooks: baked cliff bottom " + Format(_bottom) + " ground " + low.ToString("F2") + " m, top " + Format(_top) + " ground " + high.ToString("F2") +
                    " m, measured rise " + measured.ToString("F2") + " m, expected " + _bakedRise.ToString("F2") + " m");
        ModLog.Check("CliffTestHooks: measured rise of the baked cliff", measured, _bakedRise, 0.3, "m");
        return Mathf.Abs(measured - _bakedRise) <= 0.3f;
    }

    private static string Format(Vector3 v)
    {
        return v.x.ToString("F0", CultureInfo.InvariantCulture) + "," + v.z.ToString("F0", CultureInfo.InvariantCulture);
    }

    /// The terrain height at `at` read from the heightmap itself, not a ray. Expected outcome: metres (NaN without a heightmap there).
    private static float Height(Vector3 at)
    {
        Heightmap h = Heightmap.FindHeightmap(at);
        return h != null && h.GetWorldHeight(at, out float y) ? y : float.NaN;
    }
}
