using TMM.Tests.Helpers;

namespace TMM.Tests;

/// <summary>
/// Integration tests for <see cref="RuleEngine.FindMatchingRules"/>.
///
/// These tests build <see cref="GameConfig"/> instances that mirror the
/// routing rules in gta3.tmmgame and verify that:
///   • each condition type evaluates correctly against real (temp) file paths,
///   • ModType-scoped rules are returned before game-wide rules,
///   • <see cref="RuleEngine.ResolveConflict"/> picks the highest-priority rule.
///
/// Files are created on disk only where a condition reads the file system
/// (HasFolder, FolderCount, FileCount). Extension/path conditions work purely
/// on string values so no real file is needed.
/// </summary>
public class RuleEngineTests
{
    // ── Shared profile fixture ────────────────────────────────────────────────

    /// <summary>
    /// Builds a GTA III–style profile from scratch using the same rules that
    /// gta3.tmmgame defines, so tests remain self-contained and fast.
    /// </summary>
    private static GameConfig BuildGta3Profile() => new()
    {
        GameName      = "Grand Theft Auto III",
        GameDirectory = @"C:\FakeGTA3",
        CompanionSiblings = new Dictionary<string, List<string>>
        {
            ["cleo"] = new() { "CLEO_TEXT", "CLEO_FONTS" },
        },
        RoutingRules  =
        [
            new RoutingRule
            {
                Name       = "ModLoader Tree",
                Conditions =
                [
                    new Condition { Type = ConditionType.PathContains, Operator = ConditionOperator.StartsWith, Value = "modloader" },
                ],
                TargetPath = "modloader",
                Priority   = 95,
            },
            // Priority 80 — .asi AND the mod folder contains a "scripts" sub-dir
            new RoutingRule
            {
                Name       = "ASI Plugin (scripts/ if exists)",
                Conditions =
                [
                    new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi", Logic = LogicOperator.AND },
                    new Condition { Type = ConditionType.HasFolder,     Operator = ConditionOperator.Is, Value = "scripts" },
                ],
                TargetPath = "scripts",
                Priority   = 80,
            },
            // Priority 50 — .asi fallback when no scripts/ subfolder
            new RoutingRule
            {
                Name       = "ASI Plugin (root fallback)",
                Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi" } ],
                TargetPath = ".",
                Priority   = 50,
            },
            // Priority 60 — CLEO script
            new RoutingRule
            {
                Name       = "CLEO Script",
                Conditions =
                [
                    new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".cs",  Logic = LogicOperator.OR },
                    new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".cs4", Logic = LogicOperator.OR },
                    new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".cs5" },
                ],
                TargetPath = "cleo",
                Priority   = 60,
            },
            // Priority 60 — CLEO FXT
            new RoutingRule
            {
                Name       = "CLEO FXT",
                Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".fxt" } ],
                TargetPath = "cleo",
                Priority   = 60,
            },
            // Priority 10 — default catch-all
            new RoutingRule
            {
                Name       = "Default (game root)",
                Conditions = [],
                TargetPath = ".",
                Priority   = 10,
                IsDefault  = true,
            },
        ],
        OverlayFolders = ["models", "data", "audio", "text", "anim", "modloader"],
    };

    // ── HasFolder condition ───────────────────────────────────────────────────

    [Fact]
    public void FindMatchingRules_AsiFile_WithScriptsSubfolder_ReturnsThreeMatches()
    {
        // The mod folder contains a "scripts/" subdirectory, so the
        // high-priority ASI rule fires in addition to the fallback and default.
        using var tmp = new TempDirectory();
        string modFolder = tmp.CreateSubDir("MyMod");
        Directory.CreateDirectory(Path.Combine(modFolder, "scripts"));
        string asiFile = Path.Combine(modFolder, "mymod.asi");
        File.WriteAllText(asiFile, "fake asi");

        var matches = new RuleEngine().FindMatchingRules(asiFile, modFolder, BuildGta3Profile());

        Assert.Equal(3, matches.Count);
        Assert.Contains(matches, r => r.Name == "ASI Plugin (scripts/ if exists)");
        Assert.Contains(matches, r => r.Name == "ASI Plugin (root fallback)");
        Assert.Contains(matches, r => r.Name == "Default (game root)");
    }

    [Fact]
    public void FindMatchingRules_AsiFile_WithoutScriptsSubfolder_ReturnsTwoMatches()
    {
        // HasFolder condition fails → priority-80 rule does NOT match.
        // Only the fallback rule and the default catch-all match.
        using var tmp = new TempDirectory();
        string modFolder = tmp.CreateSubDir("MyMod");
        string asiFile = Path.Combine(modFolder, "mymod.asi");
        File.WriteAllText(asiFile, "fake asi");

        var matches = new RuleEngine().FindMatchingRules(asiFile, modFolder, BuildGta3Profile());

        Assert.Equal(2, matches.Count);
        Assert.DoesNotContain(matches, r => r.Name == "ASI Plugin (scripts/ if exists)");
        Assert.Contains(matches, r => r.Name == "ASI Plugin (root fallback)");
        Assert.Contains(matches, r => r.Name == "Default (game root)");
    }

    // ── Extension matching ────────────────────────────────────────────────────

    [Fact]
    public void FindMatchingRules_CsFile_MatchesCleoScriptRuleAndDefault()
    {
        using var tmp = new TempDirectory();
        string modFolder = tmp.CreateSubDir("CleoMod");
        string csFile    = Path.Combine(modFolder, "myscript.cs");
        File.WriteAllText(csFile, "cs script");

        var matches = new RuleEngine().FindMatchingRules(csFile, modFolder, BuildGta3Profile());

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, r => r.Name == "CLEO Script");
        Assert.Contains(matches, r => r.Name == "Default (game root)");
    }

    [Theory]
    [InlineData(".cs4")]
    [InlineData(".cs5")]
    public void FindMatchingRules_CleoVariants_MatchCleoScriptRuleAndDefault(string extension)
    {
        using var tmp = new TempDirectory();
        string modFolder = tmp.CreateSubDir("CleoMod");
        string scriptFile = Path.Combine(modFolder, "myscript" + extension);
        File.WriteAllText(scriptFile, "script");

        var matches = new RuleEngine().FindMatchingRules(scriptFile, modFolder, BuildGta3Profile());

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, r => r.Name == "CLEO Script");
        Assert.Contains(matches, r => r.Name == "Default (game root)");
    }

    [Fact]
    public void FindMatchingRules_FxtFile_MatchesCleoFxtRuleAndDefault()
    {
        using var tmp = new TempDirectory();
        string modFolder = tmp.CreateSubDir("CleoMod");
        string fxtFile   = Path.Combine(modFolder, "strings.fxt");
        File.WriteAllText(fxtFile, "fxt content");

        var matches = new RuleEngine().FindMatchingRules(fxtFile, modFolder, BuildGta3Profile());

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, r => r.Name == "CLEO FXT");
        Assert.Contains(matches, r => r.Name == "Default (game root)");
    }

    [Fact]
    public void FindMatchingRules_UnknownExtension_MatchesOnlyDefaultCatchAll()
    {
        // .dll has no specific rule → only the empty-condition default fires.
        var matches = new RuleEngine().FindMatchingRules(
            @"C:\mod\helper.dll", @"C:\mod", BuildGta3Profile());

        Assert.Single(matches);
        Assert.True(matches[0].IsDefault);
        Assert.Equal("Default (game root)", matches[0].Name);
    }

    // ── IsNot operator ────────────────────────────────────────────────────────

    [Fact]
    public void FindMatchingRules_ExtensionIsNot_ExcludesMatchingExtension()
    {
        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = @"C:\Game",
            RoutingRules  =
            [
                new RoutingRule
                {
                    Name       = "Non-DLL rule",
                    Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.IsNot, Value = ".dll" } ],
                    TargetPath = "mods",
                    Priority   = 50,
                },
            ],
        };

        // .dll must NOT match the rule (IsNot)
        var dllMatches = new RuleEngine().FindMatchingRules(@"C:\mod\helper.dll", "", profile);
        // .asi is not .dll, so it SHOULD match
        var asiMatches = new RuleEngine().FindMatchingRules(@"C:\mod\plugin.asi", "", profile);

        Assert.Empty(dllMatches);
        Assert.Single(asiMatches);
    }

    // ── PathContains condition ────────────────────────────────────────────────

    [Fact]
    public void FindMatchingRules_PathContains_MatchesFilePathSubstring()
    {
        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = @"C:\Game",
            RoutingRules  =
            [
                new RoutingRule
                {
                    Name       = "CLEO subfolder rule",
                    Conditions = [ new Condition { Type = ConditionType.PathContains, Operator = ConditionOperator.Contains, Value = "cleo" } ],
                    TargetPath = "cleo",
                    Priority   = 70,
                },
            ],
        };

        var cleoMatches  = new RuleEngine().FindMatchingRules(@"C:\mod\cleo\myfile.cs", "", profile);
        var otherMatches = new RuleEngine().FindMatchingRules(@"C:\mod\scripts\myfile.cs", "", profile);

        Assert.Single(cleoMatches);
        Assert.Empty(otherMatches);
    }

    [Fact]
    public void FindMatchingRules_PathContains_StartsWith_MatchesRelativeModloaderPath()
    {
        var profile = BuildGta3Profile();

        using var tmp = new TempDirectory();
        string modFolder = tmp.CreateSubDir("Mod");
        Directory.CreateDirectory(Path.Combine(modFolder, "modloader", "Speed"));
        string filePath = Path.Combine(modFolder, "modloader", "Speed", "speed.asi");
        File.WriteAllText(filePath, "mod");

        var matches = new RuleEngine().FindMatchingRules(filePath, modFolder, profile);

        Assert.Contains(matches, r => r.Name == "ModLoader Tree");
    }

    // ── FilenameMatches condition ─────────────────────────────────────────────

    [Fact]
    public void FindMatchingRules_FilenameMatches_ExactFilenameOnly()
    {
        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = @"C:\Game",
            RoutingRules  =
            [
                new RoutingRule
                {
                    Name       = "Readme rule",
                    Conditions = [ new Condition { Type = ConditionType.FilenameMatches, Operator = ConditionOperator.Is, Value = "readme.txt" } ],
                    TargetPath = ".",
                    Priority   = 90,
                },
            ],
        };

        // Case-insensitive match expected
        var readmeMatches = new RuleEngine().FindMatchingRules(@"C:\mod\readme.txt", "", profile);
        var otherMatches  = new RuleEngine().FindMatchingRules(@"C:\mod\plugin.asi", "", profile);

        Assert.Single(readmeMatches);
        Assert.Empty(otherMatches);
    }

    // ── Empty condition list ─────────────────────────────────────────────────

    [Fact]
    public void FindMatchingRules_EmptyConditionList_AlwaysMatches()
    {
        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = @"C:\Game",
            RoutingRules  =
            [
                new RoutingRule { Name = "Catch-all", Conditions = [], TargetPath = ".", IsDefault = true },
            ],
        };

        var matches = new RuleEngine().FindMatchingRules(@"C:\mod\anything.xyz", "", profile);

        Assert.Single(matches);
        Assert.Equal("Catch-all", matches[0].Name);
    }

    // ── ModType ordering ──────────────────────────────────────────────────────

    [Fact]
    public void FindMatchingRules_ModTypeRules_ReturnedBeforeGameWideRules()
    {
        // Both a ModType-scoped rule and a game-wide rule match the same .asi file.
        // The engine must return ModType rules first in the list.
        var modTypeRule = new RoutingRule
        {
            Name       = "ModType ASI",
            Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi" } ],
            TargetPath = "scripts",
            Priority   = 80,
        };
        var gameWideRule = new RoutingRule
        {
            Name       = "GameWide ASI",
            Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi" } ],
            TargetPath = ".",
            Priority   = 50,
        };

        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = @"C:\Game",
            ModTypes      = [ new ModType { Name = "ASI Plugin", RoutingRules = [ modTypeRule ] } ],
            RoutingRules  = [ gameWideRule ],
        };

        var matches = new RuleEngine().FindMatchingRules(@"C:\mod\plugin.asi", "", profile);

        Assert.Equal(2, matches.Count);
        Assert.Equal("ModType ASI",  matches[0].Name);   // ModType rules first
        Assert.Equal("GameWide ASI", matches[1].Name);
    }

    // ── Conflict resolution ───────────────────────────────────────────────────

    [Fact]
    public void ResolveConflict_ReturnsHighestPriorityRule()
    {
        var low  = new RoutingRule { Name = "Low",  Priority = 10, TargetPath = "." };
        var high = new RoutingRule { Name = "High", Priority = 80, TargetPath = "scripts" };
        var mid  = new RoutingRule { Name = "Mid",  Priority = 50, TargetPath = "plugins" };

        var winner = new RuleEngine().ResolveConflict([ low, high, mid ]);

        Assert.Equal("High", winner.Name);
    }

    [Fact]
    public void ResolveConflict_EqualPriority_ReturnsFirst()
    {
        // When priority ties, the first rule in the supplied list wins
        // (OrderByDescending is stable for equal keys in LINQ — first in, first out).
        var first  = new RoutingRule { Name = "First",  Priority = 50, TargetPath = "a" };
        var second = new RoutingRule { Name = "Second", Priority = 50, TargetPath = "b" };

        var winner = new RuleEngine().ResolveConflict([ first, second ]);

        Assert.Equal("First", winner.Name);
    }
}
