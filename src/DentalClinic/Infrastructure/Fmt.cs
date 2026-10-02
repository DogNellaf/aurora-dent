using System.Globalization;
using DentalClinic.Localization;
using DentalClinic.Models;

namespace DentalClinic.Infrastructure
{
    /// <summary>Formatting helpers used by the Razor views. Everything follows the language of the current request.</summary>
    public static class Fmt
    {
        private static CultureInfo Culture => CultureInfo.CurrentCulture;
        private static bool German => Culture.TwoLetterISOLanguageName == "de";

        public static string Money(decimal value) =>
            value <= 0
                ? Translations.Get("Бесплатно")
                : value.ToString("N0", Culture).Replace('\u00A0', ' ').Replace('\u202F', ' ') + " ₽";

        // German writes a full stop after the day number.
        public static string Date(DateTime d) => d.ToString(German ? "d. MMMM yyyy" : "d MMMM yyyy", Culture);
        public static string DayMonth(DateTime d) => d.ToString(German ? "d. MMMM" : "d MMMM", Culture);
        public static string Weekday(DateTime d) => d.ToString("dddd", Culture);
        public static string WeekdayCap(DateTime d) { var w = Weekday(d); return char.ToUpper(w[0], Culture) + w[1..]; }
        public static string Time(DateTime d) => d.ToString("HH:mm", Culture);
        public static string Month(DateTime d) => d.ToString("MMM", Culture).TrimEnd('.');
        public static string DateTimeShort(DateTime d) => d.ToString(German ? "d. MMM, HH:mm" : "d MMM, HH:mm", Culture);

        public static string Initials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
        }

        public static string Minutes(int minutes) => minutes % 60 == 0 && minutes >= 60
            ? string.Format(Culture, Translations.Get("{0} ч"), minutes / 60)
            : minutes > 60
                ? string.Format(Culture, Translations.Get("{0} ч {1} мин"), minutes / 60, minutes % 60)
                : string.Format(Culture, Translations.Get("{0} мин"), minutes);

        public static string RoleName(Profile p) => Translations.Get(
            p.IsAdmin ? "Администратор" : p.IsManager ? "Менеджер" : p.IsDoctor ? "Врач" : "Пациент");

        /// <summary>Russian has three plural forms, the other languages use the same table with one or two of them.</summary>
        public static string Years(int n)
        {
            var m10 = n % 10; var m100 = n % 100;
            var form = m100 is >= 11 and <= 14 ? "{0} лет" : m10 == 1 ? "{0} год" : m10 is >= 2 and <= 4 ? "{0} года" : "{0} лет";
            return string.Format(Culture, Translations.Get(form), n);
        }
    }
}
