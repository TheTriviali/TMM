using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace TMM
{
    internal static class ShellHelper
    {
        public static void OpenFolder(string path) =>
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });

        /// <summary>Open a TMM-owned folder, creating it first so it always exists.</summary>
        public static void OpenOwnedFolder(string path)
        {
            System.IO.Directory.CreateDirectory(path);
            OpenFolder(path);
        }

        public static void OpenUrl(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static class UiColors
    {
        public static readonly Color DisabledGray    = Color.FromRgb(70,  70,  70);
        public static readonly Color ReadyGreen      = Color.FromRgb(80,  200, 100);
        public static readonly Color NotReadyRed     = Color.FromRgb(160, 60,  60);
        public static readonly Color PendingOrange   = Color.FromRgb(200, 110, 20);
        public static readonly Color NeedsFolderAmber = Color.FromRgb(224, 160, 32);
        public static readonly Color NoModsMuted      = Color.FromArgb(128, 255, 255, 255);

        public static SolidColorBrush DisabledBrush    => new(DisabledGray);
        public static SolidColorBrush ReadyBrush       => new(ReadyGreen);
        public static SolidColorBrush NotReadyBrush    => new(NotReadyRed);
        public static SolidColorBrush PendingBrush     => new(PendingOrange);
        public static SolidColorBrush NeedsFolderBrush => new(NeedsFolderAmber);
        public static SolidColorBrush NoModsBrush      => new(NoModsMuted);
    }

    public static class JsonHelper
    {
        public static JsonSerializerOptions PrettyOptions { get; } = new() { WriteIndented = true };

        public static JsonSerializerOptions TmmGameOptions { get; } = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            // Routing-rule conditions store their enums as strings ("PathContains",
            // "StartsWith", "AND", ...). Without this converter every bundled profile
            // that uses condition-based routingRules (all six GTA profiles) throws on
            // deserialize and is silently dropped, so only the flat-schema games load.
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
