using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TMM
{
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

    // Wire-format class for .tmmgame files — camelCase JSON with $schema header.
    // Not used for internal CustomGames/ storage (that stays PascalCase).
    internal class TmmGameExport
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = "tmm-game/1.0";

        public string? GameName { get; set; }
        public string GameDirectory { get; set; } = "";
        public string? ExePath { get; set; }
        public string? SteamAppId { get; set; }
        public string? ModFileTypes { get; set; }
        public Dictionary<string, string>? OutputDirectories { get; set; }
        public List<ConditionalRoute>? ConditionalRoutes { get; set; }
        public InstallerHints? InstallerHints { get; set; }
        public LauncherCardConfig? LauncherCard { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }
    }
}
