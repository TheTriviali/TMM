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
        string? AccentColor = null
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

        public string? GameName { get; set; }
        public string GameDirectory { get; set; } = "";
        public string? ExePath { get; set; }
        public string? SteamAppId { get; set; }

        // ── New format ────────────────────────────────────────────────────────────
        public List<RoutingRule>? RoutingRules { get; set; }

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
    }

    /// <summary>
    /// Migrate an old-format CustomGameProfile (OutputDirectories + ConditionalRoutes)
    /// to the new RoutingRules list. Called on first load of legacy data.
    /// </summary>
    internal static class ProfileMigration
    {
        public static CustomGameProfile FromExport(TmmGameExport export, string fallbackName)
        {
            var config = new CustomGameProfile
            {
                GameName      = export.GameName ?? fallbackName,
                GameDirectory = export.GameDirectory,
                ExePath       = export.ExePath,
                SteamAppId    = export.SteamAppId,
                InstallerHints = export.InstallerHints,
                LauncherCard   = export.LauncherCard,
                Description    = export.Description,
                Author         = export.Author,
                Version        = export.Version,
                RoutingRules   = export.RoutingRules ?? new(),
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

            // Conditional routes first — they override static mappings for the same extension
            foreach (var cr in conditionalRoutes ?? Enumerable.Empty<ConditionalRoute>())
            {
                config.RoutingRules.Add(new RoutingRule
                {
                    ExtensionPattern  = cr.Extension,
                    CheckSubdir       = cr.CheckSubdir,
                    Destination       = cr.RouteIfExists,
                    FallbackDestination = cr.RouteIfMissing,
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
                        ExtensionPattern = kvp.Key,
                        Destination      = kvp.Value,
                    });
                }
            }
        }
    }
}
