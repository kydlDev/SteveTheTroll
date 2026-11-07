using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Contracts;

/// <summary>T-213: the trolls feature does not know the riding feature; riding sets this hook so orders and guard duty leave a ridden troll alone (the rider steers it).</summary>
internal static class TrollRiding
{
    internal static System.Func<Character, bool> Ridden;

    /// Expected outcome: true while a player rides this troll.
    internal static bool Is(Character c)
    {
        return c != null && Ridden != null && Ridden(c);
    }
}

