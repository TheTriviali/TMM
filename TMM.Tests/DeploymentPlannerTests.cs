using TMM.Tests.Helpers;

namespace TMM.Tests;

/// <summary>
/// Integration tests for <see cref="DeploymentPlanner.PlanDeploymentAsync"/>.
///
/// Coverage:
///   • Correct destination path when a single specific rule matches.
///   • Non-blocking conflict warning when two specific rules both allow conflict.
///   • Blocking conflict warning when at least one rule sets AllowConflict=false.
///   • Fallback to the IsDefault rule when no specific rule matches.
///   • Token resolution: {gameRoot} and {scriptname} in TargetPath.
///   • Blocking warning when the mod folder is missing.
///   • <see cref="DeploymentPlan.IsReady"/> reflects warning severity.
///
/// All tests use real temp directories so file enumeration works correctly.
/// </summary>
public class DeploymentPlannerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a GTA-III–style profile (mirrors gta3.tmmgame rules) rooted at
    /// the supplied <paramref name="gameDir"/>.
    /// </summary>
    private static GameConfig BuildGta3Profile(string gameDir) => new()
    {
        GameName      = "Grand Theft Auto III",
        GameDirectory = gameDir,
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
            new RoutingRule
            {
                Name       = "ASI Plugin (scripts/ if exists)",
                Conditions =
                [
                    new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi", Logic = LogicOperator.AND },
                    new Condition { Type = ConditionType.HasFolder,     Operator = ConditionOperator.Is, Value = "scripts" },
                ],
                TargetPath    = "scripts",
                Priority      = 80,
                AllowConflict = true,
            },
            new RoutingRule
            {
                Name       = "ASI Plugin (root fallback)",
                Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi" } ],
                TargetPath    = ".",
                Priority      = 50,
                AllowConflict = true,
            },
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

    private static ModItem MakeMod(string name, string folderPath) => new()
    {
        Name          = name,
        IsEnabled     = true,
        RawFolderPath = folderPath,
    };

    // ── Single rule match ─────────────────────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_CsFile_RoutesToCleoDirectory()
    {
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("CleoMod");
        File.WriteAllText(Path.Combine(modFolder, "myscript.cs"), "cleo");

        var profile = BuildGta3Profile(gameDir);
        var mod     = MakeMod("CleoMod", modFolder);

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(mod, profile);

        Assert.Empty(plan.Warnings);
        Assert.Single(plan.Files);
        Assert.EndsWith(Path.Combine("cleo", "myscript.cs"), plan.Files[0].DestinationPath);
        Assert.True(plan.IsReady);
    }

    [Theory]
    [InlineData(".cs4")]
    [InlineData(".cs5")]
    public async Task PlanDeployment_CleoVariants_RouteToCleoDirectory(string extension)
    {
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("CleoMod");
        File.WriteAllText(Path.Combine(modFolder, "myscript" + extension), "cleo");

        var profile = BuildGta3Profile(gameDir);
        var mod     = MakeMod("CleoMod", modFolder);

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(mod, profile);

        Assert.Empty(plan.Warnings);
        Assert.Single(plan.Files);
        Assert.EndsWith(Path.Combine("cleo", "myscript" + extension), plan.Files[0].DestinationPath);
    }

    [Fact]
    public async Task PlanDeployment_IniCompanion_FollowsMatchingCleoScript()
    {
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("CleoMod");
        File.WriteAllText(Path.Combine(modFolder, "myscript.cs"), "cleo");
        Directory.CreateDirectory(Path.Combine(modFolder, "CLEO_TEXT"));
        File.WriteAllText(Path.Combine(modFolder, "CLEO_TEXT", "myscript.ini"), "ini");

        var profile = BuildGta3Profile(gameDir);
        var mod     = MakeMod("CleoMod", modFolder);

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(mod, profile);

        Assert.Equal(2, plan.Files.Count);
        var scriptEntry = Assert.Single(plan.Files, f => f.SourcePath.EndsWith("myscript.cs", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(Path.Combine("cleo", "myscript.cs"), scriptEntry.DestinationPath);
        var iniEntry = Assert.Single(plan.Files, f => f.SourcePath.EndsWith("myscript.ini", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(Path.Combine("cleo", "myscript.ini"), iniEntry.DestinationPath);
    }

    [Fact]
    public async Task PlanDeployment_ModloaderTree_PreservesRelativePathAndGroup()
    {
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("SpeedPack");
        Directory.CreateDirectory(Path.Combine(modFolder, "modloader", "Speed"));
        File.WriteAllText(Path.Combine(modFolder, "modloader", "Speed", "speed.asi"), "mod");

        var profile = BuildGta3Profile(gameDir);
        var mod = MakeMod("Speed", modFolder);
        mod.GroupName = "Cars";

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(mod, profile);

        Assert.Single(plan.Files);
        Assert.EndsWith(Path.Combine("modloader", "Cars", "Speed", "speed.asi"), plan.Files[0].DestinationPath);
    }

    [Fact]
    public async Task PlanDeployment_AsiFile_WithoutScriptsSubfolder_RoutesToGameRoot()
    {
        // The HasFolder condition is false (no scripts/ in mod folder)
        // → only the fallback rule (priority 50) is a specific match → no conflict.
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("AsiMod");
        File.WriteAllText(Path.Combine(modFolder, "plugin.asi"), "fake asi");

        var profile = BuildGta3Profile(gameDir);
        var plan    = await new DeploymentPlanner().PlanDeploymentAsync(MakeMod("AsiMod", modFolder), profile);

        // No specific-rule conflict → no warnings
        Assert.Empty(plan.Warnings);
        Assert.Single(plan.Files);

        // Fallback rule sends .asi to game root (TargetPath = ".")
        string dest = plan.Files[0].DestinationPath;
        Assert.Equal(Path.GetFullPath(Path.Combine(gameDir, "plugin.asi")), dest);
    }

    // ── Non-blocking conflict ─────────────────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_AsiFile_WithScriptsSubfolder_EmitsNonBlockingWarning()
    {
        // Both "scripts/ if exists" (priority 80) and "root fallback" (priority 50)
        // are specific rules that match. AllowConflict=true on both → non-blocking
        // warning; higher-priority rule wins.
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("AsiMod");
        Directory.CreateDirectory(Path.Combine(modFolder, "scripts"));
        File.WriteAllText(Path.Combine(modFolder, "plugin.asi"), "fake asi");

        var plan = await new DeploymentPlanner()
            .PlanDeploymentAsync(MakeMod("AsiMod", modFolder), BuildGta3Profile(gameDir));

        Assert.Single(plan.Warnings);
        Assert.False(plan.Warnings[0].IsBlocking);
        Assert.True(plan.IsReady);

        // Higher-priority (80) rule wins → destination is scripts/
        Assert.Single(plan.Files);
        Assert.Contains("scripts", plan.Files[0].DestinationPath);
    }

    // ── Blocking conflict ─────────────────────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_TwoSpecificRules_OneWithAllowConflictFalse_EmitsBlockingWarning()
    {
        // When at least one conflicting specific rule has AllowConflict=false,
        // the planner cannot choose automatically → IsBlocking=true, no entry
        // is added to plan.Files for that file.
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("ConflictMod");
        File.WriteAllText(Path.Combine(modFolder, "plugin.asi"), "fake asi");

        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = gameDir,
            RoutingRules  =
            [
                new RoutingRule
                {
                    Name       = "Rule A",
                    Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi" } ],
                    TargetPath    = "scripts",
                    Priority      = 80,
                    AllowConflict = false,   // ← blocks auto-resolution
                },
                new RoutingRule
                {
                    Name       = "Rule B",
                    Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi" } ],
                    TargetPath    = ".",
                    Priority      = 50,
                    AllowConflict = true,
                },
            ],
        };

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(MakeMod("ConflictMod", modFolder), profile);

        Assert.Single(plan.Warnings);
        Assert.True(plan.Warnings[0].IsBlocking);
        Assert.Empty(plan.Files);      // No destination assigned
        Assert.False(plan.IsReady);
    }

    // ── Missing mod folder ────────────────────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_MissingModFolder_EmitsBlockingWarning()
    {
        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = @"C:\FakeGame",
            RoutingRules  = [],
        };
        var mod = MakeMod("Ghost", @"C:\DoesNotExist\GhostMod");

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(mod, profile);

        Assert.Single(plan.Warnings);
        Assert.True(plan.Warnings[0].IsBlocking);
        Assert.False(plan.IsReady);
    }

    // ── IsDefault fallback ────────────────────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_NoSpecificRuleMatch_FallsBackToDefaultRule()
    {
        // A .xyz file has no extension-specific rule; the IsDefault rule routes
        // it to the game root.
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("RandomMod");
        File.WriteAllText(Path.Combine(modFolder, "readme.xyz"), "readme");

        var plan = await new DeploymentPlanner()
            .PlanDeploymentAsync(MakeMod("RandomMod", modFolder), BuildGta3Profile(gameDir));

        Assert.Empty(plan.Warnings);
        Assert.Single(plan.Files);
        // Default rule → game root → {gameDir}\readme.xyz
        Assert.Equal(
            Path.GetFullPath(Path.Combine(gameDir, "readme.xyz")),
            plan.Files[0].DestinationPath);
        Assert.True(plan.Files[0].AppliedRule!.IsDefault);
    }

    // ── Token resolution: {gameRoot} ─────────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_GameRootToken_ResolvedToGameDirectory()
    {
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("PluginMod");
        File.WriteAllText(Path.Combine(modFolder, "plugin.asi"), "fake asi");

        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = gameDir,
            RoutingRules  =
            [
                new RoutingRule
                {
                    Name       = "ASI to plugins via gameRoot token",
                    Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".asi" } ],
                    TargetPath = "{gameRoot}/plugins",
                    Priority   = 80,
                },
            ],
        };

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(MakeMod("PluginMod", modFolder), profile);

        Assert.Single(plan.Files);
        string expected = Path.GetFullPath(Path.Combine(gameDir, "plugins", "plugin.asi"));
        Assert.Equal(expected, plan.Files[0].DestinationPath);
    }

    // ── Token resolution: {scriptname} ───────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_ScriptnameToken_ResolvedToFilenameWithoutExtension()
    {
        // {scriptname} is useful for mods where each script gets its own subfolder,
        // e.g. CLEO mods with matching .ini files: modloader/cleo/{scriptname}/myscript.cs
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("CleoMod");
        File.WriteAllText(Path.Combine(modFolder, "myscript.cs"), "cleo");

        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = gameDir,
            RoutingRules  =
            [
                new RoutingRule
                {
                    Name       = "CLEO with scriptname subfolder",
                    Conditions = [ new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = ".cs" } ],
                    TargetPath = "modloader/cleo/{scriptname}",
                    Priority   = 80,
                },
            ],
        };

        var plan = await new DeploymentPlanner().PlanDeploymentAsync(MakeMod("CleoMod", modFolder), profile);

        Assert.Single(plan.Files);
        string expected = Path.GetFullPath(Path.Combine(gameDir, "modloader", "cleo", "myscript", "myscript.cs"));
        Assert.Equal(expected, plan.Files[0].DestinationPath);
    }

    // ── IsReady reflects warning severity ────────────────────────────────────

    [Fact]
    public async Task PlanDeployment_IsReady_TrueWhenOnlyNonBlockingWarnings()
    {
        // Non-blocking warning (conflict with AllowConflict=true everywhere) → still ready.
        using var tmp = new TempDirectory();
        string gameDir   = tmp.CreateSubDir("GameDir");
        string modFolder = tmp.CreateSubDir("AsiMod");
        Directory.CreateDirectory(Path.Combine(modFolder, "scripts"));
        File.WriteAllText(Path.Combine(modFolder, "plugin.asi"), "fake");

        var plan = await new DeploymentPlanner()
            .PlanDeploymentAsync(MakeMod("AsiMod", modFolder), BuildGta3Profile(gameDir));

        Assert.NotEmpty(plan.Warnings);
        Assert.True(plan.Warnings.TrueForAll(w => !w.IsBlocking));
        Assert.True(plan.IsReady);
    }

    [Fact]
    public async Task PlanDeployment_IsReady_FalseWhenAnyBlockingWarning()
    {
        var profile = new GameConfig
        {
            GameName      = "TestGame",
            GameDirectory = @"C:\FakeGame",
            RoutingRules  = [],
        };
        var plan = await new DeploymentPlanner()
            .PlanDeploymentAsync(MakeMod("Missing", @"C:\DoesNotExist\Mod"), profile);

        Assert.False(plan.IsReady);
    }
}
