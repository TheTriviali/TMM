using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// BackendCore — settings persistence and game-path access.
    /// </summary>
    public partial class BackendCore
    {
        // ==========================================================
        // SETTINGS
        // ==========================================================

        public void LoadSettings()
        {
            string path = Path.Combine(AppDataPath, "settings.json");
            if (!File.Exists(path)) return;

            try
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (loaded is not null) Settings = loaded;

                // Ensure all GamePaths keys exist (forward-compat for added games).
                foreach (var profile in GameProfile.All)
                    Settings.GamePaths.TryAdd(profile.Key, null);
            }
            catch (Exception ex) { Log($"LoadSettings failed: {ex.Message}"); }
        }

        public void SaveSettings()
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(AppDataPath, "settings.json"),
                    JsonSerializer.Serialize(Settings, JsonHelper.PrettyOptions));
                NotificationService.ShowVerbose("Settings saved", "Settings");
            }
            catch (Exception ex) { Log($"SaveSettings failed: {ex.Message}"); }
        }

        public void FactoryReset()
        {
            // Log before deleting - after this the log file is gone.
            Log("FactoryReset: wiping AppData directory.");
            try
            {
                if (Directory.Exists(AppDataPath))
                {
                    // Delete everything except the log file so we can audit the reset.
                    foreach (var dir in Directory.GetDirectories(AppDataPath))
                        ForceDeleteDirectory(dir);
                    foreach (var file in Directory.GetFiles(AppDataPath))
                    {
                        if (Path.GetFileName(file).Equals("TMM.log", StringComparison.OrdinalIgnoreCase))
                            continue;
                        try { File.Delete(file); } catch { /* skip locked */ }
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-fatal - settings are gone, remaining files are harmless on next launch.
                try { Log($"FactoryReset partial failure: {ex.Message}"); } catch { }
            }
        }

        public void OpenAppData() => Process.Start("explorer.exe", AppDataPath);

        // ==========================================================
        // GAME PATH ACCESS
        // ==========================================================

        public string? GetVanillaPath(GameProfile profile) =>
            Settings.GamePaths.TryGetValue(profile.Key, out var path) ? path : null;

        public void SetVanillaPath(GameProfile profile, string? path)
        {
            Settings.GamePaths[profile.Key] = path;

            // When the IV base path is set, auto-derive TLaD and TBoGT from it.
            // Standard layout: [IV dir]\TLAD\  and  [IV dir]\EFLC\
            // Also check alternate names: TLAD/TLaD and EFLC/TBoGT
            if (profile.Key == "IV" && !string.IsNullOrEmpty(path))
            {
                var tladPath = Path.Combine(path, "TLAD");
                if (!Directory.Exists(tladPath)) tladPath = Path.Combine(path, "TLaD");
                if (Directory.Exists(tladPath))
                    Settings.GamePaths[GameProfile.TLaD.Key] = tladPath;

                var tbogtPath = Path.Combine(path, "EFLC");
                if (!Directory.Exists(tbogtPath)) tbogtPath = Path.Combine(path, "TBoGT");
                if (Directory.Exists(tbogtPath))
                    Settings.GamePaths[GameProfile.TBoGT.Key] = tbogtPath;
            }

            SaveSettings();
        }

        public bool IsGameReady(GameProfile profile) =>
            !string.IsNullOrEmpty(GetVanillaPath(profile));
    }
}
