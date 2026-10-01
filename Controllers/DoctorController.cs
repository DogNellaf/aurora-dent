using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.DTO;
using DentalClinic.Models.ViewModels;
using DentalClinic.Services;

namespace DentalClinic.Controllers
{
    [Route("doctor")]
    [RoleRequired(RoleIds.Doctor)]
    public class DoctorController : BaseController
    {
        private readonly IClinicClock _clock;
        private readonly IScheduleService _schedule;

        public DoctorController(DatabaseContext context, IClinicClock clock, IScheduleService schedule) : base(context)
        {
            _clock = clock;
            _schedule = schedule;
        }

        private async Task<Staff?> GetStaffAsync()
        {
            var id = GetProfile().Id;
            return await _context.Staffs.FirstOrDefaultAsync(s => s.ProfileId == id);
        }

        private IQueryable<Appointment> BookedOf(Staff staff) =>
            _context.Appointments.Include(a => a.Services).Include(a => a.Client)
                .Where(a => a.StaffId == staff.Id && a.ClientId != null);

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var staff = await GetStaffAsync();
            if (staff == null) return NotFound();

            var now = _clock.Now;
            var booked = BookedOf(staff);
            var todayStart = now.Date;

            var today = await booked.Where(a => a.StartAt >= todayStart && a.StartAt < todayStart.AddDays(1)).OrderBy(a => a.StartAt).ToListAsync();
            var current = today.FirstOrDefault(a => a.IsActiveAt(now));
            var next = await booked.Where(a => a.StartAt > now).OrderBy(a => a.StartAt).FirstOrDefaultAsync();

            return View(new DoctorDashboard
            {
                Staff = staff,
                Current = current == null ? null : ToRows(new[] { current }).First(),
                Next = next == null ? null : ToRows(new[] { next }).First(),
                Today = ToRows(today),
                UpcomingCount = await booked.CountAsync(a => a.StartAt > now),
                PatientsCount = await booked.Select(a => a.ClientId).Distinct().CountAsync()
            });
        }

        [HttpGet("appointments")]
        public async Task<IActionResult> Appointments(string? filter, int page = 1)
        {
            var staff = await GetStaffAsync();
            if (staff == null) return NotFound();

            var now = _clock.Now;
            var query = BookedOf(staff);
            query = filter == "past"
                ? query.Where(a => a.StartAt < now).OrderByDescending(a => a.StartAt)
                : query.Where(a => a.StartAt >= now).OrderBy(a => a.StartAt);

            ViewBag.Filter = filter == "past" ? "past" : "upcoming";
            var paged = await query.ToPagedAsync(page);
            return View(paged.Map(ToRows));
        }

        [HttpGet("appointments/{appointmentId:long}")]
        public async Task<IActionResult> Appointment(long appointmentId)
        {
            var staff = await GetStaffAsync();
            if (staff == null) return NotFound();

            var appointment = await BookedOf(staff).FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appointment == null) return NotFound();

            return View(ToRows(new[] { appointment }).First());
        }

        private async Task<Appointment?> OwnAppointment(long appointmentId)
        {
            var staff = await GetStaffAsync();
            if (staff == null) return null;
            return await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && a.StaffId == staff.Id);
        }

        [HttpPost("appointments/{appointmentId:long}/recommend")]
        public async Task<IActionResult> AddRecommendation(long appointmentId, string? recommendation, string? returnTo)
        {
            var appointment = await OwnAppointment(appointmentId);
            if (appointment == null) return NotFound();

            appointment.Recommendation = (recommendation ?? string.Empty).Trim();
            await _context.SaveChangesAsync();

            TempData["Success"] = "Рекомендации сохранены и доступны пациенту в личном кабинете.";
            return returnTo == "detail"
                ? RedirectToAction("Appointment", new { appointmentId })
                : RedirectToAction("Index");
        }

        [HttpPost("appointments/{appointmentId:long}/extend")]
        public async Task<IActionResult> ExtendAppointment(long appointmentId, short additionalMinutes, string? reason)
        {
            var appointment = await OwnAppointment(appointmentId);
            if (appointment == null) return NotFound();

            if (additionalMinutes <= 0 || additionalMinutes > 120)
            {
                TempData["Error"] = "Укажите количество минут для продления от 1 до 120.";
                return RedirectToAction("Index");
            }

            appointment.Duration = (short)Math.Min(appointment.Duration + additionalMinutes, 480);
            appointment.DurationChangeReason = (reason ?? string.Empty).Trim();
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Приём продлён на {additionalMinutes} мин.";
            return RedirectToAction("Index");
        }

        [HttpPost("appointments/{appointmentId:long}/end")]
        public async Task<IActionResult> EndAppointmentEarly(long appointmentId, string? reason)
        {
            var appointment = await OwnAppointment(appointmentId);
            if (appointment == null) return NotFound();

            var now = _clock.Now;
            if (now > appointment.StartAt)
            {
                appointment.Duration = (short)Math.Max(1, (int)(now - appointment.StartAt).TotalMinutes);
                appointment.DurationChangeReason = (reason ?? string.Empty).Trim();
                await _context.SaveChangesAsync();
                TempData["Success"] = "Приём завершён.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet("clients/{clientId:long}/record")]
        public async Task<IActionResult> ClientRecord(long clientId)
        {
            var client = await _context.Profiles.FirstOrDefaultAsync(p => p.Id == clientId && p.RoleId == RoleIds.Client);
            if (client == null) return NotFound();

            var appointments = await _context.Appointments
                .Include(a => a.Staff)
                .Include(a => a.Services)
                .Where(a => a.ClientId == clientId)
                .OrderByDescending(a => a.StartAt)
                .ToListAsync();

            ViewBag.Client = client;
            return View("ClientRecord", appointments);
        }

        // ------------------------------------------------------------------ own schedule

        private static GenerateScheduleViewModel ScheduleForm(GenerateScheduleModel form, Staff staff) => new()
        {
            Form = form,
            Staff = new List<Staff> { staff },
            CanChooseDoctor = false,
            PostController = "Doctor",
            PostAction = nameof(GenerateSchedule)
        };

        [HttpGet("schedule")]
        public async Task<IActionResult> Schedule()
        {
            var staff = await GetStaffAsync();
            if (staff == null) return NotFound();

            var today = _clock.Today;
            return View(ScheduleForm(new GenerateScheduleModel { From = today.AddDays(1), To = today.AddDays(14) }, staff));
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> GenerateSchedule(GenerateScheduleModel form)
        {
            var staff = await GetStaffAsync();
            if (staff == null) return NotFound();

            if (!ModelState.IsValid)
                return View("Schedule", ScheduleForm(form, staff));

            form.StaffId = staff.Id;
            var result = await _schedule.GenerateAsync(form);
            if (result.Status == GenerateStatus.Conflict)
            {
                TempData["Error"] = "Расписание изменили одновременно. Повторите создание, существующие окна будут пропущены.";
                return RedirectToAction("Schedule");
            }

            TempData["Success"] = $"Создано окон {result.Created}, уже существовало {result.SkippedExisting}.";
            return RedirectToAction("Schedule");
        }
    }
}
