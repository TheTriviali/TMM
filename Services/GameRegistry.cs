using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TMM
{
    /// <summary>
    /// Central registry for all available games: built-in (III, VC, SA) and custom user-defined games.
    /// Singleton pattern. Loads custom games from AppData on initialization.
    /// </summary>
    public class GameRegistry
    {
        private static GameRegistry? _instance;
        private static readonly object _lock = new();

        public static GameRegistry Instance
        {
            get
            {
                if (_instance is null)
                {
                    lock (_lock)
                    {
                        _instance ??= new();
                    }
                }
                return _instance;
            }
        }

        // Single unified dict: all games (built-in .tmmgame profiles + user-added custom games).
        // Priority (highest wins on key collision): user CustomGames/*.json > .tmmgame assets > GameProfile.All statics.
        private readonly Dictionary<string, (GameConfig config, GameProfile profile)> _games = new();
        private string _customGamesPath = "";

        private GameRegistry() { }

        /// <summary>Initialize the registry with a custom games directory. Call this once on app startup.</summary>
        public async Task InitializeAsync(string appDataPath)
        {
            _customGamesPath = Path.Combine(appDataPath, "CustomGames");
            Directory.CreateDirectory(_customGamesPath);
            await LoadAllGamesAsync();
        }

        private async Task LoadAllGamesAsync()
        {
            _games.Clear();

            // 1. Lowest priority: static C# GameProfile.All entries as thin GameConfig wrappers.
            //    These act as fallbacks if no .tmmgame file covers a built-in key.
            foreach (var p in GameProfile.All)
            {
                var fallback = new GameConfig
                {
                    GameName = p.DisplayName,
                    IsBuiltIn = true,
                    IsNative  = true,
                };
                _games[p.Key] = (fallback, p);
            }

            // 2. Mid priority: embedded .tmmgame assets (richer config — overrides static fallbacks).
            await LoadBuiltInProfilesAsync();

            // 3. Highest priority: user's CustomGames/*.json overrides (edits to any game, or user-added games).
        }

        private async Task LoadBuiltInProfilesAsync()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("TMM.Assets.GameProfiles.") && n.EndsWith(".tmmgame"))
                .ToList();

            foreach (var resourceName in resourceNames)
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream is null) continue;

                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    var export = JsonSerializer.Deserialize<TmmGameExport>(json, JsonHelper.TmmGameOptions);
                    if (export?.GameName is null) continue;

                    var config = ProfileMigration.FromExport(export, Path.GetFileNameWithoutExtension(resourceName));
                    config.IsBuiltIn = true;
                    config.SourceFileName = Path.GetFileName(resourceName.Replace("TMM.Assets.GameProfiles.", ""));

                    string key = !string.IsNullOrEmpty(export.GameKey)
                        ? export.GameKey
                        : $"BUILTIN_{export.GameName.ToUpperInvariant().Replace(" ", "_").Replace(":", "").Replace("\\", "").Replace("/", "")}";

                    var profile = GameConfigToGameProfile(key, config);
                    _games[key] = (config, profile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load built-in profile {resourceName}: {ex.Message}");
                }
            }
        }

        private async Task LoadCustomGamesAsync()
        {
            await LoadAllGamesAsync();
            if (!Directory.Exists(_customGamesPath)) return;

            var jsonFiles = Directory.GetFiles(_customGamesPath, "*.json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);

                    // Try new format first (has RoutingRules); fall back via legacy migration
                    GameConfig? config = null;
                    try
                    {
                        config = JsonSerializer.Deserialize<GameConfig>(json);
                    }
                    catch { /* fall through to legacy */ }

                    if (config is null || string.IsNullOrEmpty(config.GameName)) continue;

                    // If RoutingRules is empty, check for legacy fields in the raw JSON
                    if (config.RoutingRules.Count == 0)
                    {
                        // Deserialize as legacy export format to get old OutputDirectories / ConditionalRoutes
                        var legacy = JsonSerializer.Deserialize<TmmGameExport>(json, JsonHelper.TmmGameOptions);
                        if (legacy is not null)
                        {
                            ProfileMigration.MigrateOldFields(
                                config,
                                legacy.ConditionalRoutes,
                                legacy.OutputDirectories);

                            // Persist the migrated format so we don't re-migrate next launch
                            if (config.RoutingRules.Count > 0)
                            {
                                var migrated = JsonSerializer.Serialize(config, JsonHelper.PrettyOptions);
                                await File.WriteAllTextAsync(file, migrated);
                            }
                        }
                    }

                    string key = Path.GetFileNameWithoutExtension(file);
                    config.SourceFileName = Path.GetFileName(file);
                    var profile = GameConfigToGameProfile(key, config);
                    _games[key] = (config, profile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load custom game {file}: {ex.Message}");
                }
            }
        }

        /// <summary>Get a game profile by key (e.g., "III", "VC", "SA").</summary>
        public GameProfile? GetGameProfile(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_games.TryGetValue(key, out var g)) return g.profile;
            return GameProfile.ByKey(key); // static safety net
        }

        /// <summary>Get the config for any game (built-in or user-added).</summary>
        public GameConfig? GetCustomGameConfig(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _games.TryGetValue(key, out var g) ? g.config : null;
        }

        /// <summary>Get all available games sorted by display name.</summary>
        public IReadOnlyList<GameProfile> GetAllGames() =>
            _games.Values.Select(x => x.profile).OrderBy(g => g.DisplayName).ToList();

        /// <summary>Built-in games only (IsBuiltIn flag).</summary>
        public IReadOnlyList<GameProfile> GetBuiltInGames() =>
            _games.Values.Where(x => x.config.IsBuiltIn).Select(x => x.profile).ToList();

        /// <summary>User-added custom games (not IsBuiltIn).</summary>
        public IReadOnlyList<(string Key, GameConfig Config)> GetCustomGames() =>
            _games.Where(kvp => !kvp.Value.config.IsBuiltIn)
                  .Select(kvp => (kvp.Key, kvp.Value.config)).ToList();

        /// <summary>Built-in games that have a GameConfig (loaded from .tmmgame assets).</summary>
        public IReadOnlyList<(string Key, GameConfig Config)> GetBuiltInCustomGames() =>
            _games.Where(kvp => kvp.Value.config.IsBuiltIn)
                  .Select(kvp => (kvp.Key, kvp.Value.config)).ToList();

        /// <summary>Add a new custom game. Returns the generated key.</summary>
        public async Task<string> AddCustomGameAsync(GameConfig config)
        {
            if (string.IsNullOrEmpty(config.GameName))  throw new ArgumentException("Game name cannot be empty");
            if (string.IsNullOrEmpty(config.GameDirectory)) throw new ArgumentException("Game directory cannot be empty");

            // Build a clean name-based slug (no CUSTOM_ prefix)
            string baseSlug = Regex.Replace(config.GameName, @"[^a-zA-Z0-9]", "_");
            baseSlug = Regex.Replace(baseSlug, "_+", "_").Trim('_');
            if (baseSlug.Length > 24) baseSlug = baseSlug[..24].TrimEnd('_');
            if (string.IsNullOrEmpty(baseSlug)) baseSlug = "Game";

            string key = baseSlug;
            int counter = 2;
            while (_games.ContainsKey(key))
                key = $"{baseSlug}_{counter++}";

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(config, JsonHelper.PrettyOptions));

            var profile = GameConfigToGameProfile(key, config);
            _games[key] = (config, profile);
            return key;
        }

        /// <summary>Update or create a game config (works for both built-in and user-added games).</summary>
        public async Task UpdateCustomGameAsync(string key, GameConfig config)
        {
            if (string.IsNullOrEmpty(_customGamesPath))
                throw new InvalidOperationException("GameRegistry not initialized.");

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(config, JsonHelper.PrettyOptions));

            var profile = GameConfigToGameProfile(key, config);
            _games[key] = (config, profile);
        }

        /// <summary>Delete a custom game.</summary>
        public async Task DeleteCustomGameAsync(string key)
        {
            if (!_games.ContainsKey(key))
                throw new ArgumentException($"Custom game '{key}' not found");

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            if (File.Exists(filePath)) File.Delete(filePath);
            _games.Remove(key);
        }

        public async Task ReloadCustomGamesAsync() => await LoadCustomGamesAsync();

        /// <summary>
        /// Synchronously persists an already-registered custom game's config to disk.
        /// Used by Quick Scan, which runs on a background thread and mutates the in-memory
        /// config in place (e.g. filling in a detected GameDirectory). No-op for built-in
        /// profiles, which are embedded resources with no CustomGames/ file.
        /// </summary>
        public void SaveCustomGameSync(string key, GameConfig config)
        {
            if (string.IsNullOrEmpty(_customGamesPath)) return;
            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(config, JsonHelper.PrettyOptions));
        }

        /// <summary>Export a GameConfig to a .tmmgame file (camelCase JSON, new format).</summary>
        public static async Task ExportConfigAsync(GameConfig config, string destPath)
        {
            var export = new TmmGameExport
            {
                GameName       = config.GameName,
                GameDirectory  = config.GameDirectory.Replace('\\', '/'),
                ExePath        = config.ExePath?.Replace('\\', '/'),
                SteamAppId     = config.SteamAppId,
                RoutingRules   = config.RoutingRules.Count > 0 ? config.RoutingRules : null,
                OverlayFolders = config.OverlayFolders.Count > 0 ? config.OverlayFolders : null,
                CompanionSiblings = config.CompanionSiblings.Count > 0 ? config.CompanionSiblings : null,
                SearchHints    = config.SearchHints.Count > 0 ? config.SearchHints : null,
                NexusSlug      = config.NexusSlug,
                ExpectedExeBytes = config.ExpectedExeBytes,
                AcceptedExeMd5s  = config.AcceptedExeMd5s.Count > 0 ? config.AcceptedExeMd5s : null,
                InstallerHints = config.InstallerHints,
                LauncherCard   = config.LauncherCard,
                Description    = config.Description,
                Author         = config.Author,
                Version        = config.Version?.ToString(),
                // Legacy fields intentionally omitted — new format only
            };
            await File.WriteAllTextAsync(destPath, JsonSerializer.Serialize(export, JsonHelper.TmmGameOptions));
        }

        /// <summary>
        /// Import a .tmmgame file. Returns a GameConfig ready for AddCustomGameAsync.
        /// Supports both new (routingRules) and legacy (outputDirectories + conditionalRoutes) formats.
        /// Does NOT add it to the registry — caller decides whether to add or just preview.
        /// </summary>
        public static async Task<GameConfig> ImportGameConfigAsync(string sourcePath)
        {
            var json = await File.ReadAllTextAsync(sourcePath);
            var export = JsonSerializer.Deserialize<TmmGameExport>(json, JsonHelper.TmmGameOptions)
                ?? throw new InvalidDataException("Invalid .tmmgame file");

            return ProfileMigration.FromExport(export, Path.GetFileNameWithoutExtension(sourcePath));
        }

        private static GameProfile GameConfigToGameProfile(string key, GameConfig custom)
        {
            string shortName = custom.ShortName
                ?? (custom.GameName.Length > 10 ? custom.GameName[..10] : custom.GameName);

            string exeName = string.IsNullOrEmpty(custom.ExePath)
                ? ""
                : System.IO.Path.GetFileName(custom.ExePath);

            return new GameProfile(
                Key: key,
                DisplayName: custom.GameName,
                ShortName: shortName,
                ExeName: exeName,
                SteamAppId: custom.SteamAppId ?? "")
            {
                GradientStartHex = custom.GradientStartHex ?? "#1A1A2E",
                GradientEndHex   = custom.GradientEndHex   ?? "#0D0D1A",
                LibraryStatus    = custom.LibraryStatus,
            };
        }
    }
}
