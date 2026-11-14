using System.Globalization;
using System.Reflection;
using HarmonyLib;
using KydlCore.Common;
using UnityEngine;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     2026-11-14 (KydlFlat world template, user: "build cliffs in-test"): the test world is flat within 160 m of the lab start, so the cliff tests no longer find a cliff
///     in the loaded terrain (run kydlflat-stt-rerun7: cliff-walk-high-down alone found "none within 120 m"; in the full run they passed only on natural terrain that earlier
///     tests had loaded beyond the flat edge). stt.buildCliff writes a known cliff into the terrain instead: the same place, shape and height every run.
/// </summary>
internal static partial class CliffTestHooks
{
    private static readonly AccessTools.FieldRef<TerrainComp, float[]> LevelDelta = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_levelDelta");
    private static readonly AccessTools.FieldRef<TerrainComp, float[]> SmoothDelta = AccessTools.FieldRefAccess<TerrainComp, float[]>("m_smoothDelta");
    private static readonly AccessTools.FieldRef<TerrainComp, bool[]> ModifiedHeight = AccessTools.FieldRefAccess<TerrainComp, bool[]>("m_modifiedHeight");
    private static readonly AccessTools.FieldRef<TerrainComp, bool> CompInitialized = AccessTools.FieldRefAccess<TerrainComp, bool>("m_initialized");
    private static readonly AccessTools.FieldRef<TerrainComp, int> CompOperations = AccessTools.FieldRefAccess<TerrainComp, int>("m_operations");
    private static readonly MethodInfo CompSave = AccessTools.Method(typeof(TerrainComp), "Save");

    /// The cliff's foot is this far in front (towards -z) of the player on the arena spot.
    private const float CliffAhead = 20f;

    /// Width of the raised block's flat top (across the walk) and how far it runs on behind the rise: the hub stands HubBeyond (70 m) past the top point, so it must be on the block.
    private const float TopWidth = 20f, TopDepth = 85f;

    /// The longest rise of any kind (mountain): every build writes this whole area, so a smaller cliff never keeps the remains of a bigger one built before it in the session.
    private const float LongestRun = 25f;

    /// Builds a cliff of the given KIND into the terrain CliffAhead m in front of the player (towards -z), on the ground height there: a raised block whose front face is the cliff.
    /// "small" 3.2 m over 3 m, "medium" 7 m over 6 m, "high" 12 m over 8 m, "mountain" 20 m over 25 m (a slope of 0.8). The rest of the area is set back to the ground height.
    /// Sets the cliff's foot and top like stt.findCliffKind, so stt.teleportToCliff / stt.spawnAtCliff / stt.addHubAtCliff work on it.
    /// Heights above 8 m need KydlTools' lifted terrain limit (the game clamps a level change to 8 m when it applies the terrain); the top is measured after the build.
    /// Expected outcome: "bottom x,z | top x,z | height m" or "none (...)" when the measured top is not the wanted height.
    [TestHook("stt.buildCliff")]
    private static string BuildCliff(string kind)
    {
        Player p = Player.m_localPlayer;
        ZoneSystem zs = ZoneSystem.instance;
        if (p == null || zs == null)
        {
            return "none (no world)";
        }

        float rise, run;
        switch (kind)
        {
            case "small": rise = 3.2f; run = 3f; break;
            case "high": rise = 12f; run = 8f; break;
            case "mountain": rise = 20f; run = 25f; break;
            default: rise = 7f; run = 6f; break;
        }

        Vector3 dir = Vector3.back, side = Vector3.right;
        Vector3 foot = p.transform.position + dir * CliffAhead;
        float ground = zs.GetGroundHeight(foot - dir * 6f);
        float maxAlong = LongestRun * 2f + TopDepth + 2f, maxAcross = TopWidth * 0.5f + LongestRun + 2f;
        int maps = 0, vertices = 0, notReady = 0;
        foreach (Heightmap h in Heightmap.GetAllHeightmaps())
        {
            if (h == null || h.IsDistantLod)
            {
                continue;
            }

            Vector3 o = h.transform.position;
            float half = h.m_width * h.m_scale * 0.5f;
            Vector3 rel = o - foot;
            float along = Vector3.Dot(rel, dir), across = Mathf.Abs(Vector3.Dot(rel, side));
            if (along < -2f - half || along > maxAlong + half || across > maxAcross + half)
            {
                continue;
            }

            TerrainComp comp = TerrainComp.FindTerrainCompiler(o) ?? h.GetAndCreateTerrainCompiler();
            if (comp == null || !CompInitialized(comp))
            {
                notReady++;
                continue;
            }

            float[] level = LevelDelta(comp), smooth = SmoothDelta(comp);
            bool[] modified = ModifiedHeight(comp);
            int n = h.m_width + 1, mid = h.m_width / 2, written = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float x = o.x + (j - mid) * h.m_scale, z = o.z + (i - mid) * h.m_scale;
                    var v = new Vector3(x - foot.x, 0f, z - foot.z);
                    float a = Vector3.Dot(v, dir), c = Mathf.Abs(Vector3.Dot(v, side));
                    if (a < -2f || a > maxAlong || c > maxAcross)
                    {
                        continue;
                    }

                    int k = i * n + j;
                    float generated = o.y + h.GetHeight(j, i) - level[k] - smooth[k];
                    level[k] = ground + CliffUp(a, c, rise, run) - generated;
                    smooth[k] = 0f;
                    modified[k] = true;
                    written++;
                }
            }

            if (written == 0)
            {
                continue;
            }

            CompOperations(comp) = CompOperations(comp) + 1; // the game's duplicate check keeps the TerrainComp with more operations
            CompSave.Invoke(comp, new object[] { false });
            h.Poke(0, false);
            if (ClutterSystem.instance != null)
            {
                ClutterSystem.instance.ResetGrass(o, half);
            }

            maps++;
            vertices += written;
        }

        _bottom = foot - dir * 2f;
        _bottom.y = ground;
        _top = foot + dir * (run + 2f);
        _top.y = ground + rise;
        float measured = MeasuredHeight(_top) - MeasuredHeight(_bottom);
        string where = _bottom.x.ToString("F0", CultureInfo.InvariantCulture) + "," + _bottom.z.ToString("F0", CultureInfo.InvariantCulture) + " | " +
                       _top.x.ToString("F0", CultureInfo.InvariantCulture) + "," + _top.z.ToString("F0", CultureInfo.InvariantCulture) + " | " + rise.ToString("F1", CultureInfo.InvariantCulture) + " m";
        ModLog.Info("CliffTestHooks: built a " + kind + " cliff " + where + " on ground " + ground.ToString("F2") + " m - " + maps + " heightmap(s), " + vertices + " vertices, " + notReady +
                    " not ready; measured rise " + measured.ToString("F2") + " m");
        ModLog.Check("CliffTestHooks: measured rise of the built " + kind + " cliff", measured, rise, 0.3, "m");
        if (Mathf.Abs(measured - rise) > 0.3f)
        {
            return "none (built a " + kind + " cliff but measured a rise of " + measured.ToString("F2", CultureInfo.InvariantCulture) + " m instead of " +
                   rise.ToString("F1", CultureInfo.InvariantCulture) + " m - " + notReady + " heightmap(s) not ready; above 8 m the KydlTools terrain limit must be lifted)";
        }

        return where;
    }

    /// Height above the ground of the cliff at `along` m past its foot (along -z) and `across` m from its middle line: a rise over `run` m at the front, a flat top TopWidth wide and
    /// TopDepth deep, and the same slope down at the back and the sides. Expected outcome: 0 .. rise.
    private static float CliffUp(float along, float across, float rise, float run)
    {
        float front = Mathf.Clamp01(along / run);
        float back = Mathf.Clamp01((run + TopDepth + run - along) / run);
        float sides = Mathf.Clamp01((TopWidth * 0.5f + run - across) / run);
        return rise * Mathf.Min(front, Mathf.Min(back, sides));
    }

    /// The terrain height at `at` read from the heightmap itself (rebuilt by Poke), not a ray. Expected outcome: metres (NaN without a heightmap there).
    private static float MeasuredHeight(Vector3 at)
    {
        Heightmap h = Heightmap.FindHeightmap(at);
        return h != null && h.GetWorldHeight(at, out float y) ? y : float.NaN;
    }
}
