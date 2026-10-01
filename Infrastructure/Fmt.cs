using System.Globalization;
using DentalClinic.Models;

namespace DentalClinic.Infrastructure
{
    /// <summary>Small formatting helpers used by the Razor views (ru-RU everywhere).</summary>
    public static class Fmt
    {
        private static readonly CultureInfo Ru = new("ru-RU");

        public static string Money(decimal value) =>
            value <= 0 ? "Бесплатно" : value.ToString("N0", Ru).Replace(' ', ' ') + " ₽";

        public static string Date(DateTime d) => d.ToString("d MMMM yyyy", Ru);
        public static string DayMonth(DateTime d) => d.ToString("d MMMM", Ru);
        public static string Weekday(DateTime d) => d.ToString("dddd", Ru);
        public static string WeekdayCap(DateTime d) { var w = Weekday(d); return char.ToUpper(w[0], Ru) + w[1..]; }
        public static string Time(DateTime d) => d.ToString("HH:mm", Ru);
        public static string Month(DateTime d) => d.ToString("MMM", Ru).TrimEnd('.');
        public static string DateTimeShort(DateTime d) => d.ToString("d MMM, HH:mm", Ru);

        public static string Initials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
        }

        public static string Minutes(int minutes) => minutes % 60 == 0 && minutes >= 60
            ? $"{minutes / 60} ч"
            : minutes > 60 ? $"{minutes / 60} ч {minutes % 60} мин" : $"{minutes} мин";

        public static string RoleName(Profile p) =>
            p.IsAdmin ? "Администратор" : p.IsManager ? "Менеджер" : p.IsDoctor ? "Врач" : "Пациент";

        public static string Years(int n)
        {
            var m10 = n % 10; var m100 = n % 100;
            return m100 is >= 11 and <= 14 ? $"{n} лет" : m10 == 1 ? $"{n} год" : m10 is >= 2 and <= 4 ? $"{n} года" : $"{n} лет";
        }
    }
}
