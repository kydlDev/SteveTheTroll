using System;
using System.Collections.Generic;
using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Registry.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>NEXT_STEPS item 4 step 4: the troll ledger read-back - the file of one world, entries missing from the world, merge and expiry (game-free rules).</summary>
[TestFixture]
public class LedgerRulesTests
{
    static readonly DateTime Now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    static LedgerEntry E(string id, bool dead = false, bool missing = false, string since = "") =>
        new LedgerEntry { Id = id, Name = "Troll " + id, X = 10.5f, Y = 3f, Z = -20f, Health = 600f, Loaded = true, Dead = dead, Missing = missing, MissingSince = since };

    [TestCase("Valheim", 123L, "Valheim", 123L, true)] [TestCase("valheim", 123L, "VALHEIM", 123L, true)]
    // two worlds with the same name (user 2026-11-06): the uid decides
    [TestCase("Valheim", 123L, "Valheim", 124L, false)] [TestCase("Valheim", 123L, "Other", 123L, false)]
    // an unknown world uid never matches (an old file without one, or a world without an id)
    [TestCase("Valheim", 0L, "Valheim", 0L, false)] [TestCase("Valheim", 5L, "Valheim", 0L, false)] [TestCase(null, 5L, "Valheim", 5L, false)]
    public void Matches(string fileWorld, long fileUid, string world, long uid, bool expected) { Assert.That(LedgerRules.Matches(fileWorld, fileUid, world, uid), Is.EqualTo(expected)); }

    [Test] public void FormatThenParse_RoundTrips()
    {
        var list = new List<LedgerEntry> { E("a1"), E("b2", dead: true), E("c3", missing: true, since: "2026-11-05T10:00:00Z") };
        list[0].Name = "Spen\"cer \\ 2";
        string json = LedgerRules.Format("My \"World\"", 8372649182736L, 42L, list);
        string world; long uid, player;
        List<LedgerEntry> back = LedgerRules.Parse(json, out world, out uid, out player);
        Assert.That(world, Is.EqualTo("My \"World\""));
        Assert.That(uid, Is.EqualTo(8372649182736L));
        Assert.That(player, Is.EqualTo(42L));
        Assert.That(back.Count, Is.EqualTo(3));
        Assert.That(back[0].Name, Is.EqualTo("Spen\"cer \\ 2"));
        Assert.That(back[0].X, Is.EqualTo(10.5f)); Assert.That(back[0].Z, Is.EqualTo(-20f)); Assert.That(back[0].Health, Is.EqualTo(600f));
        Assert.That(back[1].Dead, Is.True); Assert.That(back[0].Dead, Is.False);
        Assert.That(back[2].Missing, Is.True); Assert.That(back[2].MissingSince, Is.EqualTo("2026-11-05T10:00:00Z")); Assert.That(back[0].Missing, Is.False);
    }

    [TestCase(null)] [TestCase("")] [TestCase("not json at all")] [TestCase("{ \"trolls\": [ ")] [TestCase("\u0000\u0001garbage \"id\": ")]
    public void Parse_GarbageNeverThrows(string text)
    {
        string world; long uid, player;
        Assert.DoesNotThrow(() => LedgerRules.Parse(text, out world, out uid, out player));
    }

    [Test] public void MissingEntries_OnlyLivingOnesWithoutAZdo()
    {
        var file = new List<LedgerEntry> { E("a1"), E("b2"), E("c3", dead: true) };
        List<LedgerEntry> missing = LedgerRules.MissingEntries(file, new HashSet<string> { "a1" });
        Assert.That(missing.ConvertAll(m => m.Id), Is.EqualTo(new[] { "b2" }));
        Assert.That(LedgerRules.MissingEntries(file, null), Is.Empty, "a client does not know the ZDOs: nothing is missing");
    }

    [Test] public void Merge_LiveEntryWithoutZdo_IsMissingSinceNow()
    {
        List<LedgerEntry> r = LedgerRules.Merge(new List<LedgerEntry>(), new List<LedgerEntry> { E("a1") }, new HashSet<string>(), Now);
        Assert.That(r[0].Missing, Is.True);
        Assert.That(r[0].MissingSince, Is.EqualTo("2026-11-06T12:00:00Z"));
    }

    [Test] public void Merge_MissingSinceKeepsTheFirstTime()
    {
        var prev = new List<LedgerEntry> { E("a1", missing: true, since: "2026-11-04T08:00:00Z") };
        List<LedgerEntry> r = LedgerRules.Merge(prev, new List<LedgerEntry> { E("a1") }, new HashSet<string>(), Now);
        Assert.That(r[0].MissingSince, Is.EqualTo("2026-11-04T08:00:00Z"));
    }

    [Test] public void Merge_TrollReturns_IsNoLongerMissing()
    {
        var prev = new List<LedgerEntry> { E("a1", missing: true, since: "2026-11-05T08:00:00Z") };
        List<LedgerEntry> r = LedgerRules.Merge(prev, new List<LedgerEntry> { E("a1") }, new HashSet<string> { "a1" }, Now);
        Assert.That(r[0].Missing, Is.False);
        Assert.That(r[0].MissingSince, Is.Empty);
    }

    [Test] public void Merge_PreviousEntryNotLive_KeptAndFlaggedWhenItsTrollIsGone()
    {
        var prev = new List<LedgerEntry> { E("old") };
        List<LedgerEntry> r = LedgerRules.Merge(prev, new List<LedgerEntry> { E("a1") }, new HashSet<string> { "a1" }, Now);
        Assert.That(r.Count, Is.EqualTo(2));
        Assert.That(r[1].Id, Is.EqualTo("old"));
        Assert.That(r[1].Missing, Is.True);
        Assert.That(r[1].MissingSince, Is.EqualTo("2026-11-06T12:00:00Z"));
    }

    [Test] public void Merge_PreviousEntryNotLive_ButItsTrollExists_IsKeptNotMissing()
    {
        List<LedgerEntry> r = LedgerRules.Merge(new List<LedgerEntry> { E("far") }, new List<LedgerEntry>(), new HashSet<string> { "far" }, Now);
        Assert.That(r.Count, Is.EqualTo(1));
        Assert.That(r[0].Missing, Is.False);
    }

    [Test] public void Merge_DeadPreviousEntry_IsDropped()
    {
        List<LedgerEntry> r = LedgerRules.Merge(new List<LedgerEntry> { E("x", dead: true) }, new List<LedgerEntry>(), new HashSet<string>(), Now);
        Assert.That(r, Is.Empty);
    }

    [TestCase("2026-11-06T11:00:00Z", true)] [TestCase("2026-10-31T12:00:01Z", true)] [TestCase("2026-10-30T11:59:59Z", false)]
    public void Merge_MissingForMoreThanKeepDays_IsDropped(string since, bool kept)
    {
        var prev = new List<LedgerEntry> { E("gone", missing: true, since: since) };
        List<LedgerEntry> r = LedgerRules.Merge(prev, new List<LedgerEntry>(), new HashSet<string>(), Now);
        Assert.That(r.Count, Is.EqualTo(kept ? 1 : 0));
    }

    [Test] public void Merge_UnknownZdos_ClientNeverFlagsAnything()
    {
        var prev = new List<LedgerEntry> { E("old") };
        List<LedgerEntry> r = LedgerRules.Merge(prev, new List<LedgerEntry> { E("a1") }, null, Now);
        Assert.That(r.Count, Is.EqualTo(2));
        Assert.That(r.TrueForAll(e => !e.Missing), Is.True);
    }
}
