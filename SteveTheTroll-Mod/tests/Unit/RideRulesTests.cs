using NUnit.Framework;
using SteveTheTroll.Features.Riding.Domain;

namespace SteveTheTroll.Tests.Unit;

/// <summary>T-213 (user decisions 2026-11-06): the game-free rules of the rideable troll, the recipe and the mount file.</summary>
[TestFixture]
public class RideRulesTests
{
    // Decide(unlocked, hasHalter, tamed, mine, dead, hasOtherRider, ridingTroll, thisTroll)
    [Test]
    public void Mounts_AFreeOwnTamedTroll_WithTheHalter()
    { Assert.That(RideRules.Decide(true, true, true, true, false, false, 0, 7), Is.EqualTo(MountVerdict.Mount)); }

    [Test]
    public void SameKeyDismounts_TheTrollItRides()
    { Assert.That(RideRules.Decide(true, true, true, true, false, false, 7, 7), Is.EqualTo(MountVerdict.Dismount)); }

    [Test]
    public void NoPrompt_OnAnotherTroll_WhileMounted()
    {
        MountVerdict v = RideRules.Decide(true, true, true, true, false, false, 7, 8);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(v, Is.EqualTo(MountVerdict.RidingAnother));
            Assert.That(RideRules.PromptVisible(v), Is.False);
        }
    }

    [Test]
    public void NoHalter_NoPrompt()
    {
        MountVerdict v = RideRules.Decide(true, false, true, true, false, false, 0, 7);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(v, Is.EqualTo(MountVerdict.NoHalter));
            Assert.That(RideRules.PromptVisible(v), Is.False);
        }
    }

    [Test]
    public void Locked_BeforeTheRecipeIsUnlocked_EvenWithTheItem()
    { Assert.That(RideRules.Decide(false, true, true, true, false, false, 0, 7), Is.EqualTo(MountVerdict.Locked)); }

    [TestCase(false, true, false)] // wild
    [TestCase(true, false, false)] // somebody else's
    [TestCase(true, true, true)]   // dead
    public void NotRideable_WildForeignOrDead(bool tamed, bool mine, bool dead)
    { Assert.That(RideRules.Decide(true, true, tamed, mine, dead, false, 0, 7), Is.EqualTo(MountVerdict.NotRideable)); }

    [Test]
    public void OneRiderPerTroll()
    { Assert.That(RideRules.Decide(true, true, true, true, false, true, 0, 7), Is.EqualTo(MountVerdict.Occupied)); }

    [TestCase(MountVerdict.Mount, true)]
    [TestCase(MountVerdict.Dismount, true)]
    [TestCase(MountVerdict.NoHalter, false)]
    [TestCase(MountVerdict.Locked, false)]
    [TestCase(MountVerdict.NotRideable, false)]
    [TestCase(MountVerdict.Occupied, false)]
    [TestCase(MountVerdict.RidingAnother, false)]
    public void PromptOnlyWhenTheKeyDoesSomething(MountVerdict v, bool shown)
    { Assert.That(RideRules.PromptVisible(v), Is.EqualTo(shown)); }

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void CollectingPauses_OnlyWhenRiddenAndFull(bool ridden, bool full, bool paused)
    { Assert.That(RideRules.CollectingPaused(ridden, full), Is.EqualTo(paused)); }

    [Test]
    public void RiddenTroll_TakesNoOrders_IgnoresMouse4_OthersFollow()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RideRules.OrdersSuspended(true), Is.True);
            Assert.That(RideRules.OrdersSuspended(false), Is.False);
            Assert.That(RideRules.FollowsOrderKey(true), Is.False, "the ridden troll ignores Mouse-4");
            Assert.That(RideRules.FollowsOrderKey(false), Is.True, "another troll follows it");
        }
    }

    [TestCase(false, true, 10f, 20f, true)]
    [TestCase(false, true, 25f, 20f, false)]
    [TestCase(false, false, 5f, 20f, false)]
    [TestCase(true, true, 5f, 20f, false)]
    public void ASecondTroll_GuardsTheRider(bool ridden, bool attacked, float distance, float range, bool guards)
    { Assert.That(RideRules.GuardsTheRider(ridden, attacked, distance, range), Is.EqualTo(guards)); }

    [Test]
    public void Damage_GoesToThePlayerOnlyWhenThePlayerWasHit()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RideRules.DamageHitsPlayer(true), Is.True);
            Assert.That(RideRules.DamageHitsPlayer(false), Is.False);
        }
    }

    [TestCase(EndReason.PlayerDismounted, EndOutcome.ItemToInventory)]
    [TestCase(EndReason.TrollDied, EndOutcome.ItemToInventory)]
    [TestCase(EndReason.Teleported, EndOutcome.ItemToInventory)]
    [TestCase(EndReason.LoggedOut, EndOutcome.ItemToInventory)]
    [TestCase(EndReason.PlayerDied, EndOutcome.ItemToTombstone)]
    public void TheItemGoesBack_ExceptAfterDeath(EndReason why, EndOutcome outcome)
    { Assert.That(RideRules.End(why), Is.EqualTo(outcome)); }

    [TestCase(false, false, 0L, LoginRecovery.None)]
    [TestCase(false, true, 7L, LoginRecovery.None)]
    [TestCase(true, false, 0L, LoginRecovery.WaitForTroll)]
    [TestCase(true, false, 9L, LoginRecovery.WaitForTroll)]
    [TestCase(true, true, 7L, LoginRecovery.ReleaseTroll)]
    [TestCase(true, true, 0L, LoginRecovery.ReleaseTroll)]
    [TestCase(true, true, 9L, LoginRecovery.LeaveToOtherRider)]
    public void Login_RecoversTheStoredTroll_OnceLoaded_UnlessAnotherPlayerRidesIt(bool fileNamesTroll, bool loaded, long rider, LoginRecovery step)
    { Assert.That(RideRules.OnLogin(fileNamesTroll, loaded, rider, 7L), Is.EqualTo(step)); }

    [Test]
    public void Login_AfterACrashWhileRiding_ReleasesOurOwnMarker()
    {
        // the bug fixed 2026-11-17: the game closed while riding left "ridden by me" on the troll and nothing ever read the mount file
        string text = MountFile.Write(7L, "abc123");
        string troll;
        bool stored = MountFile.TryRead(text, 7L, out troll);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored && troll == "abc123", Is.True);
            Assert.That(RideRules.OnLogin(stored, true, 7L, 7L), Is.EqualTo(LoginRecovery.ReleaseTroll));
        }
    }

    [Test]
    public void Recipe_IsWhatTheUserAsked()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RideRecipe.StationLevel, Is.EqualTo(3));
            Assert.That(RideRecipe.MaxQuality, Is.EqualTo(1), "no upgrades");
            Assert.That(RideRecipe.Lines.Count, Is.EqualTo(4));
            Assert.That(RideRecipe.Lines[0].Prefab, Is.EqualTo("WolfPelt")); Assert.That(RideRecipe.Lines[0].Amount, Is.EqualTo(15));
            Assert.That(RideRecipe.Lines[1].Prefab, Is.EqualTo("LinenThread")); Assert.That(RideRecipe.Lines[1].Amount, Is.EqualTo(15));
            Assert.That(RideRecipe.Lines[2].Prefab, Is.EqualTo("Iron")); Assert.That(RideRecipe.Lines[2].Amount, Is.EqualTo(10));
            Assert.That(RideRecipe.Lines[3].Prefab, Is.EqualTo("RoundLog")); Assert.That(RideRecipe.Lines[3].Amount, Is.EqualTo(10));
            Assert.That(RideRecipe.Lines[0].English, Is.EqualTo("Wolf Pelt"));
            Assert.That(RideRecipe.Lines[1].English, Is.EqualTo("Linen Thread"));
            Assert.That(RideRecipe.Lines[3].English, Is.EqualTo("Round Log"));
        }
    }

    [Test]
    public void MountFile_RoundTrips_ForThisPlayerOnly()
    {
        string line = MountFile.Write(42, "af105cfd5369");
        string troll;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(line, Is.EqualTo("player=42;troll=af105cfd5369"));
            Assert.That(MountFile.TryRead(line, 42, out troll), Is.True);
            Assert.That(troll, Is.EqualTo("af105cfd5369"));
            Assert.That(MountFile.TryRead(line, 43, out troll), Is.False, "another player's line");
            Assert.That(troll, Is.Empty);
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("garbage")]
    [TestCase("player=x;troll=a")]
    [TestCase("player=42;troll=")]
    [TestCase("player=42")]
    [TestCase("troll=a;player=42")]
    public void MountFile_DamagedOrEmpty_IsNotMounted(string text)
    {
        string troll;
        Assert.That(MountFile.TryRead(text, 42, out troll), Is.False);
    }

    [Test]
    public void MountFile_Dismounted_WritesAnEmptyFile()
    { Assert.That(MountFile.Write(42, ""), Is.Empty); }

    [Test]
    public void MountFile_ReadsOnlyTheFirstLine()
    {
        string troll;
        Assert.That(MountFile.TryRead("player=42;troll=ab\r\nplayer=43;troll=cd\r\n", 42, out troll), Is.True);
        Assert.That(troll, Is.EqualTo("ab"));
    }
}
