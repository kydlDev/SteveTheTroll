using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SteveTheTroll.Tests.Integration;

/// <summary>
///     T-217 R1 (manual test 20261110-015153: the mod's own farming / recovery brain walked a ridden troll 6.5 m): every autonomous driver of a troll asks
///     RiddenGate.Refuses FIRST, and the riding AI-skip prefix runs before the farming prefix (D-216). PatchTargetsTests only proves the patch targets exist; this
///     guards how the two UpdateAI prefixes and the drivers compose. A new driver is added to the list here together with its gate.
/// </summary>
[TestFixture]
public class RiddenGateRuleTests
{
    // file (under src) -> the method whose body must ask the gate, and the driver name it must give
    static readonly string[][] Drivers =
    {
        new[] { @"Features\Trolls\Farming\Infrastructure\TrollFarming.Part2.cs", "bool Prefix(MonsterAI __instance, float dt, ref bool __result)", "TrollFarming.Steer" },
        new[] { @"Features\Trolls\Farming\Infrastructure\TrollFarmer.cs", "bool Step(MonsterAI ai, float dt)", "TrollFarmer.Step" },
        new[] { @"Features\Trolls\Recovery\Infrastructure\TrollRecovery.cs", "bool Tick(float dt, Vector3 goal", "TrollRecovery.Tick" },
        new[] { @"Features\Trolls\Recovery\Infrastructure\TrollRecovery.Part2.cs", "bool Drive(float dt, ITrollCombat combat)", "TrollRecovery.Drive" },
        new[] { @"Features\Trolls\Recovery\Infrastructure\TrollRecovery.Ledge.cs", "bool LedgeStep(float dt, ITrollMovement mv)", "TrollRecovery.LedgeStep" },
        new[] { @"Features\Trolls\Combat\Infrastructure\TrollCounter.cs", "bool Step(MonsterAI ai, float dt)", "TrollChase.Step" },
        new[] { @"Features\Trolls\Combat\Infrastructure\TrollRage.cs", "bool Step(MonsterAI ai, float dt)", "TrollRage.Step" },
        new[] { @"Features\Trolls\Combat\Infrastructure\TrollSlam.cs", "bool Step(MonsterAI ai)", "TrollSlam.Step" },
        new[] { @"Features\Trolls\Home\Infrastructure\TrollReturn.cs", "bool Step(MonsterAI ai, float dt)", "TrollReturn.Step" },
        new[] { @"Features\Trolls\Home\Infrastructure\TrollIdle.cs", "int Tick()", "TrollIdle.Tick" },
    };

    static IEnumerable<TestCaseData> DriverCases()
    {
        foreach (string[] d in Drivers)
        {
            yield return new TestCaseData(d[0].Replace(@"\\", @"\"), d[1], d[2]).SetName("Driver_AsksTheRiddenGate_" + d[2].Replace('.', '_'));
        }
    }

    /// The text of the method that starts at `signature`, up to the next member that starts at the same indentation with an access modifier (close enough for a guard).
    static string MethodText(string text, string signature)
    {
        int at = text.IndexOf(signature, System.StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        Match next = Regex.Match(text.Substring(at + signature.Length), @"\n\s*(///|internal |private |public |protected )");
        return next.Success ? text.Substring(at, signature.Length + next.Index) : text.Substring(at);
    }

    [TestCaseSource(nameof(DriverCases))]
    public void Driver_AsksTheRiddenGate(string file, string signature, string driver)
    {
        string path = Path.Combine(TestPaths.Src, file);
        Assert.That(File.Exists(path), Is.True, "driver file moved or renamed: " + path + " - update the list in this test together with its gate");
        string method = MethodText(File.ReadAllText(path), signature);
        Assert.That(method, Is.Not.Null, file + ": method '" + signature + "' not found - update the list in this test together with its gate");
        Assert.That(method, Does.Contain("RiddenGate.Refuses(").And.Contain("\"" + driver + "\""),
            file + ": '" + signature + "' must ask RiddenGate.Refuses(troll, \"" + driver + "\") before it moves, turns or orders the troll (T-217 R1)");
    }

    [Test]
    public void LedgeStep_AsksTheGate_BeforeItWritesTheRootPosition()
    {
        string text = File.ReadAllText(Path.Combine(TestPaths.Src, @"Features\Trolls\Recovery\Infrastructure\TrollRecovery.Ledge.cs".Replace(@"\\", @"\")));
        string method = MethodText(text, "bool LedgeStep(float dt, ITrollMovement mv)");
        Assert.That(method, Is.Not.Null);
        int gate = method.IndexOf("RiddenGate.Refuses(", System.StringComparison.Ordinal);
        int write = method.IndexOf("transform.position =", System.StringComparison.Ordinal);
        Assert.That(write, Is.GreaterThan(0), "the direct root write this test guards is gone - review the test (T-217 R2)");
        Assert.That(gate, Is.GreaterThanOrEqualTo(0).And.LessThan(write), "T-217 R2: the ledge step writes transform.position itself; the ridden gate must come first");
    }

    [Test]
    public void RidingAiSkip_RunsBeforeTheFarmingPrefix()
    {
        // D-216: Harmony only skips the prefixes AFTER one that returns false; with the default priority the farming brain ran first on the ridden troll
        string text = File.ReadAllText(Path.Combine(TestPaths.Src, @"Integrations\Harmony\RidingPatches.cs".Replace(@"\\", @"\")));
        int cls = text.IndexOf("class RidingPatch_SkipAi", System.StringComparison.Ordinal);
        Assert.That(cls, Is.GreaterThanOrEqualTo(0), "RidingPatch_SkipAi not found");
        int nextClass = text.IndexOf("internal static class", cls + 10, System.StringComparison.Ordinal);
        string body = nextClass > 0 ? text.Substring(cls, nextClass - cls) : text.Substring(cls);
        Assert.That(body, Does.Contain("[HarmonyPriority(Priority.First)]"), "RidingPatch_SkipAi.Prefix must keep [HarmonyPriority(Priority.First)] (D-216)");
    }

    [Test]
    public void TheGateItself_CountsAndLogs()
    {
        string text = File.ReadAllText(Path.Combine(TestPaths.Src, @"Features\Trolls\Infrastructure\RiddenGate.cs".Replace(@"\\", @"\")));
        Assert.That(text, Does.Contain("TrollRiding.Is(").And.Contain("Refusals++").And.Contain("ModLog."), "RiddenGate.Refuses decides by TrollRiding.Is, counts and logs every refusal");
    }
}
