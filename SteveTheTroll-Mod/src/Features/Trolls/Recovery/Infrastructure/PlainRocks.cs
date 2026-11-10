using System.Collections.Generic;
using SteveTheTroll.Features.Trolls.Recovery.Domain;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Recovery.Infrastructure;

/// <summary>
///     D-214 (user 2026-11-10: in the log trolls stuck behind boulders smashed them - "they should only attack copper rocks / tin rocks, not any normal rock"): reads the game object a
///     troll is blocked by and says whether it is a PLAIN rock (StuckRules.IsPlainRock). Only the facts come from the game here: the prefab name, MineRock / MineRock5, the drop tables.
/// </summary>
internal static class PlainRocks
{
    /// Expected outcome: true when the collider belongs to a rock that gives only Stone / Flint (never attacked by the stuck rage); `what` names it for the log.
    internal static bool Is(Collider c, out string what)
    {
        what = "";
        if (c == null)
        {
            return false;
        }

        MineRock5 r5 = c.GetComponentInParent<MineRock5>();
        MineRock r = c.GetComponentInParent<MineRock>();
        Destructible des = c.GetComponentInParent<Destructible>();
        GameObject root = r5 != null ? r5.gameObject : r != null ? r.gameObject : des != null ? des.gameObject : null;
        if (root == null)
        {
            return false;
        }

        var drops = new List<string>();
        Collect(r5 != null ? r5.m_dropItems : null, drops);
        Collect(r != null ? r.m_dropItems : null, drops);
        if (des != null)
        {
            DropOnDestroyed dod = des.GetComponent<DropOnDestroyed>();
            Collect(dod != null ? dod.m_dropWhenDestroyed : null, drops);
            if (des.m_spawnWhenDestroyed != null) // a boulder that breaks into smaller rocks: what those drop counts too
            {
                DropOnDestroyed next = des.m_spawnWhenDestroyed.GetComponent<DropOnDestroyed>();
                Collect(next != null ? next.m_dropWhenDestroyed : null, drops);
                MineRock5 nextRock = des.m_spawnWhenDestroyed.GetComponent<MineRock5>();
                Collect(nextRock != null ? nextRock.m_dropItems : null, drops);
            }
        }

        string prefab = Utils.GetPrefabName(root);
        bool plain = StuckRules.IsPlainRock(prefab, r5 != null || r != null, drops);
        what = prefab + (drops.Count > 0 ? " (drops " + string.Join(", ", drops.ToArray()) + ")" : " (no drops known)");
        return plain;
    }

    private static void Collect(DropTable table, List<string> into)
    {
        if (table == null || table.m_drops == null)
        {
            return;
        }

        foreach (DropTable.DropData d in table.m_drops)
        {
            if (d.m_item != null && !into.Contains(d.m_item.name))
            {
                into.Add(d.m_item.name);
            }
        }
    }
}
