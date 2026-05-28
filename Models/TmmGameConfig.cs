using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>
    /// Legacy conditional route — used only for backward-compat import migration.
    /// New configs use RoutingRule instead.
    /// </summary>
    public record ConditionalRoute(
        string Extension,
        string CheckSubdir,
        string RouteIfExists,
        string RouteIfMissing);

    public record InstallerHints(
        List<string>? EngineProxyNames = null,
        string? DxVersionTarget = null,   // "dx9" | "dx11" | "dx12"
        bool SmartDllWizard = false
    );

    public record LauncherCardConfig(
        string? DisplayName = null,
        string? Subtitle = null,
        string? IconGlyph = null,
        string? AccentColor = null,
        string? GradientStartHex = null,
        string? GradientEndHex = null,
        string? LibraryStatus = null   // "Release"|"Beta"|"Alpha"|"Testing"
    );

    // Wire-format class for .tmmgame files — camelCase JSON, schema v1.0/v1.1.
    // Not used for internal CustomGames/ storage (that stays PascalCase).
    //
    // Supports both new format (routingRules) and old format
    // (modFileTypes + outputDirectories + conditionalRoutes) for import backward-compat.
    // New exports always write routingRules and omit the old fields.
    internal class TmmGameExport
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = "tmm-game/1.1";

        /// <summary>Override the auto-generated registry key (e.g. "III", "IV"). Must be unique.</summary>
        public string? GameKey { get; set; }

        public string? GameName { get; set; }
        /// <summary>Abbreviated name shown on game cards (≤10 chars). Derived from GameName if omitted.</summary>
        public string? ShortName { get; set; }
        public string GameDirectory { get; set; } = "";
        public string? ExePath { get; set; }
        public string? SteamAppId { get; set; }

        // ── New format ────────────────────────────────────────────────────────────
        public List<RoutingRule>? RoutingRules { get; set; }
        public List<string>? OverlayFolders { get; set; }
        public Dictionary<string, List<string>>? CompanionSiblings { get; set; }

        // ── Legacy format (read-only for backward compat, never written on export) ─
        public string? ModFileTypes { get; set; }
        public Dictionary<string, string>? OutputDirectories { get; set; }
        public List<ConditionalRoute>? ConditionalRoutes { get; set; }

        // ── Shared fields ─────────────────────────────────────────────────────────
        public InstallerHints? InstallerHints { get; set; }
        public LauncherCardConfig? LauncherCard { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }
        public string? GradientStartHex { get; set; }
        public string? GradientEndHex { get; set; }
        public string? LibraryStatus { get; set; }
    }

    /// <summary>
    /// Migrate an old-format CustomGameProfile (OutputDirectories + ConditionalRoutes)
    /// to the new RoutingRules list. Called on first load of legacy data.
    /// </summary>
    internal static class ProfileMigration
    {
        public static CustomGameProfile FromExport(TmmGameExport export, string fallbackName)
        {
            // Parse LibraryStatus string → enum
            ReleaseStatus status = ReleaseStatus.Release;
            if (!string.IsNullOrEmpty(export.LibraryStatus))
                System.Enum.TryParse(export.LibraryStatus, ignoreCase: true, out status);
            // Also check LauncherCard.LibraryStatus for per-card override
            if (status == ReleaseStatus.Release &&
                !string.IsNullOrEmpty(export.LauncherCard?.LibraryStatus))
                System.Enum.TryParse(export.LauncherCard.LibraryStatus, ignoreCase: true, out status);

            var config = new CustomGameProfile
            {
                GameName      = export.GameName ?? fallbackName,
                ShortName     = export.ShortName,
                GameDirectory = export.GameDirectory,
                ExePath       = export.ExePath,
                SteamAppId    = export.SteamAppId,
                InstallerHints = export.InstallerHints,
                LauncherCard   = export.LauncherCard,
                Description    = export.Description,
                Author         = export.Author,
                Version        = System.Version.TryParse(export.Version, out var ver) ? ver : null,
                RoutingRules   = export.RoutingRules ?? new(),
                OverlayFolders = export.OverlayFolders ?? new(),
                CompanionSiblings = export.CompanionSiblings ?? new(),
                GradientStartHex = export.GradientStartHex ?? export.LauncherCard?.GradientStartHex,
                GradientEndHex   = export.GradientEndHex   ?? export.LauncherCard?.GradientEndHex,
                LibraryStatus    = status,
            };

            if (config.RoutingRules.Count == 0)
                MigrateOldFields(config, export.ConditionalRoutes, export.OutputDirectories);

            return config;
        }

        public static void MigrateOldFields(
            CustomGameProfile config,
            List<ConditionalRoute>? conditionalRoutes,
            Dictionary<string, string>? outputDirectories)
        {
            if (config.RoutingRules.Count > 0) return;
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Conditional routes: create two rules — one with HasFolder condition, one without
            foreach (var cr in conditionalRoutes ?? Enumerable.Empty<ConditionalRoute>())
            {
                // High-priority rule: if folder exists, route to RouteIfExists
                config.RoutingRules.Add(new RoutingRule
                {
                    Name = $"{cr.Extension} (if {cr.CheckSubdir} exists)",
                    Conditions = new()
                    {
                        new Condition
                        {
                            Type = ConditionType.FileExtension,
                            Operator = ConditionOperator.Is,
                            Value = cr.Extension,
                            Logic = LogicOperator.AND
                        },
                        new Condition
                        {
                            Type = ConditionType.HasFolder,
                            Operator = ConditionOperator.Is,
                            Value = cr.CheckSubdir,
                            Logic = LogicOperator.AND
                        }
                    },
                    TargetPath = cr.RouteIfExists,
                    Priority = 100
                });

                // Low-priority rule: fallback when folder doesn't exist
                config.RoutingRules.Add(new RoutingRule
                {
                    Name = $"{cr.Extension} (fallback)",
                    Conditions = new()
                    {
                        new Condition
                        {
                            Type = ConditionType.FileExtension,
                            Operator = ConditionOperator.Is,
                            Value = cr.Extension,
                            Logic = LogicOperator.AND
                        }
                    },
                    TargetPath = cr.RouteIfMissing,
                    Priority = 50
                });

                covered.Add(cr.Extension.ToLowerInvariant());
            }

            // Static output directories — skip extensions already covered by conditional routes
            foreach (var kvp in outputDirectories ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                if (!covered.Contains(kvp.Key.ToLowerInvariant()))
                {
                    config.RoutingRules.Add(new RoutingRule
                    {
                        Name = $"{kvp.Key} → {kvp.Value}",
                        Conditions = new()
                        {
                            new Condition
                            {
                                Type = ConditionType.FileExtension,
                                Operator = ConditionOperator.Is,
                                Value = kvp.Key,
                                Logic = LogicOperator.AND
                            }
                        },
                        TargetPath = kvp.Value,
                        Priority = 75
                    });
                }
            }
        }
    }
}
