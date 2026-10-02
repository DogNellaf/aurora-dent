using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace DentalClinic.Localization
{
    public sealed record Language(string Code, string Name, CultureInfo Culture);

    /// <summary>The languages of the site. Russian is the source language and the default.</summary>
    public static class Languages
    {
        public const string Default = "ru";
        public static readonly string CookieName = CookieRequestCultureProvider.DefaultCookieName;

        public static readonly IReadOnlyList<Language> All = new[]
        {
            new Language("ru", "Русский", new CultureInfo("ru")),
            new Language("en", "English", new CultureInfo("en")),
            new Language("fr", "Français", new CultureInfo("fr")),
            new Language("de", "Deutsch", new CultureInfo("de"))
        };

        public static IReadOnlyList<CultureInfo> Cultures { get; } = All.Select(l => l.Culture).ToList();

        public static Language Find(string? code) =>
            All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? All[0];

        /// <summary>Language of the current request, "ru" when the culture is not one of the supported ones.</summary>
        public static Language Current => Find(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }
}
