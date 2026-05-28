using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace TMM.Services
{
    /// <summary>
    /// Manages UI string localization. Loads language JSON files from Assets/Localization/,
    /// caches translations, and notifies UI of language changes via PropertyChanged.
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        private string _currentLanguage = "en-US";
        private Dictionary<string, Dictionary<string, string>> _cache = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private LocalizationService()
        {
        }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
                }
            }
        }

        /// <summary>Get string by key. Returns [key] as fallback if missing (easy to spot untranslated strings).</summary>
        public string this[string key]
        {
            get
            {
                if (_cache.TryGetValue(_currentLanguage, out var strings) && strings.TryGetValue(key, out var value))
                {
                    return value;
                }

                return $"[{key}]"; // Fallback: show untranslated key
            }
        }

        /// <summary>Set active language and load translations.</summary>
        public void SetLanguage(string languageCode)
        {
            // Always ensure language is loaded (fixes bug where initial en-US never loaded)
            if (!_cache.ContainsKey(languageCode))
            {
                LoadLanguage(languageCode);
            }

            // If already set, force a property-changed notification so bindings refresh
            // (needed for first-launch when default _currentLanguage matches but cache was empty)
            if (_currentLanguage == languageCode)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
                return;
            }

            CurrentLanguage = languageCode;
        }

        // Human-readable display names for known language codes
        private static readonly Dictionary<string, string> DisplayNames = new()
        {
            { "en-US", "English (International)" },
            { "es-MX", "Español (Internacional)" },
            { "es-ES", "Español (España)" },
            { "fr-FR", "Français" },
            { "de-DE", "Deutsch" },
            { "pt-BR", "Português (Brasil)" },
            { "ja-JP", "日本語" },
            { "zh-CN", "中文（简体）" },
            { "ru-RU", "Русский" },
            { "it-IT", "Italiano" },
            { "pl-PL", "Polski" },
        };

        /// <summary>Returns human-readable display name for a language code.</summary>
        public string GetDisplayName(string code)
            => DisplayNames.TryGetValue(code, out var name) ? name : code;

        /// <summary>Get list of available language codes from embedded resources.</summary>
        public List<string> GetAvailableLanguages()
        {
            var languages = new List<string>();
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var resourceName in resourceNames)
            {
                // Match pattern: TMM.Assets.Localization.{language}.json
                if (resourceName.StartsWith("TMM.Assets.Localization.") && resourceName.EndsWith(".json"))
                {
                    var languageCode = resourceName
                        .Replace("TMM.Assets.Localization.", "")
                        .Replace(".json", "");
                    languages.Add(languageCode);
                }
            }

            return languages;
        }

        /// <summary>Load language from embedded JSON resource.</summary>
        private void LoadLanguage(string languageCode)
        {
            var resourceName = $"TMM.Assets.Localization.{languageCode}.json";
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream is null)
                {
                    // Fallback to en-US if language not found
                    if (languageCode != "en-US")
                    {
                        LoadLanguage("en-US");
                        return;
                    }

                    _cache[languageCode] = new Dictionary<string, string>();
                    return;
                }

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options) ?? new();
                    _cache[languageCode] = strings;
                }
            }
        }
    }
}
