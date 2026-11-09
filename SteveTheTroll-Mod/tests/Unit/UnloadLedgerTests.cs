using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Farming.Infrastructure;

namespace SteveTheTroll.Tests.Unit;

/// <summary>2026-11-08 (full11 obstacle-route): a delivery into depots is recorded as an event per troll, so a later pickup of loose items cannot undo "it unloaded".</summary>
[TestFixture]
public class UnloadLedgerTests
{
    [SetUp]
    public void Fresh() { FarmUnloadLedger.Clear(); }

    [Test]
    public void ATrollThatNeverUnloadedHasNoRecord()
    {
        FarmUnloadLedger.Entry e;
        Assert.That(FarmUnloadLedger.Of("1:1", out e), Is.False);
        Assert.That(e.Moved, Is.EqualTo(0));
    }

    [Test]
    public void UnloadsAddUpPerTroll()
    {
        Assert.That(FarmUnloadLedger.Record("1:1", 60, 36), Is.EqualTo(60));
        Assert.That(FarmUnloadLedger.Record("1:1", 36, 0), Is.EqualTo(96));
        Assert.That(FarmUnloadLedger.Record("2:2", 5, 0), Is.EqualTo(5));
        FarmUnloadLedger.Entry a, b;
        Assert.That(FarmUnloadLedger.Of("1:1", out a), Is.True);
        Assert.That(FarmUnloadLedger.Of("2:2", out b), Is.True);
        Assert.That(a.Unloads, Is.EqualTo(2));
        Assert.That(a.Moved, Is.EqualTo(96));
        Assert.That(a.LastMoved, Is.EqualTo(36));
        Assert.That(a.LastLeft, Is.EqualTo(0));
        Assert.That(b.Moved, Is.EqualTo(5));
    }

    [Test]
    public void ANegativeMoveCountIsNeverSubtracted()
    {
        FarmUnloadLedger.Record("1:1", 10, 0);
        Assert.That(FarmUnloadLedger.Record("1:1", -1, 4), Is.EqualTo(10));
        FarmUnloadLedger.Entry e;
        FarmUnloadLedger.Of("1:1", out e);
        Assert.That(e.LastLeft, Is.EqualTo(4));
    }

    [Test]
    public void ClearForgetsEveryTroll()
    {
        FarmUnloadLedger.Record("1:1", 3, 0);
        Assert.That(FarmUnloadLedger.Clear(), Is.EqualTo(0));
        FarmUnloadLedger.Entry e;
        Assert.That(FarmUnloadLedger.Of("1:1", out e), Is.False);
    }
}
