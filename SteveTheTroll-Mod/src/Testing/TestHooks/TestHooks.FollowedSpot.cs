using System;
using System.Collections.Generic;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Domain;
using SteveTheTroll.Features.Hubs.Infrastructure;
using SteveTheTroll.Features.Trolls.Backpack.Domain;
using SteveTheTroll.Features.Trolls.Backpack.Infrastructure;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;
using SteveTheTroll.Features.Trolls.Home.Infrastructure;
using SteveTheTroll.Features.Trolls.Orders.Commands;
using SteveTheTroll.Features.Trolls.Registry.Infrastructure;
using SteveTheTroll.Features.Trolls.Taming.Infrastructure;
using SteveTheTroll.Ui;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteveTheTroll.Testing.TestHooks;

/// <summary>Part of TestHooks: the followed troll's remembered-spot and journal hooks (split out so the class stays under the size limit).</summary>
internal static partial class TestHooks
{
    /// D-175 (run 181331: the troll's object was re-created on the way - "'troll' not found"): the FOLLOWED troll (stt.followTroll, by its ZDO) stands within `metres` of the
    /// remembered spot. Expected outcome: false without a followed troll / spot.
    [TestHook("stt.followedReachedSpot")]
    private static bool FollowedReachedSpot(float metres)
    {
        ZDO z = FollowedZdo();
        if (z == null || _spotTarget == Vector3.zero)
        {
            // 2026-11-09: false with a reason in the log (it used to be a no-op expression that hid which of the two was missing)
            ModLog.Warn("TestHooks: stt.followedReachedSpot is false - " + (z == null ? "no followed troll (stt.followTroll was not called or the troll is gone)" : "no spot was injected by this test"));
            return false;
        }

        float d = HubGeometry.Flat(_spotTarget - z.GetPosition());
        if (d <= metres)
        {
            ModLog.Info("TestHooks: the followed troll reached its remembered spot (" + d.ToString("F1") + " m)");
        }

        return d <= metres;
    }

    /// 2026-11-09 (spot-return split; replaces stt.followedSpotOutcome, which accepted "reached OR gave up" and so could not tell the two apart): the followed troll's newest walk back to its spot ended AT the spot -
    /// SpotArrived, or SpotSource (a source within SpotSearchMetres of the spot was found: the troll is there). GaveUp, Cancelled and "still walking" are false. Expected outcome: true once.
    [TestHook("stt.followedSpotReached")]
    private static bool FollowedSpotReached()
    {
        string end = TripGrammar.LastSpotEnd(TrollJournal.Names(FollowedKey()));
        return end == TripGrammar.SpotArrived || end == TripGrammar.SpotSource;
    }

    private static string FollowedKey()
    {
        ZDO z = FollowedZdo();
        return z != null ? z.m_uid.ToString() : "";
    }

    /// 2026-11-09 (trip journal): the followed troll's journal holds at least `atLeast` events with this name since stt.reset - an event cannot be missed between two polls. Expected outcome: true once.
    [TestHook("stt.followedJournalHas")]
    private static bool FollowedJournalHas(string name, int atLeast)
    {
        string k = FollowedKey();
        bool ok = k.Length > 0 && TrollJournal.Count(k, name) >= atLeast;
        if (!ok)
        {
            ModLog.Every("TestHooks.journalHas." + name, 10.0, "TestHooks: stt.followedJournalHas " + name + " >= " + atLeast + " is false - journal: " + TrollJournal.Text(k));
        }

        return ok;
    }

    /// 2026-11-09: what the followed troll decided after its newest unload: "SpotWalk", "SpotSkipped:NoSpot" / "SpotSkipped:TooClose" / "SpotSkipped:OutsideLeash", "TripDone", "" = none yet.
    [TestHook("stt.followedSpotDecision")]
    private static string FollowedSpotDecision()
    {
        string k = FollowedKey();
        List<string> names = TrollJournal.Names(k);
        string d = TripGrammar.LastDecision(names);
        if (d == TripGrammar.SpotSkipped)
        {
            string detail = TrollJournal.DetailOf(k, d);
            int sp = detail.IndexOf(' ');
            return d + ":" + (sp > 0 ? detail.Substring(0, sp) : detail);
        }

        return d;
    }

    /// 2026-11-09: how the followed troll's newest walk back to its spot ended: "SpotArrived", "SpotGaveUp", "SpotCancelled", "SpotSource" (a source at the spot), "" = still walking or no walk.
    [TestHook("stt.followedSpotEnd")]
    private static string FollowedSpotEnd()
    {
        return TripGrammar.LastSpotEnd(TrollJournal.Names(FollowedKey()));
    }

    /// 2026-11-09: the order of the followed troll's trip events is legal (TripGrammar). Logs the journal and the first illegal event when it is not. Expected outcome: true.
    [TestHook("stt.followedJournalValid")]
    private static bool FollowedJournalValid()
    {
        string k = FollowedKey();
        string why;
        bool ok = TripGrammar.Valid(TrollJournal.Names(k), out why);
        if (!ok)
        {
            ModLog.Warn("TestHooks: trip journal of the followed troll is illegal: " + why + " - journal: " + TrollJournal.Text(k));
        }

        return ok;
    }

    /// 2026-11-09: the whole journal of the followed troll as one line (for a label / diagnosis; the log gets it too). Expected outcome: the text.
    [TestHook("stt.followedJournalText")]
    private static string FollowedJournalText()
    {
        string t = TrollJournal.Text(FollowedKey());
        ModLog.Info("TestHooks: trip journal of the followed troll: " + t);
        return t;
    }

    /// 2026-11-09 (precondition of spot-return): flat metres from the followed troll to its hub, -1 without a troll or hub. A test that needs a long trip asserts this instead of trusting its layout.
    [TestHook("stt.followedMetresFromHub")]
    private static int FollowedMetresFromHub()
    {
        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        HubInfo hub = c != null ? HubRegistry.For(c) : null;
        return hub == null ? -1 : Mathf.RoundToInt(HubGeometry.Flat(hub.Pos - c.transform.position));
    }

    /// D-175: the followed troll is loaded and has a farm job that is not a home / unload trip. Expected outcome: false while not loaded.
    [TestHook("stt.followedFarming")]
    private static bool FollowedFarming()
    {
        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        TrollFarmer f = c != null ? c.GetComponent<TrollFarmer>() : null;
        return f != null && !f.Unloading;
    }

    /// 2026-11-08 (spot-return race): the followed troll (by its ZDO, survives a re-created object) is on an unload trip (full backpack, hub / bed / depot). Expected outcome: false while not loaded.
    [TestHook("stt.followedUnloading")]
    private static bool FollowedUnloading()
    {
        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        TrollFarmer f = c != null ? c.GetComponent<TrollFarmer>() : null;
        return f != null && f.Unloading;
    }

    /// 2026-11-08 (lab52c: stt.travelling looked the troll up by the ORIGINAL object): the followed troll walks to a far source / the remembered spot. Expected outcome: false while not loaded.
    [TestHook("stt.followedTravelling")]
    private static bool FollowedTravelling()
    {
        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        TrollFarmer f = c != null ? c.GetComponent<TrollFarmer>() : null;
        return f != null && f.Travelling;
    }

    /// 2026-11-08: flat metres from the followed troll's REMEMBERED spot (ZDO) to the place the test injected (-1 = none, -2 = not loaded). 0 = the spot is the injected one, i.e. nothing overwrote it.
    /// 2026-11-09 (analysis of the earlier run: a check that fails on "-2" or "-1" does not say WHY): why the followed troll's spot cannot be trusted right now - one word, OK when it can.
    /// NO_FOLLOWED_TROLL (stt.followTroll not called / ZDO gone), NOT_LOADED (object not loaded), NO_FARMER (loaded without a TrollFarmer), NO_REMEMBERED_SPOT (stt_spot empty), NO_INJECTED_SPOT
    /// (this test injected none), NOT_OWNER (this client does not own the ZDO, so nothing can be written), SPOT_MISMATCH (the remembered spot is not the injected one), OK. Expected outcome: OK.
    [TestHook("stt.followedStatus")]
    private static string FollowedStatus()
    {
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return "NO_FOLLOWED_TROLL";
        }

        Character c = _followId.Length > 0 ? TrollRegistry.FindLoaded(_followId) : null;
        if (c == null)
        {
            return "NOT_LOADED";
        }

        if (c.GetComponent<TrollFarmer>() == null)
        {
            return "NO_FARMER";
        }

        Vector3 s = z.GetVec3(FarmSpotMemory.SpotKey, Vector3.zero);
        if (s == Vector3.zero)
        {
            return "NO_REMEMBERED_SPOT";
        }

        if (_spotTarget == Vector3.zero)
        {
            return "NO_INJECTED_SPOT";
        }

        if (!z.IsOwner())
        {
            return "NOT_OWNER";
        }

        return HubGeometry.Flat(s - _spotTarget) < 1f ? "OK" : "SPOT_MISMATCH";
    }

    [TestHook("stt.followedSpotDrift")]
    private static int FollowedSpotDrift()
    {
        // 2026-11-09 (analysis of the earlier run): read the spot from the ZDO of the followed troll, NOT from a loaded TrollFarmer - the spot lives in the ZDO (FarmSpotMemory.SpotKey), so the check
        // also works while the object is being re-created. -2 now only means "no followed troll"; stt.followedStatus names the reason for any non-zero answer.
        ZDO z = FollowedZdo();
        if (z == null)
        {
            return -2;
        }

        Vector3 s = z.GetVec3(FarmSpotMemory.SpotKey, Vector3.zero);
        if (s == Vector3.zero || _spotTarget == Vector3.zero)
        {
            return -1;
        }

        int drift = Mathf.RoundToInt(HubGeometry.Flat(s - _spotTarget));
        if (drift != 0)
        {
            ModLog.Info("TestHooks: stt.followedSpotDrift " + drift + " m - remembered " + s.ToString("F0") + ", injected " + _spotTarget.ToString("F0") + ", journal: " + TrollJournal.Text(FollowedKey()));
        }

        return drift;
    }
}
