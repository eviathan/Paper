using System.Collections.Concurrent;

namespace Paper.Core.I18n
{
    /// <summary>
    /// Internationalization (i18n) support for Paper.
    /// Provides translation lookup, pluralization, and locale formatting.
    /// </summary>
    public static class I18n
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _translations = new();
        private static string _currentLocale = "en";
        private static string _defaultLocale = "en";

        /// <summary>
        /// The current locale (e.g., "en", "es", "fr").
        /// </summary>
        public static string CurrentLocale
        {
            get => _currentLocale;
            set => _currentLocale = value ?? _defaultLocale;
        }

        /// <summary>
        /// The default fallback locale.
        /// </summary>
        public static string DefaultLocale
        {
            get => _defaultLocale;
            set => _defaultLocale = value ?? "en";
        }

        /// <summary>
        /// Loads translations for a locale. Translations is a dictionary of key -> translation.
        /// </summary>
        public static void LoadTranslations(string locale, Dictionary<string, string> translations)
        {
            var dict = _translations.GetOrAdd(locale, _ => new ConcurrentDictionary<string, string>());
            foreach (var kv in translations)
                dict[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Loads translations from a JSON string.
        /// </summary>
        public static void LoadTranslationsFromJson(string locale, string json)
        {
            var translations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (translations != null)
                LoadTranslations(locale, translations);
        }

        /// <summary>
        /// Translates a key using the current locale, falling back to default locale.
        /// Supports simple interpolation: "Hello {name}" with args["name"] = "World".
        /// </summary>
        public static string T(string key, Dictionary<string, string>? args = null)
        {
            if (_translations.TryGetValue(_currentLocale, out var localeDict) && localeDict.TryGetValue(key, out var value))
                return Interpolate(value, args);

            if (_currentLocale != _defaultLocale && _translations.TryGetValue(_defaultLocale, out var defaultDict) && defaultDict.TryGetValue(key, out var defaultValue))
                return Interpolate(defaultValue, args);

            return args != null ? Interpolate(key, args) : key;
        }

        /// <summary>
        /// Translates a key with pluralization.
        /// </summary>
        public static string TP(string key, int count, Dictionary<string, string>? args = null)
        {
            var pluralKey = count == 1 ? $"{key}.one" : $"{key}.other";
            var result = T(pluralKey, args);
            if (result == pluralKey)
                result = T(key, args);
            return result.Replace("{count}", count.ToString());
        }

        /// <summary>
        /// Returns whether a translation exists for the given key.
        /// </summary>
        public static bool Exists(string key)
        {
            if (_translations.TryGetValue(_currentLocale, out var localeDict) && localeDict.ContainsKey(key))
                return true;
            if (_currentLocale != _defaultLocale && _translations.TryGetValue(_defaultLocale, out var defaultDict) && defaultDict.ContainsKey(key))
                return true;
            return false;
        }

        private static string Interpolate(string template, Dictionary<string, string>? args)
        {
            if (args == null || args.Count == 0) return template;
            foreach (var kv in args)
                template = template.Replace($"{{{kv.Key}}}", kv.Value);
            return template;
        }

        /// <summary>
        /// Clears all loaded translations (useful for testing).
        /// </summary>
        public static void Clear()
        {
            _translations.Clear();
        }
    }

    /// <summary>
    /// Context for providing locale to components.
    /// </summary>
    public class I18nContext
    {
        public string Locale { get; set; } = "en";
    }
}
