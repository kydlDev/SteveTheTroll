using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>
///     The arena hooks of the suite (D-171 / D-172). Where the player stands comes from the SHARED
///     KydlCore.Common.TestArena (MIGRATION_DECISIONS C26: one lab start
///     per game session, each mod its own lane; SteveTheTroll keeps lane 0 = the spot its long-route tests were tuned on,
///     the flattest dry 40 x 40 m area of
///     16 directions, 70 m from the lab start). This class only adds what SteveTheTroll's tests need on top: going further
///     out, clearing the area / a route,
///     stepping aside, looking at a target, god mode.
/// </summary>
internal static class ArenaTestHooks
{
    private static bool _known;
    private static Vector3 _start, _spot;

    private static readonly AccessTools.FieldRef<Character, float> MaxAirAltitude =
        AccessTools.FieldRefAccess<Character, float>("m_maxAirAltitude");

    private static bool _further;
    private static Vector3 _furtherSpot;

    /// True once the arena spot is known (after the first stt.reset / stt.arena).
    internal static bool Known => _known;

    /// The shared arena spot (lane 0) - where the test hub stands. Only meaningful when Known.
    internal static Vector3 Spot => _spot;

    /// Every test starts here (MIGRATION_DECISIONS C26). Expected outcome: "arena at (x, y, z), 70 m from the lab start, god mode True".
    [TestHook("stt.arena")]
    private static string Arena()
    {
        Player p = Player.m_localPlayer;
        if (p == null)
        {
            return "no player";
        }

        MovePlayer(p);
        return "arena at " + _spot.ToString("F0") + ", " + Flat(p.transform.position - _start).ToString("F0") +
               " m from the lab start, god mode " + p.InGodMode();
    }

    /// Moves the player to the shared arena spot (lane 0), facing world +z, god mode on. Expected outcome: the spot (the player stands there).
    internal static Vector3 MovePlayer(Player p)
    {
        string moved = TestArena.MovePlayer();
        _spot = TestArena.Spot;
        object st = AppDomain.CurrentDomain.GetData(TestArena.StartKey);
        _start = st is Vector3 s0 ? s0 : _spot;
        if (!_known)
        {
            ModLog.Info("ArenaTestHooks: shared arena - lab start " + _start.ToString("F0") + ", our spot " +
                        _spot.ToString("F0"));
        }

        _known = true;
        ModLog.Check("ArenaTestHooks: player on the arena spot", Flat(p.transform.position - _spot), 0f, 0.5f, "m");
        ModLog.Verbose("ArenaTestHooks: " + moved);
        return _spot;
    }

    /// D-171: the player walks (teleports) `metres` further out, AWAY from the lab start along the start -> spot line (a plain dx teleport went back towards the lab house).
    /// The bed stays at the spot. Expected outcome: the new position.
    [TestHook("stt.arenaFurther")]
    private static Vector3 Further(float metres)
    {
        Player p = Player.m_localPlayer;
        if (!_known)
        {
            MovePlayer(p);
        }

        Vector3 dir = _spot - _start;
        dir.y = 0f;
        dir.Normalize();
        Vector3 to = _spot + dir * metres;
        to.y = ZoneSystem.instance.GetGroundHeight(to) + 0.2f;
        _further = true;
        _furtherSpot = to;
        Place(p, to,
            Quaternion.identity); // D-172: same facing as after stt.reset (world +z), so dz > 0 stays "in front"
        ModLog.Info("ArenaTestHooks: player " + metres.ToString("F0") + " m further out at " + to.ToString("F0") +
                    " (stands at " + p.transform.position.ToString("F0") + "), facing +z");
        return p.transform.position;
    }

    /// D-172: removes trees, logs, bushes and rocks of the world within `radius` m of the player (attack-key-creature: a Beech_small2 between the player and the mob took
    /// the ray). Call it BEFORE planting / spawning the test's own objects. Expected outcome: how many were removed.
    [TestHook("stt.clearAround")]
    private static int ClearAround(float radius)
    {
        Player p = Player.m_localPlayer;
        int n = 0;
        foreach (ZNetView v in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
        {
            if (v == null || !v.IsValid())
            {
                continue;
            }

            GameObject g = v.gameObject;
            if (Flat(g.transform.position - p.transform.position) > radius)
            {
                continue;
            }

            bool veg = g.GetComponent<TreeBase>() != null || g.GetComponent<TreeLog>() != null ||
                       g.GetComponent<MineRock>() != null || g.GetComponent<MineRock5>() != null
                       || (g.GetComponent<Destructible>() != null && g.GetComponent<Piece>() == null &&
                           g.GetComponent<Character>() == null);
            if (!veg)
            {
                continue;
            }

            v.ClaimOwnership();
            ZNetScene.instance.Destroy(g);
            n++;
        }

        ModLog.Info("ArenaTestHooks: " + n + " tree(s) / bush(es) / rock(s) removed within " + radius.ToString("F0") +
                    " m of the player");
        return n;
    }

    /// D-175 (run 175732: in spot-return the troll's way from the depot to its remembered spot went through the player, TrollHold froze it - "Bjorn looks at it, 2 m away"
    /// - for minutes): the player steps `dx`,`dz` m aside at once (still facing +z). Expected outcome: the new position.
    [TestHook("stt.stepAside")]
    private static Vector3 StepAside(float dx, float dz)
    {
        Player p = Player.m_localPlayer;
        Vector3 to = p.transform.position + new Vector3(dx, 0f, dz);
        to.y = ZoneSystem.instance.GetGroundHeight(to) + 0.2f;
        Place(p, to, Quaternion.identity);
        ModLog.Info("ArenaTestHooks: player stepped aside to " + to.ToString("F0"));
        return to;
    }

    /// #29: arms RouteClearWatch - once a second every farm source (tree, stump, log, rock, bush) within `halfWidth` m of the line from the player to the arena spot (the hub)
    /// is removed in the part of the line that is loaded at that moment (the zones near the hub load only when the troll gets there). stt.reset takes the watch off.
    /// Call it after stt.clearRoute. Expected outcome: how many sources the first sweep removed.
    [TestHook("stt.keepRouteClear")]
    private static int KeepRouteClear(float halfWidth)
    {
        Player p = Player.m_localPlayer;
        if (!_known)
        {
            MovePlayer(p);
        }

        int n = RouteClearWatch.Arm(p, halfWidth);
        ModLog.Info($"ArenaTestHooks: route kept clear within {halfWidth:F0} m of the way to the hub, first sweep removed {n}");
        return n;
    }

    /// TERRAIN ONLY (D-176: the name promises more than it checks): it samples world-generator heights and water, nothing else - trees, rocks, logs and stumps are NOT checked
    /// (stt.clearAround removes them near the player, but not along the route; stt.keepRouteClear does, #29). A test that needs a walkable route must say what it removes itself.
    /// D-174 (review: "a 400-metre return test should not depend on randomly generated terrain being passable"): of 16 directions from the arena spot, the one whose straight
    /// line of `metres` has the gentlest terrain and no water (WorldGenerator heights - valid where nothing is loaded; sampled every 10 m), and teleports the player to its end
    /// (the game's distant teleport finds the floor). Expected outcome: a description; throws when no direction is dry.
    [TestHook("stt.clearRoute")]
    private static string ClearRoute(float metres)
    {
        Player p = Player.m_localPlayer;
        if (!_known)
        {
            MovePlayer(p);
        }

        WorldGenerator wg = WorldGenerator.instance;
        float water = ZoneSystem.instance.m_waterLevel;
        int best = -1;
        float bestSteep = float.MaxValue;
        for (int k = 0; k < 16; k++)
        {
            float a = k * Mathf.PI * 2f / 16f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float steep = 0f, prev = wg.GetHeight(_spot.x, _spot.z);
            bool wet = false;
            for (float d = 10f; d <= metres + 0.1f; d += 10f)
            {
                Vector3 q = _spot + dir * d;
                float h = wg.GetHeight(q.x, q.z);
                if (h < water + 0.5f)
                {
                    wet = true;
                    break;
                }

                steep = Mathf.Max(steep, Mathf.Abs(h - prev));
                prev = h;
            }

            if (!wet && steep < bestSteep)
            {
                bestSteep = steep;
                best = k;
            }
        }

        if (best < 0)
        {
            throw new Exception("no dry direction of " + metres.ToString("F0") + " m from the arena spot");
        }

        float ang = best * Mathf.PI * 2f / 16f;
        Vector3 to = _spot + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * metres;
        to.y = wg.GetHeight(to.x, to.z) + 1f;
        _further = true;
        _furtherSpot = to;
        p.TeleportTo(to, Quaternion.identity, true);
        string info = "terrain-only route (heights and water; obstacles not checked) " + metres.ToString("F0") + " m, direction " + best + " of 16, steepest 10 m step " +
                      bestSteep.ToString("F1") + " m, player to " + to.ToString("F0");
        ModLog.Info("ArenaTestHooks: " + info);
        return info;
    }

    /// D-173 (run 155357: far-troll-goes-home, 400 m out, the full troll was "not sent to the depot: fighting" - a wild creature there; fighting wins over unloading by
    /// design, D-149): removes wild creatures (not tamed, not players) within `radius` m of the player. Expected outcome: how many were removed.
    [TestHook("stt.clearMobs")]
    internal static int ClearMobs(float radius)
    {
        var names = new List<string>();
        int n = RemoveWildMobs(Player.m_localPlayer, radius, names);
        ModLog.Info("ArenaTestHooks: " + n + " wild creature(s) removed within " + radius.ToString("F0") +
                    " m of the player" + (n > 0 ? " (" + string.Join(", ", names) + ")" : ""));
        return n;
    }

    /// The removal itself, shared by stt.clearMobs and MobClearWatch: wild creatures (not tamed, not players, alive) within `radius` m of `player`; `names` (when given)
    /// receives what was removed. Expected outcome: how many were removed.
    internal static int RemoveWildMobs(Player player, float radius, List<string> names)
    {
        int n = 0;
        foreach (Character c in new List<Character>(Character.GetAllCharacters()))
        {
            if (c == null || c.IsPlayer() || c.IsTamed() || c.IsDead())
            {
                continue;
            }

            if (Flat(c.transform.position - player.transform.position) > radius)
            {
                continue;
            }

            ZNetView v = c.GetComponent<ZNetView>();
            if (v == null || !v.IsValid())
            {
                continue;
            }

            names?.Add(c.name);
            v.ClaimOwnership();
            ZNetScene.instance.Destroy(c.gameObject);
            n++;
        }

        return n;
    }

    /// 2026-11-09 (g16a-3, instruction-latency: the troll walked 11 m AWAY from its hub after the home order): the game's own SpawnSystem put two wild Boars next to the troll
    /// 3 s AFTER stt.clearMobs / stt.wildMobsNear had found the arena clean. A troll with a target creature fights first, by design (D-042 / D-149, FarmGate: YieldToFight before
    /// GoHome), so the order was correctly not followed. The one-time clearing cannot keep the arena clean while the world spawns; this arms MobSpawnBlock (the game's own spawns
    /// within `radius` m of the player are skipped, so the creatures are never created) and MobClearWatch (removes what is there already or gets past, twice a second) until
    /// stt.reset. Only for tests that do not spawn their own creatures (stt.spawnMob). Expected outcome: how many the first sweep removed.
    [TestHook("stt.keepMobsClear")]
    private static int KeepMobsClear(float radius)
    {
        MobSpawnBlock.Arm(radius); // the creatures are not created at all (MobSpawnBlock) ...
        int n = MobClearWatch.Arm(Player.m_localPlayer, radius); // ... and what is there already, or gets past, is removed twice a second
        ModLog.Info($"ArenaTestHooks: arena kept free of wild creatures within {radius:F0} m of the player (game spawns skipped, sweep every 0.5 s), first sweep removed {n}");
        return n;
    }

    /// D-206 (g16c-3, ledge-forced-medium-up: the game's SpawnSystem put a Deer next to the player in the same second stt.clearMobs found the arena clean, so the start's own precondition
    /// stt.wildMobsNear failed with 1): only the game's own creature spawns within `radius` m of the player are skipped (MobSpawnBlock; until stt.reset or 180 s). Nothing is removed and nothing
    /// is swept, so creatures a test spawns itself (stt.spawnMob, stt.spawnWildTroll, ...) are not touched - this is what the shared `start` block arms BEFORE stt.clearMobs. Expected outcome: the radius.
    [TestHook("stt.blockSpawns")]
    private static float BlockSpawns(float radius)
    {
        MobSpawnBlock.Arm(radius);
        ModLog.Info($"ArenaTestHooks: the game's own creature spawns within {radius:F0} m of the player are skipped (until stt.reset or {MobSpawnBlock.MaxSeconds:F0} s)");
        return radius;
    }

    /// D-175: how many living wild creatures (not tamed, not players) are within `radius` m of the player - the precondition a test that needs an enemy-free arena states
    /// explicitly (after stt.clearMobs, which reports what it removed, not what is left). Expected outcome: the count.
    [TestHook("stt.wildMobsNear")]
    private static int WildMobsNear(float radius)
    {
        Player p = Player.m_localPlayer;
        int n = 0;
        foreach (Character c in Character.GetAllCharacters())
        {
            if (c != null && !c.IsPlayer() && !c.IsTamed() && !c.IsDead() &&
                Flat(c.transform.position - p.transform.position) <= radius)
            {
                n++;
                // D-206 (g16d-1 ledger-readback: a creature was found 110 ms after stt.clearMobs removed 0, while MobSpawnBlock was armed and logged no skipped spawn): WHICH creature, where, how old, who owns it -
                // the log line is the only way to tell a creature that came out of a saved ZDO, a spawner or a walk-in from a SpawnSystem spawn
                ZNetView v = c.GetComponent<ZNetView>();
                string age = "?";
                BaseAI ai = c.GetComponent<BaseAI>();
                if (ai != null)
                {
                    age = ai.GetTimeSinceSpawned().TotalSeconds.ToString("F1") + " s";
                }

                ModLog.Info($"ArenaTestHooks: wild creature left within {radius:F0} m: {c.name} at {c.transform.position:F1}, {Flat(c.transform.position - p.transform.position):F1} m from the player, " +
                            $"view valid {v != null && v.IsValid()}, owner {(v != null && v.IsValid() && v.IsOwner())}, since spawn {age}, " +
                            $"ZDO {(v != null && v.IsValid() ? v.GetZDO().m_uid.ToString() : "-")}, block active {MobSpawnBlock.Active} ({MobSpawnBlock.Blocked} skipped), frame {Time.frameCount}");
            }
        }

        return n;
    }

    /// D-172 (run 143014: the lab's face step looks at the tree's ROOT; the real camera ray grazed the ground and the attack key found nothing): the player looks at the
    /// target `height` m above its root - a person aims at the trunk. Only the look direction is set; the attack key stays the real one. Expected outcome: the pitch in degrees.
    [TestHook("stt.lookAt")]
    private static float LookAt(GameObject target, float height)
    {
        Player p = Player.m_localPlayer;
        Vector3 dir = target.transform.position + Vector3.up * height - p.GetEyePoint();
        p.SetLookDir(dir.normalized);
        float pitch = Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        ModLog.Info("ArenaTestHooks: player looks at " + target.name + " " + height.ToString("F1") +
                    " m above its root, " + dir.magnitude.ToString("F1") + " m from the eyes, pitch " +
                    pitch.ToString("F0"));
        return pitch;
    }

    /// D-172 (user: god mode + the player died at stt.arenaFurther): god mode for the test player, on or off (defend-rage switches it off around stt.hitPlayer).
    /// Expected outcome: the god mode afterwards.
    [TestHook("stt.godMode")]
    private static bool GodMode(bool on)
    {
        Player p = Player.m_localPlayer;
        if (p.InGodMode() != on)
        {
            p.SetGodMode(on);
        }

        ModLog.Info("ArenaTestHooks: god mode " + p.InGodMode());
        return p.InGodMode();
    }

    /// D-172: puts the player there AT ONCE. TeleportTo was the bug: its move finishes frames later (after the area loads), so the reset's teleport to the spot landed AFTER
    /// stt.arenaFurther and the tree / troll of the attack-key tests appeared at the bed; and setting the position by hand next to it kept the fall height, so a 9 m step
    /// down counted as a fall (the player died, 2026-11-02). The test spots are near the loaded area, so a direct move is safe; velocity and fall height are reset.
    internal static void Place(Player p, Vector3 to, Quaternion rot)
    {
        Rigidbody body = p.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.position = to;
            body.rotation = rot;
        }

        p.transform.position = to;
        p.transform.rotation = rot;
        p.SetLookDir(rot *
                     Vector3.forward); // the look direction turns the body every frame - set it too, or the facing springs back
        MaxAirAltitude(p) = to.y;
        ZNet.instance.SetReferencePosition(to);
    }

    /// D-172: within `metres` of the arena spot, of the spot stt.arenaFurther went to, or of the player now (stt.teleport tests). False before the first stt.reset.
    internal static bool InArena(Vector3 at, float metres)
    {
        if (!_known)
        {
            return false;
        }

        Player p = Player.m_localPlayer;
        return Flat(at - _spot) <= metres || (_further && Flat(at - _furtherSpot) <= metres) ||
               (p != null && Flat(at - p.transform.position) <= metres);
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }
}
