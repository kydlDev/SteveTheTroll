using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Registry.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>NEXT_STEPS item 4: the frozen-troll detector and the self-heal ladder (game-free rules).</summary>
[TestFixture]
public class FrozenRulesTests
{
    [TestCase(0L, true)] [TestCase(1L, false)] [TestCase(709060242L, false)]
    public void ShouldClaim_OnlyAnUnownedTroll(long ownerId, bool expected) { Assert.That(FrozenRules.ShouldClaim(ownerId), Is.EqualTo(expected)); }

    [TestCase(true, false, false, false, true)] [TestCase(true, true, false, false, false)] [TestCase(true, false, true, false, false)] [TestCase(true, false, false, true, false)] [TestCase(false, false, false, false, false)]
    public void ExpectsMovement_NotWhileHeldFightingOrStaggering(bool travelling, bool held, bool attack, bool stagger, bool expected) { Assert.That(FrozenRules.ExpectsMovement(travelling, held, attack, stagger), Is.EqualTo(expected)); }

    [TestCase(0f, false)] [TestCase(0.49f, false)] [TestCase(0.5f, true)] [TestCase(30f, true)]
    public void Moved(float metres, bool expected) { Assert.That(FrozenRules.Moved(metres), Is.EqualTo(expected)); }

    [TestCase(true, 0f, "None")] [TestCase(true, 9.9f, "None")] [TestCase(true, 10f, "Reclaim")]
    [TestCase(true, 29.9f, "Reclaim")] [TestCase(true, 30f, "Rebuild")] [TestCase(true, 59.9f, "Rebuild")]
    [TestCase(true, 60f, "CatchUp")] [TestCase(true, 600f, "CatchUp")]
    // a troll that is not meant to travel (farming in place, idle, waiting) is never frozen
    [TestCase(false, 600f, "None")]
    public void StageFor(bool travelling, float seconds, string expected)
    { Assert.That(FrozenRules.StageFor(travelling, seconds).ToString(), Is.EqualTo(expected)); }

    [TestCase(100f, 25f)] [TestCase(25f, 25f)] [TestCase(10f, 10f)] [TestCase(0f, 0f)] [TestCase(-5f, 0f)]
    public void CatchUpStep_NeverPastTheDestination(float distance, float expected)
    { Assert.That(FrozenRules.CatchUpStep(distance), Is.EqualTo(expected)); }
}
