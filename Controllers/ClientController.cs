using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Controllers
{
    [Route("client")]
    [RoleRequired(RoleIds.Client)]
    public class ClientController : BaseController
    {
        private readonly IBookingService _booking;
        private readonly IReviewService _reviews;
        private readonly ICalendarExporter _calendar;
        private readonly IClinicClock _clock;

        public ClientController(DatabaseContext context, IBookingService booking, IReviewService reviews,
            ICalendarExporter calendar, IClinicClock clock) : base(context)
        {
            _booking = booking;
            _reviews = reviews;
            _calendar = calendar;
            _clock = clock;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var profile = GetProfile();
            var appointments = await _context.Appointments
                .Include(a => a.Staff)
                .Include(a => a.Services)
                .Where(a => a.ClientId == profile.Id)
                .OrderByDescending(a => a.StartAt)
                .ToListAsync();

            ViewBag.Profile = profile;
            return View(appointments);
        }

        private Task<Appointment?> OwnAppointment(long appointmentId, long profileId) =>
            _context.Appointments.Include(a => a.Staff).Include(a => a.Services)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.ClientId == profileId);

        [HttpGet("appointments/{appointmentId:long}")]
        public async Task<IActionResult> Appointment(long appointmentId)
        {
            var appointment = await OwnAppointment(appointmentId, GetProfile().Id);
            if (appointment == null) return NotFound();

            return View("Appointment", appointment);
        }

        [HttpGet("appointments/{appointmentId:long}/calendar.ics")]
        public async Task<IActionResult> Calendar(long appointmentId)
        {
            var appointment = await OwnAppointment(appointmentId, GetProfile().Id);
            if (appointment == null) return NotFound();

            return File(System.Text.Encoding.UTF8.GetBytes(_calendar.Export(appointment)), "text/calendar; charset=utf-8", $"appointment-{appointment.Id}.ics");
        }

        [HttpPost("appointments/{appointmentId:long}/cancel")]
        public async Task<IActionResult> Cancel(long appointmentId)
        {
            switch (await _booking.CancelAsync(GetProfile(), appointmentId))
            {
                case CancelStatus.NotFound:
                    return NotFound();
                case CancelStatus.TooLate:
                    TempData["Error"] = "Отменить запись можно не позднее чем за 2 часа до приёма. Позвоните нам, и мы что-нибудь придумаем.";
                    break;
                default:
                    TempData["Success"] = "Запись отменена. Время снова доступно для записи.";
                    break;
            }

            return RedirectToAction("Index");
        }

        [HttpGet("review")]
        public async Task<IActionResult> Review()
        {
            var profile = GetProfile();
            var now = _clock.Now;
            if (!await _context.Appointments.AnyAsync(a => a.ClientId == profile.Id && a.StartAt < now))
                return View("Review/DeniedMessage");

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ProfileId == profile.Id);
            return View("Review/Edit", review ?? new Review());
        }

        [HttpPost("review/create")]
        public async Task<IActionResult> ReviewCreate(Review review)
        {
            if (!ModelState.IsValid)
                return View("Review/Edit", review);

            if (await _reviews.SubmitAsync(GetProfile(), review.Text, review.Rating) == ReviewSubmitStatus.NoFinishedVisit)
                return Forbid();

            TempData["Success"] = "Спасибо! Отзыв отправлен на модерацию.";
            return RedirectToAction("Index");
        }

        [HttpPost("review/hide")]
        public async Task<IActionResult> Hide()
        {
            if (!await _reviews.HideOwnAsync(GetProfile().Id)) return NotFound();
            return RedirectToAction("Review");
        }
    }
}
