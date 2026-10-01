using Microsoft.Extensions.Options;

namespace DentalClinic.Services
{
    /// <summary>
    /// Current time in the clinic's time zone. Every appointment time in the database is clinic-local,
    /// so the server's own time zone (usually UTC in containers) must never leak into the logic.
    /// </summary>
    public interface IClinicClock
    {
        DateTime Now { get; }
        DateTime Today { get; }
    }

    public sealed class ClinicClock : IClinicClock
    {
        private readonly TimeProvider _time;
        private readonly TimeZoneInfo _zone;

        public ClinicClock(TimeProvider time, IOptions<ClinicOptions> options)
        {
            _time = time;
            _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        }

        public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _zone).DateTime, DateTimeKind.Unspecified);

        public DateTime Today => Now.Date;
    }
}
