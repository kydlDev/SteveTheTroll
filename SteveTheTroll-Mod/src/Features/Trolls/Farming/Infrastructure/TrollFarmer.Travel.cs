using System;
using System.Collections.Generic;
using KydlCore.Common;
using KydlCore.Core;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Contracts;
using SteveTheTroll.Features.Trolls.Core;
using SteveTheTroll.Features.Trolls.Farming.Application;
using SteveTheTroll.Features.Trolls.Farming.Domain;
using SteveTheTroll.Features.Trolls.Farming.Execution;
using SteveTheTroll.Features.Trolls.Orders.Domain;
using SteveTheTroll.Features.Trolls.Recovery.Infrastructure;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Domain;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Presentation.Notifications;
using SteveTheTroll.Ui;
using SteveTheTroll.Features.Trolls.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Features.Trolls.Farming.Infrastructure;

/// <summary>Part of TrollFarmer: the long walk to a remembered spot or home (split out so the class stays under the size limit).</summary>
internal sealed partial class TrollFarmer
{
    internal void StartTravel(Vector3 to, bool toRememberedSpot = false)
    {
        Travelling = true;
        _travelTo = to;
        _travelStart = Time.time;
        _toSpot = toRememberedSpot;
        if (toRememberedSpot)
        {
            SetSpotWalk(true); // D-175: survives the troll's object being re-created on the way (zone reload)
        }

        _nextSearch =
            Time.time + SearchSeconds; // the normal search runs every SearchSeconds on the way: the zone loads, the near sources show up
    }

    private void SetSpotWalk(bool on)
    {
        ZNetView v = GetComponent<ZNetView>();
        if (v != null && v.IsValid() && v.IsOwner() && v.GetZDO().GetBool(SpotWalkKey) != on)
        {
            v.GetZDO().Set(SpotWalkKey, on);
        }
    }

    /// D-175: a walk back to the remembered spot was running when this farmer was made (the object was re-created): walk on. Expected outcome: true when it resumed.
    internal bool ResumeSpotWalk()
    {
        ZNetView v = GetComponent<ZNetView>();
        if (v == null || !v.IsValid() || !v.GetZDO().GetBool(SpotWalkKey))
        {
            return false;
        }

        Vector3 spot;
        if (!Spots.Spot(out spot) || HubGeometry.Flat(spot - transform.position) <= TravelArriveMetres)
        {
            SetSpotWalk(false);
            return false;
        }

        StartTravel(spot, true);
        ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " walks on to its remembered spot " +
                    spot.ToString("F0") + " (" + HubGeometry.Flat(spot - transform.position).ToString("F0") +
                    " m, the walk survived a reload)");
        return true;
    }

    private void EndTravel(bool failed)
    {
        if (_toSpot && Travelling)
        {
            FarmHomeTrip.Note(GetComponent<Character>(), failed ? TripGrammar.SpotGaveUp : TripGrammar.SpotArrived);
        }

        if (_toSpot)
        {
            SetSpotWalk(false);
        }

        if (failed)
        {
            Spots.GiveUp(_travelTo);
        }

        Travelling = false;
        _nextSearch = failed ? Time.time + NothingWaitSeconds : 0f;
    }

    // Walking to a far source / back to the farm spot. Expected outcome: true = the walk drove the troll this frame.
    private bool Travel(float dt)
    {
        Vector3 flat = _travelTo - transform.position;
        flat.y = 0f;
        float d = flat.magnitude;
        if (d <= TravelArriveMetres)
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " arrived at the far farm place " +
                        _travelTo.ToString("F0") + " (" + (Time.time - _travelStart).ToString("F0") +
                        " s) - searches there");
            TrollRecovery rr = GetComponent<TrollRecovery>();
            if (rr != null)
            {
                rr.Reached();
            }

            EndTravel(false);
            return d <= TravelArriveMetres;
        }

        if (Time.time - _travelStart > TravelMaxSeconds)
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") +
                        " (" + d.ToString("F0") + " m left, walked too long) - gives that place up for " +
                        FarmSpotMemory.FarBadSeconds.ToString("0") + " s");
            EndTravel(true);
            return !Travelling;
        }

        // D-167: stuck detection, rage and detours are TrollRecovery; a far place that fails two complete cycles is given up (spec 3: 120 s)
        long ka, kb;
        TrollRecovery.KeyOfPlace(_travelTo, out ka, out kb);
        var rec = TrollRecovery.Of(this);
        bool drove = rec.Tick(dt, _travelTo, ka, kb, true, true, null);
        if (drove)
        {
            return drove;
        }

        if (rec.TakeAbandon())
        {
            ModLog.Info("TrollFarmer: " + TrollTaming.Tag(this) + " does not get to " + _travelTo.ToString("F0") +
                        " (" + d.ToString("F0") +
                        " m left, two complete recovery cycles failed) - gives that place up for " +
                        FarmSpotMemory.FarBadSeconds.ToString("0") + " s");
            EndTravel(true);
            return !Travelling;
        }

        Movement.MoveTo(dt, d > FarWalkMetres ? FarmHomeTrip.Waypoint(transform.position, _travelTo) : _travelTo,
            TravelArriveMetres * 0.5f, true);
        return Travelling;
    }
}
