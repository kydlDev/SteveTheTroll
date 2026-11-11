using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;

namespace SteveTheTroll.Tests.Unit;

/// <summary>2026-11-10 (agent26): the depot unload range is one pure rule that Recovery must respect (a ledge walk that stops short of the depot).</summary>
[TestFixture]
public class TripUnloadRangeTests
{
    [TestCase(0f, true)]
    [TestCase(1.9f, true)]
    [TestCase(10f, true)]
    [TestCase(10.01f, false)]
    [TestCase(60f, false)]
    public void Unload_range_is_the_ten_metre_rule(float flatMetres, bool expected)
    {
        Assert.That(TripRules.InUnloadRange(flatMetres), Is.EqualTo(expected));
    }

    // a26rep2: the ledge walk stopped 1.9 m short of the depot (the depot piece stands on the walk's goal), well inside the range - the unload must not wait for the walk's 1.5 m arrival
    [Test]
    public void A_ledge_walk_that_stops_short_of_the_depot_is_still_in_range()
    {
        Assert.That(TripRules.InUnloadRange(1.9f), Is.True);
        Assert.That(TripRules.UnloadMetres, Is.GreaterThan(1.9f));
    }
}
