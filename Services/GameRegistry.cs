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

        private readonly Dictionary<string, GameProfile> _builtInGames = new();
        private readonly Dictionary<string, (CustomGameProfile config, GameProfile profile)> _customGames = new();
        private string _customGamesPath = "";

        private GameRegistry()
        {
            InitializeBuiltInGames();
        }

        /// <summary>Initialize the registry with a custom games directory. Call this once on app startup.</summary>
        public async Task InitializeAsync(string appDataPath)
        {
            _customGamesPath = Path.Combine(appDataPath, "CustomGames");
            Directory.CreateDirectory(_customGamesPath);
            // LoadCustomGamesAsync calls LoadBuiltInProfilesAsync internally
            await LoadCustomGamesAsync();
        }

        private void InitializeBuiltInGames()
        {
            _builtInGames.Clear();
            foreach (var p in GameProfile.All)
                _builtInGames[p.Key] = p;
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

                    // Use explicit gameKey if provided; otherwise generate from name
                    string key = !string.IsNullOrEmpty(export.GameKey)
                        ? export.GameKey
                        : $"BUILTIN_{export.GameName.ToUpperInvariant().Replace(" ", "_").Replace(":", "").Replace("\\", "").Replace("/", "")}";

                    var profile = CustomGameProfileToGameProfile(key, config);
                    _customGames[key] = (config, profile);

                    // If this .tmmgame claims a known built-in key, retire the static C# profile
                    // so the same game doesn't appear twice in GetAllGames().
                    if (_builtInGames.ContainsKey(key))
                        _builtInGames.Remove(key);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load built-in profile {resourceName}: {ex.Message}");
                }
            }
        }

        private async Task LoadCustomGamesAsync()
        {
            // Reset static built-in games first so a reload is idempotent
            InitializeBuiltInGames();
            _customGames.Clear();
            await LoadBuiltInProfilesAsync();
            if (!Directory.Exists(_customGamesPath)) return;

            var jsonFiles = Directory.GetFiles(_customGamesPath, "*.json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);

                    // Try new format first (has RoutingRules); fall back via legacy migration
                    CustomGameProfile? config = null;
                    try
                    {
                        config = JsonSerializer.Deserialize<CustomGameProfile>(json);
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
                    var profile = CustomGameProfileToGameProfile(key, config);
                    _customGames[key] = (config, profile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load custom game {file}: {ex.Message}");
                }
            }
        }

        /// <summary>Get a game profile by key (e.g., "III", "VC", "SA", "CUSTOM_1").</summary>
        public GameProfile? GetGameProfile(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_builtInGames.TryGetValue(key, out var builtIn)) return builtIn;
            if (_customGames.TryGetValue(key, out var custom)) return custom.profile;
            return null;
        }

        /// <summary>Get the custom game config (if it's a custom game, else null).</summary>
        public CustomGameProfile? GetCustomGameConfig(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _customGames.TryGetValue(key, out var custom) ? custom.config : null;
        }

        /// <summary>Get all available games (built-in + custom), sorted by display name.</summary>
        public IReadOnlyList<GameProfile> GetAllGames() =>
            _builtInGames.Values
                .Concat(_customGames.Values.Select(x => x.profile))
                .OrderBy(g => g.DisplayName)
                .ToList();

        public IReadOnlyList<GameProfile> GetBuiltInGames() =>
            _builtInGames.Values.ToList();

        public IReadOnlyList<(string Key, CustomGameProfile Config)> GetCustomGames() =>
            _customGames.Where(kvp => !kvp.Value.config.IsBuiltIn)
                        .Select(kvp => (kvp.Key, kvp.Value.config)).ToList();

        public IReadOnlyList<(string Key, CustomGameProfile Config)> GetBuiltInCustomGames() =>
            _customGames.Where(kvp => kvp.Value.config.IsBuiltIn)
                        .Select(kvp => (kvp.Key, kvp.Value.config)).ToList();

        /// <summary>Add a new custom game. Returns the generated key.</summary>
        public async Task<string> AddCustomGameAsync(CustomGameProfile config)
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
            while (_customGames.ContainsKey(key) || _builtInGames.ContainsKey(key))
                key = $"{baseSlug}_{counter++}";

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(config, JsonHelper.PrettyOptions));

            var profile = CustomGameProfileToGameProfile(key, config);
            _customGames[key] = (config, profile);
            return key;
        }

        /// <summary>Update or create a game config (works for both built-in and user-added games).</summary>
        public async Task UpdateCustomGameAsync(string key, CustomGameProfile config)
        {
            if (string.IsNullOrEmpty(_customGamesPath))
                throw new InvalidOperationException("GameRegistry not initialized.");

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(config, JsonHelper.PrettyOptions));

            var profile = CustomGameProfileToGameProfile(key, config);
            _customGames[key] = (config, profile);
            // If there was a static built-in entry for this key, retire it
            _builtInGames.Remove(key);
        }

        /// <summary>Delete a custom game.</summary>
        public async Task DeleteCustomGameAsync(string key)
        {
            if (!_customGames.ContainsKey(key))
                throw new ArgumentException($"Custom game '{key}' not found");

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            if (File.Exists(filePath)) File.Delete(filePath);
            _customGames.Remove(key);
        }

        public async Task ReloadCustomGamesAsync() => await LoadCustomGamesAsync();

        /// <summary>
        /// Synchronously persists an already-registered custom game's config to disk.
        /// Used by Quick Scan, which runs on a background thread and mutates the in-memory
        /// config in place (e.g. filling in a detected GameDirectory). No-op for built-in
        /// profiles, which are embedded resources with no CustomGames/ file.
        /// </summary>
        public void SaveCustomGameSync(string key, CustomGameProfile config)
        {
            if (string.IsNullOrEmpty(_customGamesPath)) return;
            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(config, JsonHelper.PrettyOptions));
        }

        /// <summary>Export a CustomGameProfile to a .tmmgame file (camelCase JSON, new format).</summary>
        public static async Task ExportConfigAsync(CustomGameProfile config, string destPath)
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
        /// Import a .tmmgame file. Returns a CustomGameProfile ready for AddCustomGameAsync.
        /// Supports both new (routingRules) and legacy (outputDirectories + conditionalRoutes) formats.
        /// Does NOT add it to the registry — caller decides whether to add or just preview.
        /// </summary>
        public static async Task<CustomGameProfile> ImportGameConfigAsync(string sourcePath)
        {
            var json = await File.ReadAllTextAsync(sourcePath);
            var export = JsonSerializer.Deserialize<TmmGameExport>(json, JsonHelper.TmmGameOptions)
                ?? throw new InvalidDataException("Invalid .tmmgame file");

            return ProfileMigration.FromExport(export, Path.GetFileNameWithoutExtension(sourcePath));
        }

        private static GameProfile CustomGameProfileToGameProfile(string key, CustomGameProfile custom)
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
