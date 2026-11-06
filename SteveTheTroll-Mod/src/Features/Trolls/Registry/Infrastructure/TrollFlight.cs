using KydlCore.Common;
using KydlCore.Features.Diagnostics.Contracts;
using SteveTheTroll.Features.Trolls.Combat.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;

namespace SteveTheTroll.Features.Trolls.Registry.Infrastructure;

/// <summary>
///     NEXT_STEPS item 0 / D-181 (PROJECT_RULES 28): every own tamed troll the registry sees is tracked by KydlCore's flight
///     recorder, so a frozen far troll or a pinned guard target leaves the last two minutes of its position, owner, zone and OUR
///     state in <c>flight\*.txt</c> without anybody having switched the debug log on.
/// </summary>
internal static class TrollFlight
{
    /// <summary>
    ///     Tracks (or keeps tracking: the recorder keeps the history of an object tracked again) one own troll.
    ///     Expected outcome: true while the recorder is on and has room for it.
    /// </summary>
    internal static bool Track(Character troll, string label)
    {
        bool tracked = FlightRecorder.Track(troll, label, () => Facts(troll));
        ModLog.Every("TrollFlight.track", 60d, "TrollFlight: tracking " + label + " -> " + tracked + " (" + FlightRecorder.TrackedCount + " object(s) tracked)");
        return tracked;
    }

    /// <summary>Stops tracking a troll that left the list (dead for good). Returns whether it was tracked.</summary>
    internal static bool Untrack(Character troll)
    {
        bool was = troll != null && FlightRecorder.Untrack(troll);
        ModLog.Decision("TrollFlight.untrack", was ? "untracked" : "not tracked", "troll " + (troll != null ? TrollTaming.Tag(troll) : "gone"));
        return was;
    }

    /// <summary>
    ///     The mod's own facts for one sample (runs once per sample, so it only reads): guard / AI / forced target, the farmed item,
    ///     and which job component holds the troll now. Expected outcome: a short single-line text.
    /// </summary>
    internal static string Facts(Character troll)
    {
        if (troll == null)
        {
            return "gone";
        }

        string farm = TrollFarming.Farming(troll);
        return TrollGuard.Describe(troll) + ", farming '" + farm + "'" +
               ", walk-back " + (troll.GetComponent<TrollReturn>() != null) +
               ", chase " + (troll.GetComponent<TrollChase>() != null) +
               ", rage " + (troll.GetComponent<TrollRage>() != null) +
               ", " + TrollHold.Describe(troll);
    }
}
