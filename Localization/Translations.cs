using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace DentalClinic.Localization
{
    /// <summary>
    /// Translation tables. The Russian text is the key, so Russian needs no table and any text without a
    /// translation (for example a service a manager typed in) is shown as it is.
    /// </summary>
    public static class Translations
    {
        private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Tables = new(Load);

        public static IReadOnlyDictionary<string, string> For(string language) =>
            Tables.Value.TryGetValue(language, out var table) ? table : new Dictionary<string, string>();

        public static IEnumerable<string> Loaded => Tables.Value.Keys;

        /// <summary>Translates for the language of the current request.</summary>
        public static string Get(string? key) => Get(key, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        public static string Get(string? key, string language)
        {
            if (string.IsNullOrEmpty(key)) return key ?? string.Empty;
            return Tables.Value.TryGetValue(language, out var table) && table.TryGetValue(key, out var text) ? text : key;
        }

        /// <summary>
        /// Russian texts whose translation into the current language contains <paramref name="term"/>. Lets a search
        /// typed in the language of the reader find data that is stored in Russian, such as the demo names.
        /// </summary>
        public static IReadOnlyList<string> SourcesMatching(string term)
        {
            var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (string.IsNullOrWhiteSpace(term) || !Tables.Value.TryGetValue(language, out var table)) return Array.Empty<string>();
            return table.Where(p => p.Value.Contains(term, StringComparison.CurrentCultureIgnoreCase)).Select(p => p.Key).ToList();
        }

        public static bool Has(string key, string language) =>
            Tables.Value.TryGetValue(language, out var table) && table.ContainsKey(key);

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load()
        {
            var assembly = typeof(Translations).Assembly;
            var result = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            foreach (var language in Languages.All.Where(l => l.Code != Languages.Default))
            {
                using var stream = assembly.GetManifestResourceStream($"Localization.{language.Code}.json")
                    ?? throw new InvalidOperationException($"Translation table '{language.Code}' is missing from the assembly.");
                result[language.Code] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                    ?? throw new InvalidOperationException($"Translation table '{language.Code}' is empty.");
            }
            return result;
        }
    }
}
