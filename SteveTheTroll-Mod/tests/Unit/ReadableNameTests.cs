using NUnit.Framework;
using SteveTheTroll.Features.Trolls.Orders.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>D-214 (user 2026-11-10): the English name of a manual-attack target when the game has no hover name.</summary>
[TestFixture]
public class ReadableNameTests
{
    [TestCase("Beech_small1", "Small beech tree")]
    [TestCase("Beech1(Clone)", "Beech tree")]
    [TestCase("FirTree_oldLog", "Old fir log")]
    [TestCase("stubbe", "Stump")]
    [TestCase("Rock_3", "Rock")]
    [TestCase("rock2_cell", "Rock")]
    [TestCase("MineRock_Copper", "Copper rock")]
    [TestCase("MineRock_Tin", "Tin rock")]
    [TestCase("SwampTree1_log", "Swamp log")]
    [TestCase("SomeThing_x2", "Some thing x")]
    [TestCase("", "")]
    public void FromPrefab_GivesTheWordsAPlayerKnows(string prefab, string expected)
    {
        Assert.That(ReadableName.FromPrefab(prefab), Is.EqualTo(expected));
    }

    [Test]
    public void NullIsEmpty()
    {
        Assert.That(ReadableName.FromPrefab(null), Is.EqualTo(""));
    }
}
