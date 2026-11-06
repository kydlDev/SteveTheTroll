namespace SteveTheTroll.Features.Riding.Domain;

/// <summary>What pressing the alternative interact key on a troll does (T-213, user decisions 2026-11-06).</summary>
public enum MountVerdict
{
    /// Mount: the halter + seat go on the troll, the player sits.
    Mount,

    /// The player already rides THIS troll: the same key dismounts.
    Dismount,

    /// Nothing happens and no prompt is shown: the player carries no halter + seat.
    NoHalter,

    /// No prompt: the recipe is not unlocked yet (the item is not there from the start).
    Locked,

    /// No prompt: not a tamed troll, not this player's troll, or dead.
    NotRideable,

    /// No prompt: another rider already sits on this troll (one rider per troll).
    Occupied,

    /// No prompt: the player rides ANOTHER troll (the mount prompt is not shown on any other troll while mounted).
    RidingAnother
}

/// <summary>
///     T-213 (user decisions 2026-11-06): the game-free rules of the rideable troll. The alternative interact key mounts and dismounts; E stays follow / stay.
///     Expected outcomes are in each member's comment; the numbers and enum values are what the game code and the lab read.
/// </summary>
internal static class RideRules
{
    /// What the alternative interact key does on this troll. `ridingTroll` is the troll id the player rides now (0 = none), `thisTroll` the one being looked at.
    /// Order matters: a rider never gets a prompt on another troll, even one that would otherwise be rideable. Expected outcome: Mount only for a free, living, own tamed troll
    /// with the halter in the inventory and the recipe unlocked.
    internal static MountVerdict Decide(bool unlocked, bool hasHalter, bool tamed, bool mine, bool dead, bool hasOtherRider, int ridingTroll, int thisTroll)
    {
        if (ridingTroll != 0)
        {
            return ridingTroll == thisTroll ? MountVerdict.Dismount : MountVerdict.RidingAnother;
        }

        if (!tamed || !mine || dead)
        {
            return MountVerdict.NotRideable;
        }

        if (hasOtherRider)
        {
            return MountVerdict.Occupied;
        }

        if (!unlocked)
        {
            return MountVerdict.Locked;
        }

        return hasHalter ? MountVerdict.Mount : MountVerdict.NoHalter;
    }

    /// The hover prompt is shown only when the key would do something. Expected outcome: true for Mount and Dismount.
    internal static bool PromptVisible(MountVerdict v)
    {
        return v == MountVerdict.Mount || v == MountVerdict.Dismount;
    }

    /// Full backpack while ridden = the collecting list pauses until the backpack is empty again (no unload trip is possible). Expected outcome: true = do not look for sources.
    internal static bool CollectingPaused(bool ridden, bool backpackFull)
    {
        return ridden && backpackFull;
    }

    /// Farm, guard and hub work are stopped on a ridden troll; the owner's other trolls still guard the rider. Expected outcome: true = the troll takes orders only from its rider.
    internal static bool OrdersSuspended(bool ridden)
    {
        return ridden;
    }

    /// The Mouse-4 troll order: a ridden troll ignores it, every other troll near the player follows it. Expected outcome: true = this troll follows the order.
    internal static bool FollowsOrderKey(bool ridden)
    {
        return !ridden;
    }

    /// A second tamed troll guards the rider (and the ridden troll) when either is attacked. Expected outcome: true = this troll engages the attacker.
    internal static bool GuardsTheRider(bool thisTrollRidden, bool riderOrMountAttacked, float distanceToRider, float guardRange)
    {
        return !thisTrollRidden && riderOrMountAttacked && distanceToRider <= guardRange;
    }

    /// Damage while ridden: a hit that reaches the player hurts the player (normally), a hit that only reaches the troll hurts only the troll. The player is never knocked off.
    /// Expected outcome: the damage goes to the one hit, nobody else.
    internal static bool DamageHitsPlayer(bool hitReachedPlayer)
    {
        return hitReachedPlayer;
    }

    /// Why a ride ended. Expected outcome: the halter + seat return to the inventory, except after death (tombstone) and the troll is free again.
    internal static EndOutcome End(EndReason reason)
    {
        return reason == EndReason.PlayerDied ? EndOutcome.ItemToTombstone : EndOutcome.ItemToInventory;
    }

    /// On login with a mount file: the item is back in the inventory, the troll is sent to its hub and behaves as normal. Expected outcome: the recovery to run.
    internal static LoginRecovery OnLogin(int storedTroll)
    {
        return storedTroll == 0 ? LoginRecovery.None : LoginRecovery.ItemBackAndTrollToHub;
    }
}

public enum EndReason
{
    PlayerDismounted,
    PlayerDied,
    TrollDied,
    Teleported,
    LoggedOut
}

public enum EndOutcome
{
    ItemToInventory,
    ItemToTombstone
}

public enum LoginRecovery
{
    None,
    ItemBackAndTrollToHub
}