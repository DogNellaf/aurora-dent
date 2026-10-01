using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services
{
    public enum BookingStatus { Booked, NotFound, InThePast, AlreadyTaken }

    public enum CancelStatus { Cancelled, NotFound, TooLate }

    public record BookingResult(BookingStatus Status, Appointment? Appointment = null);

    public interface IBookingService
    {
        Task<BookingResult> BookAsync(Profile client, long appointmentId, long? serviceId);
        Task<CancelStatus> CancelAsync(Profile client, long appointmentId);
    }

    public sealed class BookingService : IBookingService
    {
        /// <summary>A visit can be cancelled up to this long before it starts.</summary>
        public static readonly TimeSpan CancellationWindow = TimeSpan.FromHours(2);

        private readonly DatabaseContext _db;
        private readonly IClinicClock _clock;
        private readonly INotifier _notifier;
        private readonly ILogger<BookingService> _logger;

        public BookingService(DatabaseContext db, IClinicClock clock, INotifier notifier, ILogger<BookingService> logger)
        {
            _db = db;
            _clock = clock;
            _notifier = notifier;
            _logger = logger;
        }

        public async Task<BookingResult> BookAsync(Profile client, long appointmentId, long? serviceId)
        {
            var slot = await _db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (slot is null) return new BookingResult(BookingStatus.NotFound);
            if (slot.StartAt <= _clock.Now) return new BookingResult(BookingStatus.InThePast);

            // Atomic claim: of two simultaneous requests only one updates a row, the other gets zero.
            var claimed = await _db.Appointments
                .Where(a => a.Id == appointmentId && a.ClientId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClientId, (long?)client.Id));

            if (claimed == 0)
            {
                _logger.LogInformation("Slot {AppointmentId} was already taken when profile {ProfileId} tried to book it", appointmentId, client.Id);
                return new BookingResult(BookingStatus.AlreadyTaken);
            }

            var booked = await _db.Appointments.Include(a => a.Staff).Include(a => a.Services).FirstAsync(a => a.Id == appointmentId);
            var service = serviceId is null ? null : await _db.Services.FirstOrDefaultAsync(s => s.Id == serviceId);
            if (service is not null)
            {
                booked.Services.Add(service);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Profile {ProfileId} booked appointment {AppointmentId} with staff {StaffId} at {StartAt}",
                client.Id, booked.Id, booked.StaffId, booked.StartAt);

            await _notifier.BookingConfirmedAsync(booked, client);
            return new BookingResult(BookingStatus.Booked, booked);
        }

        public async Task<CancelStatus> CancelAsync(Profile client, long appointmentId)
        {
            var appointment = await _db.Appointments.Include(a => a.Staff).Include(a => a.Services)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.ClientId == client.Id);
            if (appointment is null) return CancelStatus.NotFound;
            if (appointment.StartAt <= _clock.Now.Add(CancellationWindow)) return CancelStatus.TooLate;

            var snapshot = new Appointment
            {
                Id = appointment.Id,
                StaffId = appointment.StaffId,
                Staff = appointment.Staff,
                StartAt = appointment.StartAt,
                Duration = appointment.Duration,
                Services = appointment.Services.ToList()
            };

            // The slot goes back to the schedule for other patients, without the old recommendation.
            appointment.Release();
            await _db.SaveChangesAsync();

            _logger.LogInformation("Profile {ProfileId} cancelled appointment {AppointmentId}", client.Id, appointmentId);
            await _notifier.BookingCancelledAsync(snapshot, client);
            return CancelStatus.Cancelled;
        }
    }
}
