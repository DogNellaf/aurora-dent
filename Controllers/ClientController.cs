using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;

namespace DentalClinic.Controllers
{
    [Route("client")]
    [RoleRequired(RoleIds.Client)]
    public class ClientController : BaseController
    {
        public ClientController(DatabaseContext context) : base(context) { }

        [HttpGet("")]
        public IActionResult Index()
        {
            var profile = GetProfile();
            var appointments = _context.Appointments
                .Include(a => a.Staff)
                .Include(a => a.Services)
                .Where(a => a.ClientId == profile.Id)
                .OrderByDescending(a => a.StartAt)
                .ToList();

            ViewBag.Profile = profile;
            return View(appointments);
        }

        [HttpGet("appointments/{appointmentId:long}")]
        public IActionResult Appointment(long appointmentId)
        {
            var profile = GetProfile();
            var appointment = _context.Appointments
                .Include(a => a.Staff)
                .Include(a => a.Services)
                .FirstOrDefault(a => a.Id == appointmentId && a.ClientId == profile.Id);

            if (appointment == null) return NotFound();
            return View("Appointment", appointment);
        }

        [HttpPost("appointments/{appointmentId:long}/cancel")]
        public IActionResult Cancel(long appointmentId)
        {
            var profile = GetProfile();
            var appointment = _context.Appointments
                .Include(a => a.Services)
                .FirstOrDefault(a => a.Id == appointmentId && a.ClientId == profile.Id);

            if (appointment == null) return NotFound();

            if (appointment.StartAt <= DateTime.Now.AddHours(2))
            {
                TempData["Error"] = "Отменить запись можно не позднее чем за 2 часа до приёма. Позвоните нам, и мы что-нибудь придумаем.";
                return RedirectToAction("Index");
            }

            // The slot goes back to the schedule for other patients.
            appointment.ClientId = 0;
            appointment.Services.Clear();
            _context.SaveChanges();

            TempData["Success"] = "Запись отменена. Время снова доступно для записи.";
            return RedirectToAction("Index");
        }

        [HttpGet("review")]
        public IActionResult Review()
        {
            var profile = GetProfile();
            var hasPastAppointment = _context.Appointments
                .Any(a => a.ClientId == profile.Id && a.StartAt < DateTime.Now);

            if (!hasPastAppointment)
                return View("Review/DeniedMessage");

            var review = _context.Reviews.FirstOrDefault(r => r.ProfileId == profile.Id);
            return View("Review/Edit", review ?? new Review());
        }

        [HttpPost("review/create")]
        public IActionResult ReviewCreate(Review review)
        {
            var profile = GetProfile();

            var hasPastAppointment = _context.Appointments
                .Any(a => a.ClientId == profile.Id && a.StartAt < DateTime.Now);
            if (!hasPastAppointment) return Forbid();

            if (!ModelState.IsValid)
                return View("Review/Edit", review);

            var rating = Math.Clamp(review.Rating, 1, 5);
            var existing = _context.Reviews.FirstOrDefault(r => r.ProfileId == profile.Id);
            if (existing != null)
            {
                existing.Text = review.Text;
                existing.Rating = rating;
                existing.IsVisible = false; // edited reviews go through moderation again
            }
            else
            {
                _context.Reviews.Add(new Review
                {
                    ProfileId = profile.Id,
                    Text = review.Text,
                    Rating = rating,
                    IsVisible = false,
                    CreatedAt = DateTime.Now
                });
            }

            _context.SaveChanges();
            TempData["Success"] = "Спасибо! Отзыв отправлен на модерацию.";
            return RedirectToAction("Index");
        }

        [HttpPost("review/hide")]
        public IActionResult Hide()
        {
            var profile = GetProfile();
            var review = _context.Reviews.FirstOrDefault(r => r.ProfileId == profile.Id);
            if (review == null) return NotFound();

            review.IsVisible = false;
            _context.SaveChanges();
            return RedirectToAction("Review");
        }
    }
}
