using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                if (_instance == null)
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
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
        private static readonly JsonSerializerOptions TmmGameOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private GameRegistry()
        {
            InitializeBuiltInGames();
        }

        /// <summary>Initialize the registry with a custom games directory. Call this once on app startup.</summary>
        public async Task InitializeAsync(string appDataPath)
        {
            _customGamesPath = Path.Combine(appDataPath, "CustomGames");
            Directory.CreateDirectory(_customGamesPath);
            await LoadCustomGamesAsync();
        }

        private void InitializeBuiltInGames()
        {
            _builtInGames.Clear();
            foreach (var profile in GameProfile.All)
                _builtInGames[profile.Key] = profile;
        }

        private async Task LoadCustomGamesAsync()
        {
            _customGames.Clear();
            if (!Directory.Exists(_customGamesPath)) return;

            var jsonFiles = Directory.GetFiles(_customGamesPath, "*.json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var config = JsonSerializer.Deserialize<CustomGameProfile>(json);
                    if (config != null && !string.IsNullOrEmpty(config.GameName))
                    {
                        string key = Path.GetFileNameWithoutExtension(file);
                        var profile = CustomGameProfileToGameProfile(key, config);
                        _customGames[key] = (config, profile);
                    }
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

            if (_builtInGames.TryGetValue(key, out var builtIn))
                return builtIn;

            if (_customGames.TryGetValue(key, out var custom))
                return custom.profile;

            return null;
        }

        /// <summary>Get the custom game config (if it's a custom game, else null).</summary>
        public CustomGameProfile? GetCustomGameConfig(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _customGames.TryGetValue(key, out var custom) ? custom.config : null;
        }

        /// <summary>Get all available games (built-in + custom), sorted by display name.</summary>
        public IReadOnlyList<GameProfile> GetAllGames()
        {
            var all = new List<GameProfile>();
            all.AddRange(_builtInGames.Values);
            all.AddRange(_customGames.Values.Select(x => x.profile));
            return all.OrderBy(g => g.DisplayName).ToList();
        }

        /// <summary>Get only built-in games.</summary>
        public IReadOnlyList<GameProfile> GetBuiltInGames() =>
            _builtInGames.Values.ToList();

        /// <summary>Get only custom games.</summary>
        public IReadOnlyList<(string Key, CustomGameProfile Config)> GetCustomGames() =>
            _customGames.Select(kvp => (kvp.Key, kvp.Value.config)).ToList();

        /// <summary>Add a new custom game. Returns the generated key.</summary>
        public async Task<string> AddCustomGameAsync(CustomGameProfile config)
        {
            if (string.IsNullOrEmpty(config.GameName))
                throw new ArgumentException("Game name cannot be empty");

            if (string.IsNullOrEmpty(config.GameDirectory))
                throw new ArgumentException("Game directory cannot be empty");

            // Generate a unique key: CUSTOM_1, CUSTOM_2, etc.
            int counter = 1;
            string key;
            while (true)
            {
                key = $"CUSTOM_{counter}";
                if (!_customGames.ContainsKey(key) && !_builtInGames.ContainsKey(key))
                    break;
                counter++;
            }

            // Save to JSON file
            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            var json = JsonSerializer.Serialize(config, JsonOpts);
            await File.WriteAllTextAsync(filePath, json);

            // Add to registry
            var profile = CustomGameProfileToGameProfile(key, config);
            _customGames[key] = (config, profile);

            return key;
        }

        /// <summary>Update an existing custom game.</summary>
        public async Task UpdateCustomGameAsync(string key, CustomGameProfile config)
        {
            if (!_customGames.ContainsKey(key))
                throw new ArgumentException($"Custom game '{key}' not found");

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            var json = JsonSerializer.Serialize(config, JsonOpts);
            await File.WriteAllTextAsync(filePath, json);

            var profile = CustomGameProfileToGameProfile(key, config);
            _customGames[key] = (config, profile);
        }

        /// <summary>Delete a custom game.</summary>
        public async Task DeleteCustomGameAsync(string key)
        {
            if (!_customGames.ContainsKey(key))
                throw new ArgumentException($"Custom game '{key}' not found");

            var filePath = Path.Combine(_customGamesPath, $"{key}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);

            _customGames.Remove(key);
        }

        /// <summary>Reload all custom games from disk (useful if files were modified externally).</summary>
        public async Task ReloadCustomGamesAsync()
        {
            await LoadCustomGamesAsync();
        }

        /// <summary>Export a CustomGameProfile to a .tmmgame file (camelCase JSON).</summary>
        public static async Task ExportConfigAsync(CustomGameProfile config, string destPath)
        {
            var export = new TmmGameExport
            {
                GameName          = config.GameName,
                GameDirectory     = config.GameDirectory.Replace('\\', '/'),
                ExePath           = config.ExePath?.Replace('\\', '/'),
                SteamAppId        = config.SteamAppId,
                ModFileTypes      = config.ModFileTypes,
                OutputDirectories = config.OutputDirectories.Count > 0 ? config.OutputDirectories : null,
                ConditionalRoutes = config.ConditionalRoutes.Count > 0 ? config.ConditionalRoutes : null,
                InstallerHints    = config.InstallerHints,
                LauncherCard      = config.LauncherCard,
                Description       = config.Description,
                Author            = config.Author,
                Version           = config.Version,
            };
            var json = JsonSerializer.Serialize(export, TmmGameOpts);
            await File.WriteAllTextAsync(destPath, json);
        }

        /// <summary>
        /// Import a .tmmgame file. Returns a CustomGameProfile ready for AddCustomGameAsync.
        /// Does NOT add it to the registry — caller decides whether to add or just preview.
        /// </summary>
        public static async Task<CustomGameProfile> ImportGameConfigAsync(string sourcePath)
        {
            var json = await File.ReadAllTextAsync(sourcePath);
            var export = JsonSerializer.Deserialize<TmmGameExport>(json, TmmGameOpts)
                ?? throw new InvalidDataException("Invalid .tmmgame file");

            return new CustomGameProfile
            {
                GameName          = export.GameName ?? Path.GetFileNameWithoutExtension(sourcePath),
                GameDirectory     = export.GameDirectory,
                ExePath           = export.ExePath,
                SteamAppId        = export.SteamAppId,
                ModFileTypes      = export.ModFileTypes ?? ".rar, .zip, .7z",
                OutputDirectories = export.OutputDirectories ?? new(),
                ConditionalRoutes = export.ConditionalRoutes ?? new(),
                InstallerHints    = export.InstallerHints,
                LauncherCard      = export.LauncherCard,
                Description       = export.Description,
                Author            = export.Author,
                Version           = export.Version,
            };
        }

        /// <summary>Convert a CustomGameProfile to a GameProfile.</summary>
        private static GameProfile CustomGameProfileToGameProfile(string key, CustomGameProfile custom)
        {
            return new GameProfile(
                Key: key,
                DisplayName: custom.GameName,
                ShortName: custom.GameName.Length > 10 ? custom.GameName[..10] : custom.GameName,
                ExeName: "", // Custom games may not have a standard .exe name
                SteamAppId: "", // Custom games are not on Steam
                Vanilla10Md5: "");
        }
    }
}
