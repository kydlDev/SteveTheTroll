using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Movement.Infrastructure;

/// <summary>
///     D-176 diagnostics (fix8: guard-mob-kills, a troll whose real speed was 0.0 m/s for 15 s with MoveTo called every step, "nothing in the straight line"). Says WHY the game
///     may not be moving a troll that is told to walk: who owns it, whether the game's navmesh has a path from here to the goal (BaseAI.MoveTo uses it), how steep the ground
///     under it is, whether it stands on the ground and which way the game has it moving. Only reads; never changes behaviour.
/// </summary>
internal static class TrollMoveDiag
{
    private static readonly string[] GroundLayers = { "terrain", "Default", "static_solid", "Default_small" };

    private static readonly System.Reflection.FieldInfo FindResult = HarmonyLib.AccessTools.Field(typeof(BaseAI), "m_lastFindPathResult");
    private static readonly System.Reflection.FieldInfo FindTime = HarmonyLib.AccessTools.Field(typeof(BaseAI), "m_lastFindPathTime");
    private static readonly System.Reflection.FieldInfo FindTarget = HarmonyLib.AccessTools.Field(typeof(BaseAI), "m_lastFindPathTarget");

    /// One line of facts about the troll's movement towards `goal`. Expected outcome: the text (never throws).
    internal static string Describe(Character body, Vector3 goal)
    {
        if (body == null)
        {
            return "movement unknown (no character)";
        }

        string owner = "no network view";
        ZNetView view = body.GetComponent<ZNetView>();
        if (view != null && view.IsValid())
        {
            owner = view.IsOwner() ? "owner here" : "OWNED BY ANOTHER PEER";
        }

        string path = "navmesh path unknown";
        BaseAI ai = body.GetComponent<BaseAI>();
        if (ai != null && Pathfinding.instance != null)
        {
            bool have = Pathfinding.instance.HavePath(body.transform.position, goal, ai.m_pathAgentType);
            path = "navmesh path to the goal " + (have ? "exists" : "NONE") + " (agent " + ai.m_pathAgentType + ")";
            // 2026-11-07: the goal as given may be above or below the ground (the hub piece, a waypoint at the troll's own height) - is there a path to the ground under it?
            ZoneSystem zs = ZoneSystem.instance;
            if (zs != null && zs.IsZoneLoaded(ZoneSystem.GetZone(goal)))
            {
                var onGround = new Vector3(goal.x, zs.GetGroundHeight(goal), goal.z);
                path += ", goal " + (goal.y - onGround.y).ToString("F1") + " m above the ground, path to the ground under it " + (Pathfinding.instance.HavePath(body.transform.position, onGround, ai.m_pathAgentType) ? "exists" : "NONE");
            }
        }

        string ground = "ground not found";
        RaycastHit hit;
        if (Physics.Raycast(body.transform.position + Vector3.up * 2f, Vector3.down, out hit, 5f, LayerMask.GetMask(GroundLayers)))
        {
            ground = "ground slope " + Vector3.Angle(hit.normal, Vector3.up).ToString("F0") + " deg (" + hit.collider.name + ")";
        }

        // 2026-11-07: the game's own path-search memory (BaseAI.FindPath keeps the last answer and does not ask again for ~5 s when the same target failed) - read-only, by name
        string lastFind = "path search memory unknown";
        if (ai != null && FindResult != null && FindTime != null && FindTarget != null)
        {
            lastFind = "game path search: last answer " + (bool)FindResult.GetValue(ai) + " " + (Time.time - (float)FindTime.GetValue(ai)).ToString("F1") + " s ago, for a target " +
                       Vector3.Distance((Vector3)FindTarget.GetValue(ai), goal).ToString("F1") + " m from this goal";
        }

        path += ", " + lastFind;
        string physics = "no rigidbody";
        Rigidbody rb = body.GetComponent<Rigidbody>();
        if (rb != null)
        {
            physics = "rigidbody velocity " + rb.linearVelocity.ToString("F1") + (rb.isKinematic ? ", KINEMATIC" : "") + (rb.IsSleeping() ? ", asleep" : "");
        }

        string brain = ai == null ? "no AI" : "AI sleeping " + ai.IsSleeping() + ", alerted " + ai.IsAlerted();
        return owner + ", " + path + ", " + ground + ", on ground " + body.IsOnGround() + ", move dir " + body.GetMoveDir().ToString("F1") + ", running " + body.IsRunning() +
               ", " + physics + ", " + brain + ", dead " + body.IsDead() + ", encumbered " + body.IsEncumbered() + ", time scale " + Time.timeScale.ToString("F2");
    }
}
