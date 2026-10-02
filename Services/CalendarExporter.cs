using System.Text;
using DentalClinic.Localization;
using DentalClinic.Models;
using Microsoft.Extensions.Options;

namespace DentalClinic.Services
{
    /// <summary>Builds an iCalendar (.ics) event for a booked visit.</summary>
    public interface ICalendarExporter
    {
        string Export(Appointment appointment);
    }

    /// <summary>Times are written in UTC, which every calendar application understands without a VTIMEZONE block.</summary>
    public sealed class CalendarExporter : ICalendarExporter
    {
        private readonly ClinicOptions _clinic;
        private readonly TimeZoneInfo _zone;
        private readonly TimeProvider _time;

        public CalendarExporter(IOptions<ClinicOptions> clinic, TimeProvider time)
        {
            _clinic = clinic.Value;
            _zone = TimeZoneInfo.FindSystemTimeZoneById(_clinic.TimeZone);
            _time = time;
        }

        public string Export(Appointment appointment)
        {
            var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(appointment.StartAt, DateTimeKind.Unspecified), _zone);
            var end = start.AddMinutes(appointment.Duration);

            var services = appointment.Services.Count > 0
                ? string.Join(", ", appointment.Services.Select(s => Translations.Get(s.Title)))
                : Translations.Get("Приём врача");
            var doctor = Translations.Get(appointment.Staff?.DisplayName ?? "врач");

            var lines = new[]
            {
                "BEGIN:VCALENDAR",
                "VERSION:2.0",
                "PRODID:-//Aurora Dent//Appointments//RU",
                "CALSCALE:GREGORIAN",
                "METHOD:PUBLISH",
                "BEGIN:VEVENT",
                $"UID:appointment-{appointment.Id}@aurora-dent",
                $"DTSTAMP:{Format(_time.GetUtcNow().UtcDateTime)}",
                $"DTSTART:{Format(start)}",
                $"DTEND:{Format(end)}",
                $"SUMMARY:{Escape($"{Translations.Get(_clinic.Name)}: {services}")}",
                $"DESCRIPTION:{Escape(string.Format(Translations.Get("Врач: {0}. Телефон клиники: {1}"), doctor, _clinic.Phone))}",
                $"LOCATION:{Escape(Translations.Get(_clinic.Address))}",
                "BEGIN:VALARM",
                "TRIGGER:-PT2H",
                "ACTION:DISPLAY",
                $"DESCRIPTION:{Escape(Translations.Get("Приём в клинике через 2 часа"))}",
                "END:VALARM",
                "END:VEVENT",
                "END:VCALENDAR"
            };

            return string.Join("\r\n", lines) + "\r\n";
        }

        private static string Format(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'");

        private static string Escape(string text) => new StringBuilder(text)
            .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n")
            .ToString();
    }
}
