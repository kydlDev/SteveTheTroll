using System.Collections.Generic;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>What an emptied troll does about the place where it farmed (FarmHomeTrip.ResumeAtSpot).</summary>
internal enum ResumeDecision
{
    Walk,
    NoSpot,
    TooClose,
    OutsideLeash
}

/// <summary>
///     2026-11-09 (agent24): the decisions of a home trip as PURE functions, so the game code and the tests ask the same question and a property test can try every combination.
///     Unity-free (unit tests link this file).
/// </summary>
internal static class TripRules
{
    /// User 2026-10-31: a remembered spot closer than this to the troll (after unloading) is not worth the walk.
    internal const float SpotMinMetres = 40f;

    /// D-106: a full troll empties its backpack once it is this close (flat metres) to its depot - it never has to reach the depot itself.
    internal const float UnloadMetres = 10f;

    /// 2026-11-10 (a26rep2 ledge-forced-medium-down, 3 of 5 repeats: the depot was built exactly at the ledge walk's goal, the walk stopped 1.9 m short at the depot's wall and ran into its 40 s timeout
    /// while the troll stood inside the unload range): the troll is close enough to unload. Recovery must not hold the troll back from here. Expected outcome: true at `flatMetres` <= UnloadMetres.
    internal static bool InUnloadRange(float flatMetres)
    {
        return flatMetres <= UnloadMetres;
    }

    /// What to do with the remembered spot after an unload. `awayMetres` = flat distance troll -> spot, `farmAllowed` = the hub's farm leash lets the troll farm at the spot (HubRules.FarmAllowed).
    /// Order matters and is part of the contract: no spot, then too close, then the leash. Expected outcome: exactly one decision.
    internal static ResumeDecision Resume(bool spotKnown, float awayMetres, bool farmAllowed)
    {
        if (!spotKnown)
        {
            return ResumeDecision.NoSpot;
        }

        if (awayMetres < SpotMinMetres)
        {
            return ResumeDecision.TooClose;
        }

        return farmAllowed ? ResumeDecision.Walk : ResumeDecision.OutsideLeash;
    }

    /// The journal event name of a decision.
    internal static string EventOf(ResumeDecision d)
    {
        return d == ResumeDecision.Walk ? TripGrammar.SpotWalk : TripGrammar.SpotSkipped;
    }
}

/// <summary>
///     The legal order of the trip events in <see cref="TrollJournal" /> (a small state machine). Phases: 0 idle, 1 full, 2 walking to the depot, 3 unloaded (a decision is due), 4 walking to the
///     spot. A decision may only follow an unload, a spot outcome only a spot walk, an unload only a depot walk. "Full" may restart from any phase (an order ends a trip silently); a depot walk may
///     also start from idle (SetupUnload after a logout never says "full"). "SpotRemembered" is free. Used by the lab hooks (is the order of this troll's trip legal?) and property-tested.
/// </summary>
internal static class TripGrammar
{
    internal const string Full = "Full",
        DepotWalk = "DepotWalk",
        Unloaded = "Unloaded",
        SpotWalk = "SpotWalk",
        SpotSkipped = "SpotSkipped",
        TripDone = "TripDone",
        SpotArrived = "SpotArrived",
        SpotGaveUp = "SpotGaveUp",
        SpotCancelled = "SpotCancelled",
        SpotSource = "SpotSource",
        SpotRemembered = "SpotRemembered";

    /// Is this sequence of event names a legal trip history? `why` names the first illegal event. Expected outcome: true for a legal (or empty, or unfinished) history.
    internal static bool Valid(IList<string> events, out string why)
    {
        int phase = 0;
        for (int i = 0; i < events.Count; i++)
        {
            string e = events[i];
            int next = Next(phase, e);
            if (next < 0)
            {
                why = "event " + (i + 1) + " '" + e + "' after phase " + PhaseName(phase) + " is not allowed";
                return false;
            }

            phase = e == SpotRemembered ? phase : next;
        }

        why = "";
        return true;
    }

    /// The phase after `e` from `phase`, -1 when not allowed.
    internal static int Next(int phase, string e)
    {
        switch (e)
        {
            case SpotRemembered:
                return phase;
            case Full:
                return 1;
            case DepotWalk:
                return phase == 0 || phase == 1 ? 2 : -1;
            case Unloaded:
                return phase == 2 ? 3 : -1;
            case SpotWalk:
                return phase == 3 ? 4 : -1;
            case SpotSkipped:
            case TripDone:
                return phase == 3 ? 0 : -1;
            case SpotArrived:
            case SpotGaveUp:
            case SpotCancelled:
            case SpotSource:
                return phase == 4 ? 0 : -1;
            default:
                return -1;
        }
    }

    internal static string PhaseName(int phase)
    {
        switch (phase)
        {
            case 0:
                return "idle";
            case 1:
                return "full";
            case 2:
                return "walking to the depot";
            case 3:
                return "unloaded (decision due)";
            default:
                return "walking to the spot";
        }
    }

    /// The newest decision after an unload: "SpotWalk", "SpotSkipped" or "TripDone" ("" = none yet).
    internal static string LastDecision(IList<string> events)
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i] == SpotWalk || events[i] == SpotSkipped || events[i] == TripDone)
            {
                return events[i];
            }
        }

        return "";
    }

    /// How the newest spot walk ended: "SpotArrived", "SpotGaveUp", "SpotCancelled", "SpotSource" ("" = not ended, or no walk).
    internal static string LastSpotEnd(IList<string> events)
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            string e = events[i];
            if (e == SpotArrived || e == SpotGaveUp || e == SpotCancelled || e == SpotSource)
            {
                return e;
            }

            if (e == SpotWalk)
            {
                return "";
            }
        }

        return "";
    }
}
