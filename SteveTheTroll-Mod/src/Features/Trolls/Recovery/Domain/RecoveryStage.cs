namespace SteveTheTroll.Features.Trolls.Recovery.Domain;

/// <summary>D-167: where one recovery cycle stands (travel = no manoeuvre runs).</summary>
internal enum RecoveryStage
{
    Travel,
    Rage,
    Leg,

    /// Item 5: the assisted walk over a cliff (last rung, or first when the ground ahead is a cliff).
    Ledge
}

/// <summary>D-167: what the game side has to do now (RecoveryDecision.Command; RecoveryExecutor maps each to its action).</summary>
internal enum RecoveryCmd
{
    None,
    StartRage,
    EndRage,
    StartLeg,
    EndLeg,

    /// Item 5: the ledge walk starts / ends.
    StartLedge,
    EndLedge,
    CycleFailed,
    CycleReset
}
