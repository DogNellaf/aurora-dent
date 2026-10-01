using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.ViewModels;

namespace DentalClinic.Controllers
{
    [Route("doctor")]
    [RoleRequired(RoleIds.Doctor)]
    public class DoctorController : BaseController
    {
        public DoctorController(DatabaseContext context) : base(context) { }

        private Staff? GetStaff()
        {
            var profile = GetProfile();
            return _context.Staffs.FirstOrDefault(s => s.Profile.Id == profile.Id);
        }

        private IQueryable<Appointment> BookedOf(Staff staff) =>
            _context.Appointments.Include(a => a.Services).Where(a => a.StaffId == staff.Id && a.ClientId != 0);

        [HttpGet("")]
        public IActionResult Index()
        {
            var staff = GetStaff();
            if (staff == null) return NotFound();

            var now = DateTime.Now;
            var booked = BookedOf(staff);

            var current = booked.AsEnumerable().FirstOrDefault(a => a.IsActive);
            var next = booked.Where(a => a.StartAt > now).OrderBy(a => a.StartAt).FirstOrDefault();
            var todayStart = now.Date;
            var today = booked.Where(a => a.StartAt >= todayStart && a.StartAt < todayStart.AddDays(1))
                .OrderBy(a => a.StartAt).ToList();

            var rows = ToRows(new[] { current, next }.Where(a => a != null).Select(a => a!).Concat(today));
            AppointmentRow? Find(Appointment? a) => a == null ? null : rows.First(r => r.Appointment.Id == a.Id);

            return View(new DoctorDashboard
            {
                Staff = staff,
                Current = Find(current),
                Next = Find(next),
                Today = rows.Where(r => today.Any(t => t.Id == r.Appointment.Id)).DistinctBy(r => r.Appointment.Id)
                    .OrderBy(r => r.Appointment.StartAt).ToList(),
                UpcomingCount = booked.Count(a => a.StartAt > now),
                PatientsCount = booked.Select(a => a.ClientId).Distinct().Count()
            });
        }

        [HttpGet("appointments")]
        public IActionResult Appointments(string? filter)
        {
            var staff = GetStaff();
            if (staff == null) return NotFound();

            var now = DateTime.Now;
            var query = BookedOf(staff);
            query = filter == "past"
                ? query.Where(a => a.StartAt < now).OrderByDescending(a => a.StartAt)
                : query.Where(a => a.StartAt >= now).OrderBy(a => a.StartAt);

            ViewBag.Filter = filter == "past" ? "past" : "upcoming";
            return View(ToRows(query.ToList()));
        }

        [HttpGet("appointments/{appointmentId:long}")]
        public IActionResult Appointment(long appointmentId)
        {
            var staff = GetStaff();
            if (staff == null) return NotFound();

            var appointment = BookedOf(staff).FirstOrDefault(a => a.Id == appointmentId);
            if (appointment == null) return NotFound();

            return View(ToRows(new[] { appointment }).First());
        }

        [HttpPost("appointments/{appointmentId:long}/recommend")]
        public IActionResult AddRecommendation(long appointmentId, string? recommendation, string? returnTo)
        {
            var staff = GetStaff();
            if (staff == null) return NotFound();

            var appointment = _context.Appointments.FirstOrDefault(a => a.Id == appointmentId && a.StaffId == staff.Id);
            if (appointment == null) return NotFound();

            appointment.Recommendation = (recommendation ?? string.Empty).Trim();
            _context.SaveChanges();

            TempData["Success"] = "Рекомендации сохранены и доступны пациенту в личном кабинете.";
            return returnTo == "detail"
                ? RedirectToAction("Appointment", new { appointmentId })
                : RedirectToAction("Index");
        }

        [HttpPost("appointments/{appointmentId:long}/extend")]
        public IActionResult ExtendAppointment(long appointmentId, short additionalMinutes, string? reason)
        {
            var staff = GetStaff();
            if (staff == null) return NotFound();

            var appointment = _context.Appointments.FirstOrDefault(a => a.Id == appointmentId && a.StaffId == staff.Id);
            if (appointment == null) return NotFound();

            if (additionalMinutes <= 0 || additionalMinutes > 120)
            {
                TempData["Error"] = "Укажите количество минут для продления от 1 до 120.";
                return RedirectToAction("Index");
            }

            appointment.Duration = (short)Math.Min(appointment.Duration + additionalMinutes, 480);
            appointment.DurationChangeReason = (reason ?? string.Empty).Trim();
            _context.SaveChanges();

            TempData["Success"] = $"Приём продлён на {additionalMinutes} мин.";
            return RedirectToAction("Index");
        }

        [HttpPost("appointments/{appointmentId:long}/end")]
        public IActionResult EndAppointmentEarly(long appointmentId, string? reason)
        {
            var staff = GetStaff();
            if (staff == null) return NotFound();

            var appointment = _context.Appointments.FirstOrDefault(a => a.Id == appointmentId && a.StaffId == staff.Id);
            if (appointment == null) return NotFound();

            var now = DateTime.Now;
            if (now > appointment.StartAt)
            {
                appointment.Duration = (short)Math.Max(1, (int)(now - appointment.StartAt).TotalMinutes);
                appointment.DurationChangeReason = (reason ?? string.Empty).Trim();
                _context.SaveChanges();
                TempData["Success"] = "Приём завершён.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet("clients/{clientId:long}/record")]
        public IActionResult ClientRecord(long clientId)
        {
            var client = _context.Profiles.FirstOrDefault(p => p.Id == clientId && p.RoleId == RoleIds.Client);
            if (client == null) return NotFound();

            var appointments = _context.Appointments
                .Include(a => a.Staff)
                .Include(a => a.Services)
                .Where(a => a.ClientId == clientId)
                .OrderByDescending(a => a.StartAt)
                .ToList();

            ViewBag.Client = client;
            return View("ClientRecord", appointments);
        }
    }
}
