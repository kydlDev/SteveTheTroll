using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Home.Domain;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>
///     The way home of a farming troll: to the bed / Troll Hub (D-135), with a full backpack on to a depot (D-064) and
///     into its chests (D-106), afterwards back to the remembered
///     farm spot (D-163). D-170: moved out of TrollFarmer unchanged (class size); one per farmer, it drives through the
///     farmer's movement capability.
/// </summary>
internal sealed class FarmHomeTrip
{
    private const float UnloadMetres = 10f,
        UnloadStopMetres = 8f,
        UnloadWalkSeconds = 60f,
        StuckHomeSeconds = 8f;

    private const float WaypointMetres = 30f;
    private readonly TrollFarmer _f;
    private Vector3 _unloadAt;
    private bool _unloading;
    private float _unloadStart, _homeBest = float.MaxValue, _homeBestAt;
    private bool _arrivalHome;
    private float _nextArrivalLog;
    private string _stage = "GoingHome"; // 2026-11-08: GoingHome -> WalkingToDepot -> Unloaded (the log says which stage a stuck trip is in)

    /// The ZDO id the unload ledger (and the test hooks) key a troll by. Expected outcome: "" without a ZDO.
    internal static string ZdoKey(Character troll)
    {
        ZDO z = TrollTaming.Zdo(troll);
        return z != null ? z.m_uid.ToString() : "";
    }

    private void Stage(string to, string reason)
    {
        string from = _stage;
        _stage = to;
        ModLog.State("FarmHomeTrip.stage " + TrollTaming.Tag(_f), from, to, reason);
        // 2026-11-09: the stages that tests care about are also journal events (an event cannot be missed between two polls)
        if (to == "WalkingToDepot")
        {
            Note(_f.GetComponent<Character>(), TripGrammar.DepotWalk, reason);
        }
        else if (to == "Unloaded")
        {
            Note(_f.GetComponent<Character>(), TripGrammar.Unloaded, reason);
        }
    }

    /// 2026-11-09: one event of this troll's trip into TrollJournal (and the debug log). Expected outcome: none.
    internal static void Note(Character troll, string name, string detail = "")
    {
        string key = ZdoKey(troll);
        TrollJournal.Add(key, Time.time, name, detail);
        ModLog.Verbose("TrollJournal: " + (troll != null ? troll.name : "?") + " " + name + (detail.Length > 0 ? " (" + detail + ")" : ""));
    }

    internal FarmHomeTrip(TrollFarmer farmer)
    {
        _f = farmer;
    }

    /// A point WaypointMetres ahead on the straight line to `to` (the game's path search fails for a far destination outside the loaded area; same idea as TrollReturn, D-164).
    internal static Vector3 Waypoint(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        Vector3 point = from + dir.normalized * Mathf.Min(WaypointMetres, dir.magnitude);
        // 2026-11-07 (instruction-latency, full runs 1-5 + review of full3): the point was 30 m ahead at the troll's OWN height. Uphill that is metres below the ground - the game's path
        // search finds no path to a point that is not on its navmesh, so MoveTo moved nothing and the troll stood still until the 3 s stuck rule sent it sideways (a pass in full3 was
        // 0.7 m "closer", rounded up to 1). The waypoint lies on the ground.
        ZoneSystem zs = ZoneSystem.instance;
        if (zs != null && zs.IsZoneLoaded(ZoneSystem.GetZone(point)))
        {
            point.y = zs.GetGroundHeight(point);
        }

        return point;
    }

    /// 2026-11-09 (agent25-rep02, spot-return: the depot walk ended 11.7 m short behind a log - the ledge walk refused every step, and while the recovery machine is busy the farmer lets
    /// IT drive and never calls GoHome, so the 60 s walk limit below was never looked at for 80 s; TrollFrozenWatch rebuilt the troll, the new trip announced DepotWalk a second time
    /// and the journal was illegal): the walk to the depot has used up its UnloadWalkSeconds. The farmer does not let a busy recovery drive past this point - the unload happens where
    /// the troll stands, as the limit has always promised (full11: 96 stacks delivered 12.9 m from the depot). Expected outcome: true only while unloading and past the limit.
    internal bool UnloadWalkExpired => _unloading && Time.time - _unloadStart > UnloadWalkSeconds;

    /// A new home trip (after ResumeAtSpot). Expected outcome: not unloading, no best distance.
    internal bool Reset()
    {
        _unloading = false;
        _homeBest = float.MaxValue;
        _stage = "GoingHome";
        return !_unloading;
    }

    /// One frame home. Expected outcome: whether the trip drove the troll (false = the farmer removed itself, the game's AI runs).
    internal bool GoHome(MonsterAI ai, float dt)
    {
        ITrollMovement mv = _f.Movement;
        Character me = _f.GetComponent<Character>();
        PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
        bool hasBed = profile != null && profile.HaveCustomSpawnPoint();
        // D-135: a Troll Hub (its owner's, chosen by hand or the nearest) replaces the bed as the place to go to, unload and rest
        Vector3 bed;
        bool atHub = HubRegistry.Anchor(me, hasBed ? profile.GetCustomSpawnPoint() : Vector3.zero, out bed);
        if (!hasBed && !atHub)
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " has no bed or Troll Hub to go to - stays here");
            ai.SetPatrolPoint();
            _f.Remove();
            return hasBed || atHub;
        }

        if (_unloading)
        {
            return Unload(ai, dt, bed);
        }

        float d = Vector3.Distance(_f.transform.position, bed);
        // D-095 (user: "5-25m from the bed ... don't just keep walking trying to get closer without being able to do so"): a full troll unloads as soon as it is within the depot
        // ring (25 m); any troll counts as home once within 25 m when it stops getting closer (bed inside a house it cannot enter) for StuckHomeSeconds.
        if (d < _homeBest - 0.5f)
        {
            _homeBest = d;
            _homeBestAt = Time.time;
        }

        bool stuck = d <= TrollDepot.MaxFromBed && Time.time - _homeBestAt > StuckHomeSeconds;
        // D-097 (user: "as close as 15m from the bed, not right on top of the bed"): home = within HomeRadius, it waits there
        float home = ModSettings.HomeRadius.Value;
        bool there = d <= home + 1f || (_f.IsFull && d <= TrollDepot.MaxFromBed) || stuck;
        if (there != _arrivalHome || Time.time >= _nextArrivalLog)
        {
            // 2026-11-06 (14-test cliff run: 2660 of these lines - the distance changes every step, so the repeat filter never matched): on a change of verdict, else every 5 s
            _arrivalHome = there;
            _nextArrivalLog = Time.time + 5f;
            ModLog.Decision("FarmHomeTrip.arrival", there ? "home" : "walk", TrollTaming.Tag(_f) + " is " + d.ToString("F1") + " m from the " + (atHub ? "hub" : "bed") + ", full " + _f.IsFull + ", counts as home within " + (home + 1f).ToString("F0") +
                                                                              " m (full: " + TrollDepot.MaxFromBed.ToString("F0") + " m), stuck-home " + stuck); // T-214: why a troll walks or does not (a full troll within 25 m never walks)
        }
        if (!there)
        {
            // a far walk goes to a point 30 m ahead, renewed every step, and runs (D-164); D-167: stuck detection, rage and detours are TrollRecovery, a hub is never abandoned
            long ka, kb;
            TrollRecovery.KeyOfHub(bed, out ka, out kb);
            var rec = TrollRecovery.Of(_f);
            if (rec.Tick(dt, bed, ka, kb, false, true, null))
            {
                return true;
            }

            if (d > TrollFarmer.FarWalkMetres)
            {
                mv.MoveTo(dt, Waypoint(_f.transform.position, bed), 0.5f, true);
            }
            else
            {
                mv.MoveTo(dt, bed, home, false);
            }

            return !there;
        }

        {
            TrollRecovery rh = _f.GetComponent<TrollRecovery>();
            if (rh != null)
            {
                rh.Reached();
            }
        } // D-167: at the hub: its counter starts again
        if (stuck && d > home + 1f && !_f.IsFull)
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " gets no closer to the bed than " + d.ToString("F1") +
                        " m - counts as home");
        }

        if (_f.IsFull)
        {
            // D-064: at the bed with a full backpack -> to a troll depot with room (built now when there is none)
            Vector3 at;
            if (TrollDepot.Plan(me, bed, TrollTaming.Owner(me), out at))
            {
                _unloading = true;
                _unloadAt = at;
                _unloadStart = Time.time;
                Stage("WalkingToDepot", "reached the " + (atHub ? "hub" : "bed") + " area (" + d.ToString("F1") + " m), depot spot " + at.ToString("F1") + " is " + HubGeometry.Flat(at - _f.transform.position).ToString("F1") + " m away");
                return _unloading;
            }

            ModLog.Verbose("FarmHomeTrip: " + TrollTaming.Tag(_f) + " is at the " + (atHub ? "hub" : "bed") + " (" + d.ToString("F1") + " m) with a full backpack but TrollDepot.Plan found no depot spot - retries soon");
            TrollIdle.RetrySoon(me);
            if (_f.IsMine())
            {
                Notify.Show(Loc.T("stevethetroll_depot_nospot", _f.Shown(), HubRegistry.DestinationName(me)),
                    Notify.Kind.Warn);
            }
        }

        mv.Stop();
        ai.SetPatrolPoint();
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " is at its destination (" +
                    HubRegistry.DestinationName(me) + ", " + d.ToString("F1") + " m) - waits");
        if (!_f.Auto)
        {
            Notify.Show(Loc.T("stevethetroll_farm_home", _f.Shown(), HubRegistry.DestinationName(me)),
                Notify.Kind.Farm);
        }

        _f.Remove();
        return there;
    }

    // D-064: walk to the depot (within UnloadMetres), then everything goes into its chests; gives up walking after UnloadWalkSeconds.
    private bool Unload(MonsterAI ai, float dt, Vector3 bed)
    {
        Vector3 flat = _unloadAt - _f.transform.position;
        flat.y = 0f;
        bool late = UnloadWalkExpired;
        // D-106 (user: "empty their backpacks when they are 5-10m away from the depot, so that they don't all stumble into each other"): unloads within UnloadMetres, walks only
        // until UnloadStopMetres, never into the depot
        if (flat.magnitude > UnloadMetres && !late)
        {
            // 2026-11-08 (agent24-full10b, far-troll-goes-home-obstacle-route: the troll stood 9 m from its hub behind a wall line and tried to reach the depot chest 23 m away for
            // 4 minutes - the game's path search has no way round, BaseAI then stops in place, and this plain walk had no stuck detection, so only TrollFrozenWatch's 10 s reclaim
            // ever moved it; the test passed at ~100 s in full8 / full9 / full10 by luck): the walk to the depot is a walk like the one to the hub - TrollRecovery sees "not getting
            // anywhere" and detours (a depot is never abandoned: the 60 s walk limit below ends it)
            long ka, kb;
            TrollRecovery.KeyOfPlace(_unloadAt, out ka, out kb);
            if (ModLog.IsDue("FarmHomeTrip.depotWalk." + GetHashCode(), 5.0))
            {
                // 2026-11-08 (full11 review): where a depot walk stands - distance, time used of the 60 s limit, whether recovery is busy
                ModLog.Every("FarmHomeTrip.depotWalk." + GetHashCode(), 5.0,
                    TrollTaming.Tag(_f) + " walks to the depot spot " + _unloadAt.ToString("F1") + ": " + flat.magnitude.ToString("F1") + " m to go (unloads within " + UnloadMetres.ToString("F0") + " m), walked " +
                    (Time.time - _unloadStart).ToString("F0") + " of " + UnloadWalkSeconds.ToString("F0") + " s, recovery busy " + TrollRecovery.Of(_f).Busy + ", at " + _f.transform.position.ToString("F1"));
            }

            if (TrollRecovery.Of(_f).Tick(dt, _unloadAt, ka, kb, false, true, null))
            {
                return _unloading;
            }

            _f.Movement.MoveTo(dt, _unloadAt, UnloadStopMetres, false);
            return _unloading;
        }

        {
            TrollRecovery ru = _f.GetComponent<TrollRecovery>();
            if (ru != null)
            {
                ru.Reached();
                if (late)
                {
                    ru.Forget(); // the walk was ended by the limit, not by arriving: a running rage / detour / ledge walk is cut short, the next walk starts clean
                }
            }
        } // at the depot (or the walk ended): its counter starts again
        _f.Movement.Stop();
        ai.SetPatrolPoint();
        Character me = _f.GetComponent<Character>();
        int left;
        int moved = TrollDepot.Unload(me, bed, TrollTaming.Owner(me), out left);
        int delivered = FarmUnloadLedger.Record(ZdoKey(me), moved, left);
        Stage("Unloaded", (late ? "the " + UnloadWalkSeconds.ToString("F0") + " s walk limit ended the walk, " : "") + moved + " stack(s) moved, " + left + " left, " + delivered + " delivered so far");
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " unloaded " + moved + " stack(s) at the depot (" +
                    flat.magnitude.ToString("F1") + " m" + (late ? ", walked too long" : "") + "), " + left + " left");
        if (_f.IsMine())
        {
            Notify.Show(
                left > 0
                    ? Loc.T("stevethetroll_depot_full", _f.Shown(), left.ToString())
                    : Loc.T("stevethetroll_depot_done", _f.Shown(), moved.ToString()),
                left > 0 ? Notify.Kind.Warn : Notify.Kind.Farm);
        }

        bool decided = false; // ResumeAtSpot writes its own journal decision (SpotWalk / SpotSkipped), every other way out writes TripDone
        switch (HomeRules.AfterUnloading(left, TrollHome.OwnerWears(me)))
        {
            case AfterUnload.ResumeSpot:
                if (ResumeAtSpot())
                {
                    return true;
                }

                decided = true;
                break;
            case AfterUnload.StayHome:
                Note(me, TripGrammar.TripDone, "stays home (owner wears no slippers)");
                // D-173: no slippers - home is where it waits; GoHome walks it on to the home radius (not full any more) and the farmer ends there
                _f.IsFull = false;
                _unloading = false;
                ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) +
                            " emptied its backpack at home - the owner wears no slippers, it stays home");
                return true;
        }

        if (!decided)
        {
            Note(me, TripGrammar.TripDone, left + " stack(s) left");
        }

        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " is done unloading (" + left + " stack(s) left, owner wears the slippers " + TrollHome.OwnerWears(me) + ") - the idle check sends it on from here");
        _f.Remove();
        return moved >= 0;
    }

    // User 2026-10-31 ("have trolls remember the spot where they've farmed before coming home to empty the backpack"): an emptied troll walks back to where it last worked and farms
    // on from there (focus from its ZDO). Too close to bother (SpotMinMetres), out of the hub's farm leash or no spot known -> false (the idle check sends it farming from here).
    private bool ResumeAtSpot()
    {
        Vector3 spot;
        Character self = _f.GetComponent<Character>();
        if (!_f.Spots.Spot(out spot))
        {
            Note(self, TripGrammar.SpotSkipped, "NoSpot");
            // 2026-11-08 (agent24-lab52c, spot-return: once the troll did not walk back although the test had remembered a spot 150 m away, and nothing in the log said why): every way out says so
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " emptied its backpack - no spot to walk back to (none remembered, or it has expired), farms from here");
            return spot != Vector3.zero;
        }

        Vector3 away = spot - _f.transform.position;
        away.y = 0f;
        if (TripRules.Resume(true, away.magnitude, true) == ResumeDecision.TooClose)
        {
            Note(self, TripGrammar.SpotSkipped, "TooClose " + away.magnitude.ToString("F0") + " m");
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " emptied its backpack - the remembered spot " + spot.ToString("F0") + " is only " + away.magnitude.ToString("F0") + " m away (under " + TripRules.SpotMinMetres.ToString("F0") + " m) - farms from here");
            return away.magnitude >= TripRules.SpotMinMetres;
        }

        Character me = _f.GetComponent<Character>();
        HubInfo hub = HubRegistry.For(me);
        if (TripRules.Resume(true, away.magnitude, hub == null || HubRules.FarmAllowed(true, HubGeometry.Flat(spot - hub.Pos))) == ResumeDecision.OutsideLeash)
        {
            Note(self, TripGrammar.SpotSkipped, "OutsideLeash");
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " emptied its backpack - the remembered spot " + spot.ToString("F0") + " is " + HubGeometry.Flat(spot - hub.Pos).ToString("F0") + " m from its hub, outside the farm leash - farms from here");
            return hub == null;
        }

        ZDO z = TrollTaming.Zdo(me);
        string focus = z != null ? TrollFocus.Normalize(z.GetString(TrollRegistry.FocusKey)) : "";
        Note(self, TripGrammar.SpotWalk, away.magnitude.ToString("F0") + " m");
        _f.Setup(focus, spot);
        _f.IsFull = false;
        Reset();
        _f.Auto = true;
        _f.StartTravel(spot, true);
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(_f) + " emptied its backpack - walks " +
                    away.magnitude.ToString("F0") + " m back to where it farmed (" + spot.ToString("F0") + ")" +
                    (focus.Length > 0 ? ", focus '" + focus + "'" : ""));
        return _f.Travelling;
    }
}
