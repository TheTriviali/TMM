using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace TMM.Tests;

/// <summary>
/// Regression tests for all bundled .tmmgame profiles.
///
/// Previously the five "other" profiles (Skyrim/FNV/Cyberpunk/RDR2/Witcher 3) used
/// a flat extensionPattern/destination schema that mapped to no property on RoutingRule,
/// so they deserialized to empty rule lists and routing was non-functional. These tests
/// pin that every bundled profile:
///   1. Deserializes without throwing.
///   2. Has at least one routing rule.
///   3. Every rule has a non-empty TargetPath.
/// </summary>
public class BuiltInProfilesTests
{
    private static IEnumerable<string> EmbeddedProfileNames()
    {
        var asm = Assembly.GetAssembly(typeof(GameRegistry))!;
        return asm.GetManifestResourceNames()
            .Where(n => n.StartsWith("TMM.Assets.GameProfiles.") && n.EndsWith(".tmmgame"));
    }

    [Fact]
    public void AllBundledProfiles_DeserializeWithoutThrowing()
    {
        var asm = Assembly.GetAssembly(typeof(GameRegistry))!;
        var names = EmbeddedProfileNames().ToList();

        Assert.NotEmpty(names);

        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            // Should not throw
            var export = JsonSerializer.Deserialize<TmmGameExportAccessor>(json, JsonHelper.TmmGameOptions);
            Assert.NotNull(export);
            Assert.False(string.IsNullOrEmpty(export!.GameName), $"{name}: GameName must not be empty");
        }
    }

    [Fact]
    public void AllBundledProfiles_HaveAtLeastOneRoutingRule()
    {
        var asm = Assembly.GetAssembly(typeof(GameRegistry))!;

        foreach (var name in EmbeddedProfileNames())
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var export = JsonSerializer.Deserialize<TmmGameExportAccessor>(json, JsonHelper.TmmGameOptions);
            Assert.NotNull(export?.RoutingRules);
            Assert.True(export!.RoutingRules!.Count > 0,
                $"{name}: expected at least one routing rule but got 0 — profile likely uses the old flat schema");
        }
    }

    [Fact]
    public void AllBundledProfiles_RoutingRulesHaveNonEmptyTargetPath()
    {
        var asm = Assembly.GetAssembly(typeof(GameRegistry))!;

        foreach (var name in EmbeddedProfileNames())
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var export = JsonSerializer.Deserialize<TmmGameExportAccessor>(json, JsonHelper.TmmGameOptions)!;
            foreach (var rule in export.RoutingRules ?? [])
                Assert.False(string.IsNullOrWhiteSpace(rule.TargetPath),
                    $"{name}: rule '{rule.Name}' has empty TargetPath");
        }
    }

    // Minimal accessor for the embedded-resource wire format.
    // TmmGameExport is internal; this mirrors only the fields the tests need.
    private class TmmGameExportAccessor
    {
        public string? GameName { get; set; }
        public List<RoutingRule>? RoutingRules { get; set; }
    }
}
