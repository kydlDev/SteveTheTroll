using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;

namespace SteveTheTroll.Tests.Unit;

/// <summary>
///     2026-11-09 (agent24, spot-return history: the same race again and again): the decisions of a home trip (TripRules) and the legal ORDER of its events (TripGrammar, TrollJournal) are
///     pure, so they are tried with random inputs and random event orders instead of the one order a lab run happens to produce. The lab tests then only prove that the game emits these events.
/// </summary>
[TestFixture]
public class TripPropertyTests
{
    private static readonly string[] AllEvents =
    {
        TripGrammar.Full, TripGrammar.DepotWalk, TripGrammar.Unloaded, TripGrammar.SpotWalk, TripGrammar.SpotSkipped, TripGrammar.TripDone,
        TripGrammar.SpotArrived, TripGrammar.SpotGaveUp, TripGrammar.SpotCancelled, TripGrammar.SpotSource, TripGrammar.SpotRemembered
    };

    private static Gen<bool> Bool() { return Gen.Elements(new[] { true, false }); }

    // ---- TripRules.Resume

    [TestCase(39.9f, "TooClose")]
    [TestCase(40f, "Walk")]
    [TestCase(40.1f, "Walk")]
    [TestCase(0f, "TooClose")]
    public void TheSpotIsWalkedToFromFortyMetres(float away, string want)
    {
        Assert.That(TripRules.Resume(true, away, true).ToString(), Is.EqualTo(want));
    }

    [Test]
    public void NoSpotWinsOverEverything()
    {
        Assert.That(TripRules.Resume(false, 500f, true), Is.EqualTo(ResumeDecision.NoSpot));
        Assert.That(TripRules.Resume(false, 0f, false), Is.EqualTo(ResumeDecision.NoSpot));
    }

    [Test]
    public void TooCloseWinsOverTheLeash()
    {
        Assert.That(TripRules.Resume(true, 10f, false), Is.EqualTo(ResumeDecision.TooClose));
    }

    [Test]
    public void Resume_MatchesTheWrittenTableForAnyInput()
    {
        var gen = from known in Bool() from allowed in Bool() from tenth in Gen.Choose(0, 20000) select Tuple.Create(known, allowed, tenth / 10f);
        Props.Check(Prop.ForAll(gen.ToArbitrary(), q =>
        {
            ResumeDecision d = TripRules.Resume(q.Item1, q.Item3, q.Item2);
            ResumeDecision want = !q.Item1 ? ResumeDecision.NoSpot : q.Item3 < 40f ? ResumeDecision.TooClose : q.Item2 ? ResumeDecision.Walk : ResumeDecision.OutsideLeash;
            return d == want;
        }));
    }

    [Test]
    public void Resume_FurtherAwayNeverTurnsAWalkIntoASkip()
    {
        var gen = from known in Bool() from allowed in Bool() from a in Gen.Choose(0, 20000) from extra in Gen.Choose(0, 5000) select Tuple.Create(known, allowed, a / 10f, extra / 10f);
        Props.Check(Prop.ForAll(gen.ToArbitrary(), q =>
        {
            bool near = TripRules.Resume(q.Item1, q.Item3, q.Item2) == ResumeDecision.Walk;
            bool far = TripRules.Resume(q.Item1, q.Item3 + q.Item4, q.Item2) == ResumeDecision.Walk;
            return !near || far;
        }));
    }

    [Test]
    public void OnlyAWalkIsASpotWalkEvent()
    {
        foreach (ResumeDecision d in Enum.GetValues(typeof(ResumeDecision)))
        {
            Assert.That(TripRules.EventOf(d), Is.EqualTo(d == ResumeDecision.Walk ? TripGrammar.SpotWalk : TripGrammar.SpotSkipped));
        }
    }

    // ---- TripGrammar

    /// A random LEGAL trip history: at each step one of the events the grammar allows (chosen by index), built phase by phase from the written rules, not from TripGrammar.Next.
    private static Gen<string[]> LegalTrip()
    {
        return from steps in Gen.ArrayOf(40, Gen.Choose(0, 99)) from len in Gen.Choose(0, 40)
               select Build(steps.Take(len).ToArray());
    }

    private static string[] Build(int[] picks)
    {
        var list = new List<string>();
        int phase = 0; // 0 idle, 1 full, 2 depot walk, 3 unloaded, 4 spot walk
        foreach (int pick in picks)
        {
            var allowed = new List<string> { TripGrammar.Full, TripGrammar.SpotRemembered };
            if (phase == 0 || phase == 1) allowed.Add(TripGrammar.DepotWalk);
            if (phase == 2) allowed.Add(TripGrammar.Unloaded);
            if (phase == 3) { allowed.Add(TripGrammar.SpotWalk); allowed.Add(TripGrammar.SpotSkipped); allowed.Add(TripGrammar.TripDone); }
            if (phase == 4) { allowed.Add(TripGrammar.SpotArrived); allowed.Add(TripGrammar.SpotGaveUp); allowed.Add(TripGrammar.SpotCancelled); allowed.Add(TripGrammar.SpotSource); }
            string e = allowed[pick % allowed.Count];
            list.Add(e);
            switch (e)
            {
                case TripGrammar.Full: phase = 1; break;
                case TripGrammar.DepotWalk: phase = 2; break;
                case TripGrammar.Unloaded: phase = 3; break;
                case TripGrammar.SpotWalk: phase = 4; break;
                case TripGrammar.SpotRemembered: break;
                default: phase = 0; break; // SpotSkipped, TripDone and the four spot ends
            }
        }

        return list.ToArray();
    }

    [Test]
    public void EveryLegalTripIsAccepted_AndSoIsEveryPrefix()
    {
        Props.Check(Prop.ForAll(LegalTrip().ToArbitrary(), trip =>
        {
            string why;
            for (int n = 0; n <= trip.Length; n++)
            {
                if (!TripGrammar.Valid(trip.Take(n).ToList(), out why)) return false;
            }

            return true;
        }));
    }

    [Test]
    public void TheNormalTripsAreLegal()
    {
        string why;
        Assert.That(TripGrammar.Valid(new[] { "Full", "DepotWalk", "Unloaded", "SpotWalk", "SpotArrived" }, out why), Is.True, why);
        Assert.That(TripGrammar.Valid(new[] { "SpotRemembered", "Full", "DepotWalk", "Unloaded", "SpotSkipped" }, out why), Is.True, why);
        Assert.That(TripGrammar.Valid(new[] { "DepotWalk", "Unloaded", "TripDone", "Full", "DepotWalk" }, out why), Is.True, why); // SetupUnload after a logout never says "Full"
        Assert.That(TripGrammar.Valid(new[] { "Full", "DepotWalk", "Unloaded", "SpotWalk", "SpotCancelled", "Full", "DepotWalk", "Unloaded", "SpotWalk", "SpotGaveUp" }, out why), Is.True, why);
    }

    [TestCase("Unloaded")]                                              // an unload without a depot walk
    [TestCase("Full", "Unloaded")]
    [TestCase("Full", "DepotWalk", "SpotWalk")]                         // a decision without an unload
    [TestCase("Full", "DepotWalk", "Unloaded", "SpotWalk", "SpotWalk")] // two decisions for one unload
    [TestCase("Full", "DepotWalk", "Unloaded", "SpotSkipped", "SpotWalk")]
    [TestCase("Full", "DepotWalk", "Unloaded", "TripDone", "SpotArrived")] // a spot end without a spot walk
    [TestCase("SpotGaveUp")]
    [TestCase("Full", "DepotWalk", "DepotWalk")]
    [TestCase("Nonsense")]
    public void IllegalOrdersAreRejected(params string[] events)
    {
        string why;
        Assert.That(TripGrammar.Valid(events, out why), Is.False);
        Assert.That(why, Is.Not.Empty);
    }

    [Test]
    public void Valid_IsSound_ForAnyRandomSequence()
    {
        // whatever the grammar accepts must satisfy the rules in their plain written form (checked without TripGrammar.Next)
        var gen = from len in Gen.Choose(0, 12) from picks in Gen.ArrayOf(len, Gen.Choose(0, AllEvents.Length - 1)) select picks.Select(i => AllEvents[i]).ToArray();
        Props.Check(Prop.ForAll(gen.ToArbitrary(), seq =>
        {
            string why;
            if (!TripGrammar.Valid(seq, out why)) return true;
            var core = seq.Where(e => e != TripGrammar.SpotRemembered).ToArray();
            for (int i = 0; i < core.Length; i++)
            {
                string prev = i > 0 ? core[i - 1] : "";
                switch (core[i])
                {
                    case TripGrammar.Unloaded: if (prev != TripGrammar.DepotWalk) return false; break;
                    case TripGrammar.SpotWalk:
                    case TripGrammar.SpotSkipped:
                    case TripGrammar.TripDone: if (prev != TripGrammar.Unloaded) return false; break;
                    case TripGrammar.SpotArrived:
                    case TripGrammar.SpotGaveUp:
                    case TripGrammar.SpotCancelled:
                    case TripGrammar.SpotSource: if (prev != TripGrammar.SpotWalk) return false; break;
                    case TripGrammar.DepotWalk: if (prev != "" && prev != TripGrammar.Full && !IsEnd(prev)) return false; break;
                }
            }

            int unloads = core.Count(e => e == TripGrammar.Unloaded);
            int decisions = core.Count(e => e == TripGrammar.SpotWalk || e == TripGrammar.SpotSkipped || e == TripGrammar.TripDone);
            return decisions <= unloads;
        }));
    }

    private static bool IsEnd(string e)
    {
        return e == TripGrammar.SpotSkipped || e == TripGrammar.TripDone || e == TripGrammar.SpotArrived || e == TripGrammar.SpotGaveUp || e == TripGrammar.SpotCancelled || e == TripGrammar.SpotSource;
    }

    [Test]
    public void LastDecisionAndLastSpotEnd_ReadTheNewestEvent()
    {
        var trip = new[] { "Full", "DepotWalk", "Unloaded", "SpotWalk", "SpotArrived", "Full", "DepotWalk", "Unloaded", "SpotWalk" };
        Assert.That(TripGrammar.LastDecision(trip), Is.EqualTo("SpotWalk"));
        Assert.That(TripGrammar.LastSpotEnd(trip), Is.EqualTo(""), "the newest walk has not ended; the older one's end must not be reported");
        Assert.That(TripGrammar.LastSpotEnd(trip.Concat(new[] { "SpotGaveUp" }).ToArray()), Is.EqualTo("SpotGaveUp"));
        Assert.That(TripGrammar.LastDecision(new string[0]), Is.EqualTo(""));
    }

    // ---- TrollJournal

    [SetUp]
    public void Fresh() { TrollJournal.Clear(); }

    [Test]
    public void TheJournalKeepsTheNewestEventsInOrder_AndStaysBounded()
    {
        var gen = from n in Gen.Choose(0, 300) from trolls in Gen.Choose(1, 200) select Tuple.Create(n, trolls);
        Props.Check(Prop.ForAll(gen.ToArbitrary(), q =>
        {
            TrollJournal.Clear();
            for (int i = 0; i < q.Item1; i++)
            {
                TrollJournal.Add("t" + (i % q.Item2), i, "e" + i);
            }

            for (int t = 0; t < q.Item2; t++)
            {
                List<string> names = TrollJournal.Names("t" + t);
                if (names.Count > TrollJournal.MaxPerTroll) return false;
                // oldest first: the numbers only grow
                int last = -1;
                foreach (string s in names)
                {
                    int v = int.Parse(s.Substring(1));
                    if (v <= last) return false;
                    last = v;
                }
            }

            return true;
        }));
    }

    [Test]
    public void ATrollWithoutAnIdRecordsNothing()
    {
        Assert.That(TrollJournal.Add("", 1f, "Full"), Is.EqualTo(0));
        Assert.That(TrollJournal.Add(null, 1f, "Full"), Is.EqualTo(0));
        Assert.That(TrollJournal.Text("nobody"), Is.EqualTo("(no events)"));
    }

    [Test]
    public void TextShowsEventsWithDetails()
    {
        TrollJournal.Add("a", 1f, "Full");
        TrollJournal.Add("a", 2f, "Unloaded", "96 stacks");
        Assert.That(TrollJournal.Text("a"), Is.EqualTo("Full | Unloaded (96 stacks)"));
        Assert.That(TrollJournal.Count("a", "Unloaded"), Is.EqualTo(1));
        Assert.That(TrollJournal.DetailOf("a", "Unloaded"), Is.EqualTo("96 stacks"));
        Assert.That(TrollJournal.Last("a").Name, Is.EqualTo("Unloaded"));
    }
}
