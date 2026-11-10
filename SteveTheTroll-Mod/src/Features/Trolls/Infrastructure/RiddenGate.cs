using KydlCore.Common;
using SteveTheTroll.Features.Trolls.Contracts;

namespace SteveTheTroll.Features.Trolls.Infrastructure;

/// <summary>
///     T-217 R1 (manual test 20261110-015153: a ridden troll was walked 6.5 m by TrollRecovery.LedgeStep while its move direction was zero): ONE rule for every
///     autonomous driver of a troll - "ridden => the rider steers, nothing else moves, turns or orders the troll". Every entry point of the mod's own brains
///     (the farming steer prefix and each step it calls, the recovery tick / drive / ledge step, the idle check) asks this gate first. Switching the
///     components off (TrollSaddle.SuspendBehaviours) is only a second line of defence: a disabled MonoBehaviour's methods can still be called.
/// </summary>
internal static class RiddenGate
{
    private const double LogEverySeconds = 5d;

    /// How often a driver was refused since the last ResetRefusals (the lab asserts that mounting a busy troll makes this grow while the troll's root stays put).
    internal static int Refusals { get; private set; }

    /// Lab: start counting again.
    internal static int ResetRefusals()
    {
        Refusals = 0;
        return Refusals;
    }

    /// Expected outcome: true = the troll is ridden, so `driver` must return at once without moving, turning or ordering it (counted, logged every few seconds per driver);
    /// false = not ridden, the driver works as usual. `driver` is a constant name such as "TrollFarming.Steer" (it is also the log key, so no string is built per call).
    internal static bool Refuses(Character troll, string driver)
    {
        bool ridden = TrollRiding.Is(troll);
        if (ridden)
        {
            Refusals++;
            if (ModLog.IsDue(driver, LogEverySeconds))
            {
                ModLog.Every(driver, LogEverySeconds, $"RiddenGate: {driver} refused on the ridden {troll.name} - the rider steers it ({Refusals} refusal(s) of all drivers since the last reset)");
            }
        }

        return ridden;
    }
}
