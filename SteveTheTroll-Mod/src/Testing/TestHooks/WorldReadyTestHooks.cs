using KydlCore.Common;
using UnityEngine;
namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
/// D-231 (2026-11-15, runs kydlflat320-stt-full3 / -four): a test step ran before the world around the player was there. After a teleport the game first shows the
/// GENERATED ground and applies the saved terrain (the KydlFlat template's flat 53 m) some frames later, and a ZDO made by a test (the hub) gets its object some frames
/// later too. At 60 fps (monitors off) that took longer than the tests' fixed waits: a troll and a Greydwarf were put on the 46 - 48 m ground and ended up under the
/// 53 m terrain (the attack-key ray hit Terrain at 2.4 m), and the hub's colliders appeared around a troll that was already standing there (it could not walk).
/// The tests now WAIT on these facts instead of on seconds.
/// </summary>
internal static class WorldReadyTestHooks
{
    private const float StableSeconds = 1f;
    private static readonly Vector3[] Probes = { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
    private static string _lastHeights = "";
    private static float _sameSince = -1f;
    private static float _askedSince = -1f;

    /// True when the player's zone and its neighbours are loaded with every saved object created (the game's own ZNetScene.IsAreaReady - terrain compilers and the
    /// test hub included) AND the ground under the player and `radius` m in front / behind / left / right has not changed for 1 s. Poll it with `wait until`.
    /// Expected outcome: true within a few seconds of a teleport.
    [TestHook("stt.worldReady")]
    private static bool WorldReady(float radius = 30f)
    {
        Player p = Player.m_localPlayer;
        if (p == null || ZNetScene.instance == null || ZoneSystem.instance == null)
        {
            return false;
        }

        float now = Time.time;
        if (_askedSince < 0f)
        {
            _askedSince = now;
        }

        Vector3 at = p.transform.position;
        if (!ZNetScene.instance.IsAreaReady(at))
        {
            _lastHeights = "";
            _sameSince = -1f;
            return false;
        }

        string heights = "";
        for (int i = 0; i < Probes.Length; i++)
        {
            heights += ZoneSystem.instance.GetGroundHeight(at + Probes[i] * radius).ToString("F2") + " ";
        }

        if (heights != _lastHeights)
        {
            _lastHeights = heights;
            _sameSince = now;
            return false;
        }

        if (now - _sameSince < StableSeconds)
        {
            return false;
        }

        ModLog.Info("WorldReadyTestHooks: world ready at " + at.ToString("F0") + " after " + (now - _askedSince).ToString("F1") + " s (ground " + heights.Trim() + " m, " +
                    radius.ToString("F0") + " m around)");
        _askedSince = -1f;
        _lastHeights = "";
        _sameSince = -1f;
        return true;
    }

    /// World x / z of the middle of the lake the KydlFlat world template holds (baked by KydlTools' WorldBaker, LakePlan: 200 m north of the lab start (50, 1), a 50 x 50 m
    /// floor 12 m under the water; Claude\shared\TestLab\templates\KydlFlat\bake.txt).
    private const float LakeX = 50f, LakeZ = 201f;

    /// The player is put on the water surface in the middle of the template's baked lake (user, 2026-11-15: a lake in the world instead of searching the random terrain
    /// for water). The zones there are not loaded yet: wait for stt.worldReady, then stt.inDeepWater checks the real depth. Expected outcome: "lake at (x, y, z)".
    [TestHook("stt.toBakedLake")]
    private static string ToBakedLake()
    {
        Player p = Player.m_localPlayer;
        var at = new Vector3(LakeX, ZoneSystem.instance.m_waterLevel + 0.5f, LakeZ);
        p.TeleportTo(at, Quaternion.identity, true);
        ModLog.Info("WorldReadyTestHooks: player sent to the baked lake at " + at.ToString("F0"));
        return "lake at " + at.ToString("F0");
    }

    /// True when the REAL ground under the player (the loaded terrain, not the world generator's height) is at least `depth` m under the water surface. stt.toWater
    /// chooses by the generator's heights; a world template changes the terrain, so the place is checked once it has loaded. Expected outcome: true.
    [TestHook("stt.inDeepWater")]
    private static bool InDeepWater(float depth)
    {
        Player p = Player.m_localPlayer;
        float water = ZoneSystem.instance.m_waterLevel;
        float ground = ZoneSystem.instance.GetGroundHeight(p.transform.position);
        ModLog.Info("WorldReadyTestHooks: real ground under the player " + ground.ToString("F1") + " m, water surface " + water.ToString("F1") + " m, wanted depth " +
                    depth.ToString("F1") + " m");
        return ground < water - depth;
    }
}
