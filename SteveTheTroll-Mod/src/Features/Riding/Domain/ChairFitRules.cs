using System;

namespace SteveTheTroll.Features.Riding.Domain;

/// <summary>
///     2026-11-17 (user: "re-run the centering once more if the back is detected 2 cm outside", "the limit starts from the inside of the chair", "glue the back of the
///     player to the chair or a bit forward"; lab run 20261117-180708: the back was 2 cm inside the chair back 5 s after a centring that left 0.0 cm). Distances are in
///     metres along the troll's forward axis; 0 = the INSIDE face of the chair back (the cantle). The rider's back = hips centre minus the hips depth.
///     Game-free: HalterCentring measures, these rules decide.
/// </summary>
internal static class ChairFitRules
{
    /// Where the centring puts the rider's back: this far in front of the chair back's inside face ("glued, a bit forward").
    internal const float ForwardMarginM = 0.015f;

    /// The rider's back this far INSIDE the chair back (behind its inside face) starts the centring again.
    internal const float RecentreAtM = 0.02f;

    /// The centring is settled when both remaining errors are below this.
    internal const float SettledM = 0.01f;

    /// How often (s) a finished centring checks the rider's back again.
    internal const float WatchEverySeconds = 0.25f;

    /// The back must be RecentreAtM inside on this many checks IN A ROW (0.75 s) before a new centring: a single pose while the troll starts walking is not
    /// enough (repeat run rep-chair 2026-11-17, rep02: one re-centre at the start of a walk measured a passing pose and pushed the rider 15 cm sideways).
    internal const int RecentreAfterChecks = 3;

    /// The distance (m) from the hips centre to the chair back's inside face that the centring aims for. Expected outcome: hips depth + the forward margin.
    internal static float TargetGap(float hipsDepth) => hipsDepth + ForwardMarginM;

    /// How far (m) the rider's back is INSIDE the chair back; 0 when it is in front of the inside face. `gap` = hips centre to the inside face. Expected outcome: >= 0.
    internal static float IntoBack(float gap, float hipsDepth) => Math.Max(0f, hipsDepth - gap);

    /// The forward correction (m, + = towards the troll's head) still needed to reach the target - both ways, so the back stays glued to its place. Expected outcome:
    /// 0 at the target, + when the back is too far back, - when it sits too far forward.
    internal static float ForwardError(float gap, float hipsDepth) => TargetGap(hipsDepth) - gap;

    /// Is the back too far inside the chair back on this check? Expected outcome: true at RecentreAtM or more.
    /// (A tenth of a millimetre of tolerance: 0.17 - 0.15 in float is 0.0199999, and exactly 2 cm must count.)
    internal static bool TooFarInside(float intoBackM) => intoBackM >= RecentreAtM - 0.0001f;

    /// After the centring finished: start it again? `checksInARow` = how many checks in a row (this one included) found the back too far inside. Expected
    /// outcome: true from the RecentreAfterChecks-th check in a row on.
    internal static bool NeedsRecentre(float intoBackM, int checksInARow) => TooFarInside(intoBackM) && checksInARow >= RecentreAfterChecks;

    /// A new centring after the ride started moves the rider forward / back only: the sideways place found when mounting stays (rep02). Expected outcome: the
    /// sideways error the centring may act on - 0 for a re-run.
    internal static float SideErrorFor(bool rerun, float sideErrorM) => rerun ? 0f : sideErrorM;

    /// Is the centring done? Expected outcome: true when both errors (sideways, forward) are below SettledM.
    internal static bool Settled(float sideErrorM, float forwardErrorM) => Math.Abs(sideErrorM) < SettledM && Math.Abs(forwardErrorM) < SettledM;
}
