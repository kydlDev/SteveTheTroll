using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SteveTheTroll.Tests.Integration;

/// <summary>
/// User rule: the player config holds only what a player should change, everything else is a Tunable (dev-only, shown by KydlTools).
/// KydlTools finds Tunables by reflection (type name "Tunable`1", properties Section / Name / Description / Default / Value / External):
/// these tests keep that contract from breaking.
/// </summary>
[TestFixture]
public class SettingsTests
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static List<object> Tunables()
    {
        var list = new List<object>();
        foreach (var f in ModAssembly.Type("SteveTheTroll.Configuration.ModSettings").GetFields(Static))
            if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition().Name == "Tunable`1") list.Add(f.GetValue(null));
        return list;
    }

    [Test]
    public void Tunables_HaveTheMembersKydlToolsReads()
    {
        foreach (var t in Tunables())
            foreach (var p in new[] { "Section", "Name", "Description", "Default", "Value", "External" })
                Assert.That(t.GetType().GetProperty(p), Is.Not.Null, "Tunable property " + p + " missing (KydlTools reads it)");
    }

    [Test]
    public void Tunables_StartAtTheirFixedValue()
    {
        foreach (var t in Tunables())
        {
            var ty = t.GetType();
            Assert.That(ty.GetProperty("Value").GetValue(t, null), Is.EqualTo(ty.GetProperty("Default").GetValue(t, null)), t.ToString());
        }
    }

    [Test]
    public void PlayerConfig_HasDebugLogSwitch()
    {
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        Assert.That(Regex.IsMatch(src, @"Bind\(\s*""DebugLog""\s*,\s*""Enable""\s*,\s*false"), Is.True, "[DebugLog] Enable (off by default) is required in every mod");
    }

    [Test]
    public void PlayerConfig_HasEnableMod()
    {
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        Assert.That(Regex.IsMatch(src, @"Bind\(\s*""General""\s*,\s*""Enable Mod"""), Is.True, "[General] Enable Mod (on by default) is the global switch of every mod");
    }

    [Test]
    public void PlayerConfig_EnableModIsTheFirstSettingOfGeneral()
    {
        // user, 2026-11-17: [General] Enable Mod is the first setting of every mod in the F6 hub (the order the entries are bound in) and in the
        // F1 configuration manager (the Order tag of ConfigOrder.First)
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        Match first = Regex.Match(src, @"\.Bind\(\s*""General""\s*,\s*""(?<k>[^""]+)""");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Success && first.Groups["k"].Value == "Enable Mod", Is.True, "the first [General] entry bound must be 'Enable Mod'");
            Assert.That(Regex.IsMatch(src, @"""Enable Mod""\s*,\s*Old\([^)]*\)\s*,\s*ConfigOrder\.First\("), Is.True, "'Enable Mod' carries the F1 order tag (ConfigOrder.First)");
        }
    }
    [Test]
    public void PlayerConfig_EnableSwitchesLiveInGeneral()
    {
        // user 2026-11-15: every functionality switch is an "Enable <name>" entry in [General]; only [DebugLog] keeps its own Enable
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        foreach (Match m in Regex.Matches(src, @"\.Bind\(\s*""(?<s>[^""]+)""\s*,\s*""(?<k>[^""]+)"""))
        {
            string s = m.Groups["s"].Value, k = m.Groups["k"].Value;
            if (k.StartsWith("Enable", System.StringComparison.Ordinal) && s != "DebugLog")
                Assert.That(s, Is.EqualTo("General"), "[" + s + "] " + k + ": the Enable switches are all in [General]");
        }
    }

    [Test]
    public void PlayerConfig_HasTheFunctionalitySwitches()
    {
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        foreach (var name in new[] { "Enable Slippers", "Enable Costume", "Enable Trolls", "Enable Troll Hub", "Enable Riding" })
            Assert.That(Regex.IsMatch(src, @"Bind\(\s*""General""\s*,\s*""" + name + @""""), Is.True, "[General] " + name + " is the switch of that functionality");
    }

    [Test]
    public void PlayerConfig_HasTheTrollBreakSwitches()
    {
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        foreach (var name in new[] { "Two star trolls break fortress walls", "Trolls mine Petrified Gammeltroll" })
            Assert.That(Regex.IsMatch(src, @"Bind\(\s*""Trolls""\s*,\s*""" + name + @"""\s*,\s*true"), Is.True, "[Trolls] " + name + " (on by default)");
    }

    [Test]
    public void PlayerConfig_HasNoUnlockWord()
    {
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        Assert.That(src.Contains("UnlockWord"), Is.False, "the stt_ commands exist only with KydlTools loaded; there is no unlock word option");
    }

    [Test]
    public void PlayerConfig_HasNoMenuKey()
    {
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Regex.IsMatch(src, @"Bind\(\s*""[^""]*""\s*,\s*""MenuKey"""), Is.False, "no mod has its own menu key: the menu is tabs in the KY hub (F6, KydlCore)");
            Assert.That(Regex.IsMatch(src, @"ConfigEntry<KeyCode>\s+MenuKey"), Is.False, "no MenuKey entry (KydlCore owns the hub key)");
        }
    }

    [Test]
    public void ConfigDescriptions_DoNotTalkAboutOldVersions()
    {
        var src = File.ReadAllText(Path.Combine(TestPaths.Src, "Configuration", "ModSettings.cs"));

        foreach (Match m in Regex.Matches(src, @"Bind\([^;]*?""(?<d>[^""]{20,})"""))
            Assert.That(Regex.IsMatch(m.Groups["d"].Value, @"\b(previous|formerly|renamed|old value|used to|before version)\b", RegexOptions.IgnoreCase), Is.False, m.Groups["d"].Value);
    }
}
