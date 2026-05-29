using System.Collections.Generic;
using System.Text.Json;

namespace TMM.Tests;

/// <summary>
/// Regression tests for <see cref="JsonHelper.TmmGameOptions"/>.
///
/// The six bundled GTA profiles express their routing rules with condition objects
/// whose <c>type</c>/<c>operator</c>/<c>logic</c> fields are enum *names* ("PathContains",
/// "StartsWith", "AND"). System.Text.Json rejects string→enum unless a
/// <c>JsonStringEnumConverter</c> is registered. Before the converter was added these
/// profiles threw on deserialize and were silently dropped, so only the flat-schema
/// games (Skyrim/FNV/Cyberpunk/RDR2/Witcher 3) loaded. These tests pin the converter
/// in place so that regression can't return unnoticed.
/// </summary>
public class TmmGameOptionsTests
{
    private const string ConditionRuleJson = """
        [
          {
            "name": "ModLoader Tree",
            "conditions": [
              { "type": "PathContains", "operator": "StartsWith", "value": "modloader" }
            ],
            "targetPath": "modloader",
            "priority": 95
          },
          {
            "name": "CLEO Script",
            "conditions": [
              { "type": "FileExtension", "operator": "Is", "value": ".cs", "logic": "OR" },
              { "type": "FileExtension", "operator": "Is", "value": ".cs4", "logic": "OR" }
            ],
            "targetPath": "cleo",
            "priority": 60
          }
        ]
        """;

    [Fact]
    public void StringEnumRoutingRules_DeserializeWithoutThrowing()
    {
        var rules = JsonSerializer.Deserialize<List<RoutingRule>>(
            ConditionRuleJson, JsonHelper.TmmGameOptions);

        Assert.NotNull(rules);
        Assert.Equal(2, rules!.Count);

        var modloader = rules[0];
        Assert.Equal("ModLoader Tree", modloader.Name);
        Assert.Single(modloader.Conditions);
        Assert.Equal(ConditionType.PathContains, modloader.Conditions[0].Type);
        Assert.Equal(ConditionOperator.StartsWith, modloader.Conditions[0].Operator);

        var cleo = rules[1];
        Assert.Equal(2, cleo.Conditions.Count);
        Assert.Equal(LogicOperator.OR, cleo.Conditions[0].Logic);
    }

    [Fact]
    public void NumericEnumRoutingRules_StillDeserialize()
    {
        // Older exports wrote enums as integers; the converter must accept both forms
        // so previously-exported .tmmgame files keep loading.
        const string numericJson = """
            [ { "name": "n", "conditions": [ { "type": 0, "operator": 0, "value": ".asi" } ], "targetPath": "." } ]
            """;

        var rules = JsonSerializer.Deserialize<List<RoutingRule>>(numericJson, JsonHelper.TmmGameOptions);

        Assert.NotNull(rules);
        Assert.Equal(ConditionType.FileExtension, rules![0].Conditions[0].Type);
    }
}
