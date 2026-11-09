using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Combat.Infrastructure;

/// <summary>Part 2 of TrollGuard (split out so the class stays under the size limit): reading and clearing the forced target.</summary>
internal static partial class TrollGuard
{
    internal static bool Forget(Character troll)
    {
        return troll != null && Forced.Remove(troll.GetInstanceID());
    }

    /// D-084: the game AI's target is cleared (the troll keeps farming). Expected outcome: whether it had one.
    internal static bool ClearTarget(MonsterAI ai)
    {
        if (ai == null || Target(ai) == null)
        {
            return false;
        }

        Target(ai) = null;
        return true;
    }

    /// The name of the forced target ("" = none). Only TrollGuard (a creature that hunts the owner) and an order (attack key / hit) set one - the game's own AI never does.
    internal static string ForcedName(Character troll)
    {
        Character f;
        return troll != null && Forced.TryGetValue(troll.GetInstanceID(), out f) && f != null ? Utils.GetPrefabName(f.gameObject) : ""; // the prefab name, as stt.targetOf reports it (f.name is "Greydwarf(Clone)")
    }

    /// D-176 (diagnostics): what the troll holds now - the game AI's target, the forced target and the running chase, side by side (a lost target shows which part lost it).
    internal static string Describe(Character troll)
    {
        MonsterAI ai = troll != null ? troll.GetComponent<MonsterAI>() : null;
        Character ait = ai != null ? Target(ai) : null;
        Character f2 = null;
        if (troll != null)
        {
            Forced.TryGetValue(troll.GetInstanceID(), out f2);
        }

        TrollChase chase = troll != null ? troll.GetComponent<TrollChase>() : null;
        return "AI target " + (ait != null ? ait.name : "none") + ", forced target " + (f2 != null ? f2.name : "none") + ", chase " +
               (chase != null && chase.Target != null ? chase.Target.name + " (" + chase.Swings + " swing(s))" : "none") + ", farmer " +
               (troll != null && troll.GetComponent<TrollFarmer>() != null);
    }

    /// D-084: the troll has a forced target from the player's defence (TrollDefend / the attack key). Expected outcome: true when it has one.
    internal static bool HasForced(Character troll)
    {
        Character t;
        return troll != null && Forced.TryGetValue(troll.GetInstanceID(), out t) && t != null && !t.IsDead();
    }
}
