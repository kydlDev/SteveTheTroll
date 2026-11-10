namespace SteveTheTroll.Features.Riding.Domain;

/// <summary>What a ridden troll does while its rider asks for nothing (T-217 R2 / R5).</summary>
public enum StillVerdict
{
    /// The rider drives, the troll attacks / staggers / is in the air or swims: movement is expected.
    Driven,

    /// Input was released less than SettleSeconds ago: the troll may still roll out.
    Settling,

    /// No input, and the troll's root stays where it settled.
    Still,

    /// No input, and the troll's root moved further than DriftLimitMetres from where it settled: something other than the rider moves it.
    Drifting
}

/// <summary>
///     T-217 R2 / R5 (manual test 20261110-015153: 6.5 m of root displacement in 2 s under a rider who asked for nothing; the saddle's speed sample said 0.00 m/s because
///     the ledge walk wrote transform.position directly): "input stopped" is not "the troll stands". The saddle measures the troll's ROOT against the place where it
///     settled after the last input and judges it here.
/// </summary>
internal static class RideStill
{
    /// Seconds after the last input / attack / stagger / air time before the standing place is taken (the game brakes a character over a few physics steps;
    /// log 01:57:58..01:58:03: 0.61 m/s right after driving, 0.01 m/s two seconds later).
    internal const float SettleSeconds = 2f;

    /// Flat distance (m) from the settled place that counts as "something moves the ridden troll" (well above idle animation root sway and slope creep, well below one ledge step series).
    internal const float DriftLimitMetres = 0.5f;

    /// Expected outcomes: Driven whenever movement is asked for or expected; Settling for the first SettleSeconds after that; then Still up to DriftLimitMetres
    /// from the settled place and Drifting beyond it.
    internal static StillVerdict Judge(bool movementExpected, float secondsSinceExpected, float driftMetres)
    {
        if (movementExpected)
        {
            return StillVerdict.Driven;
        }

        if (secondsSinceExpected < SettleSeconds)
        {
            return StillVerdict.Settling;
        }

        return driftMetres > DriftLimitMetres ? StillVerdict.Drifting : StillVerdict.Still;
    }
}
