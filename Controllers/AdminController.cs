using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.DTO;
using DentalClinic.Models.ViewModels;
using DentalClinic.Services;

namespace DentalClinic.Controllers
{
    [Route("admin")]
    [RoleRequired(RoleIds.Admin)]
    public class AdminController : BaseController
    {
        private readonly IClinicClock _clock;
        private readonly IProfileService _profiles;
        private readonly IReviewService _reviews;
        private readonly IScheduleService _schedule;

        public AdminController(DatabaseContext context, IClinicClock clock, IProfileService profiles,
            IReviewService reviews, IScheduleService schedule) : base(context)
        {
            _clock = clock;
            _profiles = profiles;
            _reviews = reviews;
            _schedule = schedule;
        }

        // ---------------------------------------------------------------- overview

        [HttpGet("")]
        public async Task<IActionResult> Overview()
        {
            var now = _clock.Now;
            var next = await _context.Appointments.Include(a => a.Staff).Include(a => a.Services).Include(a => a.Client)
                .Where(a => a.ClientId != null && a.StartAt > now)
                .OrderBy(a => a.StartAt).Take(6).ToListAsync();

            return View(new AdminOverview
            {
                Upcoming = await _context.Appointments.CountAsync(a => a.ClientId != null && a.StartAt > now),
                FreeSlots = await _context.Appointments.CountAsync(a => a.ClientId == null && a.StartAt > now),
                Clients = await _context.Profiles.CountAsync(p => p.RoleId == RoleIds.Client),
                Doctors = await _context.Staffs.CountAsync(),
                PendingReviews = await _context.Reviews.CountAsync(r => !r.IsVisible),
                NextAppointments = ToRows(next)
            });
        }

        // ---------------------------------------------------------------- appointments

        [HttpGet("appointments")]
        public async Task<IActionResult> Index(string? filter, string? q, int page = 1)
        {
            var now = _clock.Now;
            var query = _context.Appointments.Include(a => a.Staff).Include(a => a.Client).AsQueryable();

            query = filter switch
            {
                "free" => query.Where(a => a.ClientId == null && a.StartAt > now).OrderBy(a => a.StartAt),
                "past" => query.Where(a => a.StartAt <= now).OrderByDescending(a => a.StartAt),
                _ => query.Where(a => a.ClientId != null && a.StartAt > now).OrderBy(a => a.StartAt)
            };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(a => a.Staff!.FullName.Contains(term) || (a.Client != null && (a.Client.FullName.Contains(term) || a.Client.Email!.Contains(term))));
            }

            ViewBag.Filter = filter is "free" or "past" ? filter : "upcoming";
            ViewBag.Query = q;
            return View("Appointments/Index", (await query.ToPagedAsync(page)).Map(ToRows));
        }

        private async Task<AppointmentEditViewModel> EditModel(Models.Appointment appointment, bool isNew) => new()
        {
            Appointment = appointment,
            IsNew = isNew,
            Staff = await _context.Staffs.OrderBy(s => s.Id).ToListAsync(),
            Clients = await _context.Profiles.Where(p => p.RoleId == RoleIds.Client).OrderBy(p => p.FullName).ToListAsync()
        };

        [HttpGet("appointments/new")]
        public async Task<IActionResult> AppointmentCreate() =>
            View("Appointments/Edit", await EditModel(new Models.Appointment { StartAt = _clock.Today.AddDays(1).AddHours(10), Duration = 60 }, true));

        [HttpPost("appointments/new")]
        public async Task<IActionResult> AppointmentStore(Models.Appointment appointment)
        {
            await ValidateAppointment(appointment, null);
            if (!ModelState.IsValid)
                return View("Appointments/Edit", await EditModel(appointment, true));

            appointment.Id = 0;
            appointment.Recommendation ??= string.Empty;
            appointment.DurationChangeReason ??= string.Empty;
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Приём добавлен в расписание.";
            return RedirectToAction("Index", new { filter = appointment.IsBooked ? null : "free" });
        }

        [HttpGet("appointments/{appointmentId:long}")]
        public async Task<IActionResult> Appointment(long appointmentId)
        {
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appointment == null) return NotFound();

            return View("Appointments/Edit", await EditModel(appointment, false));
        }

        [HttpPost("appointments/{appointmentId:long}")]
        public async Task<IActionResult> AppointmentUpdate(long appointmentId, Models.Appointment appointment)
        {
            var existing = await _context.Appointments.Include(a => a.Services).FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (existing == null) return NotFound();

            await ValidateAppointment(appointment, appointmentId);
            if (!ModelState.IsValid)
            {
                appointment.Id = appointmentId;
                return View("Appointments/Edit", await EditModel(appointment, false));
            }

            existing.StaffId = appointment.StaffId;
            existing.StartAt = appointment.StartAt;
            existing.Duration = appointment.Duration;
            existing.Recommendation = appointment.Recommendation ?? string.Empty;
            existing.DurationChangeReason = appointment.DurationChangeReason ?? string.Empty;

            // Another patient is booked in, or the slot is released. A released slot keeps nothing of the old visit.
            if (appointment.ClientId != existing.ClientId)
            {
                if (appointment.ClientId == null)
                    existing.Release();
                else
                    existing.ClientId = appointment.ClientId;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Изменения сохранены.";
            return RedirectToAction("Index");
        }

        private async Task ValidateAppointment(Models.Appointment appointment, long? ownId)
        {
            if (!await _context.Staffs.AnyAsync(s => s.Id == appointment.StaffId))
                ModelState.AddModelError("StaffId", "Выберите врача");

            if (appointment.ClientId is { } clientId && !await _context.Profiles.AnyAsync(p => p.Id == clientId && p.RoleId == RoleIds.Client))
                ModelState.AddModelError("ClientId", "Выберите пациента из списка");

            // The database enforces this too (unique index), the check only gives a readable message.
            if (await _context.Appointments.AnyAsync(a => a.StaffId == appointment.StaffId && a.StartAt == appointment.StartAt && a.Id != ownId))
                ModelState.AddModelError("StartAt", "У этого врача уже есть приём на это время");
        }

        [HttpPost("appointments/{appointmentId:long}/delete")]
        public async Task<IActionResult> AppointmentDelete(long appointmentId)
        {
            var deleted = await _context.Appointments.Where(a => a.Id == appointmentId).ExecuteDeleteAsync();
            if (deleted > 0) TempData["Success"] = "Приём удалён.";
            return RedirectToAction("Index");
        }

        // ---------------------------------------------------------------- schedule generation

        private async Task<GenerateScheduleViewModel> ScheduleForm(GenerateScheduleModel form) => new()
        {
            Form = form,
            Staff = await _context.Staffs.OrderBy(s => s.Id).ToListAsync(),
            CanChooseDoctor = true,
            PostController = "Admin",
            PostAction = nameof(GenerateSchedule)
        };

        [HttpGet("schedule")]
        public async Task<IActionResult> Schedule()
        {
            var today = _clock.Today;
            return View(await ScheduleForm(new GenerateScheduleModel { From = today.AddDays(1), To = today.AddDays(14) }));
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> GenerateSchedule(GenerateScheduleModel form)
        {
            if (!ModelState.IsValid)
                return View("Schedule", await ScheduleForm(form));

            var result = await _schedule.GenerateAsync(form);
            switch (result.Status)
            {
                case GenerateStatus.UnknownDoctor:
                    ModelState.AddModelError("StaffId", "Выберите врача из списка");
                    return View("Schedule", await ScheduleForm(form));
                case GenerateStatus.Conflict:
                    TempData["Error"] = "Расписание изменили одновременно. Повторите создание, существующие окна будут пропущены.";
                    return RedirectToAction("Schedule");
            }

            TempData["Success"] = $"Создано окон {result.Created}, уже существовало {result.SkippedExisting}.";
            return RedirectToAction("Index", new { filter = "free" });
        }

        // ---------------------------------------------------------------- profiles

        [HttpGet("profiles")]
        public async Task<IActionResult> Profiles(string? q, long? role, int page = 1)
        {
            var query = _context.Profiles.AsQueryable();
            if (role is { } roleId) query = query.Where(p => p.RoleId == roleId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p => p.FullName.Contains(term) || p.Email!.Contains(term) || p.PhoneNumber!.Contains(term));
            }

            ViewBag.Query = q;
            ViewBag.Role = role;
            return View("Profiles/Index", await query.OrderBy(p => p.RoleId).ThenBy(p => p.FullName).ToPagedAsync(page));
        }

        [HttpGet("profiles/{profileId:long}")]
        public async Task<IActionResult> Profile(long profileId)
        {
            var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.Id == profileId);
            if (profile == null) return NotFound();

            return View("Profiles/Edit", new ProfileEditModel
            {
                Id = profile.Id,
                FullName = profile.FullName,
                Email = profile.Email ?? string.Empty,
                Phone = profile.PhoneNumber,
                RoleId = profile.RoleId,
                IsBanned = profile.IsBanned
            });
        }

        [HttpGet("profiles/create")]
        public IActionResult ProfileCreate() => View("Profiles/Create", new NewProfile { RoleTitle = RoleTitle.Клиент });

        [HttpPost("profiles/create")]
        public async Task<IActionResult> ProfileStore(NewProfile model)
        {
            if (!ModelState.IsValid)
                return View("Profiles/Create", model);

            var (result, _) = await _profiles.CreateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "Профиль создан.";
                return RedirectToAction("Profiles");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", IdentityErrors.Translate(error));

            return View("Profiles/Create", model);
        }

        [HttpPost("profiles/{profileId:long}")]
        public async Task<IActionResult> ProfileUpdate(long profileId, ProfileEditModel model)
        {
            var profile = await _context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
            if (profile == null) return NotFound();

            if (ModelState.IsValid)
            {
                var result = await _profiles.UpdateAsync(profileId, model);
                switch (result.Status)
                {
                    case UpdateProfileStatus.Updated:
                        TempData["Success"] = "Профиль обновлён.";
                        return RedirectToAction("Profiles");
                    case UpdateProfileStatus.EmailTaken:
                        ModelState.AddModelError(nameof(model.Email), "Этот email уже занят");
                        break;
                    case UpdateProfileStatus.Invalid:
                        foreach (var error in result.Errors)
                            ModelState.AddModelError(nameof(model.Email), IdentityErrors.Translate(error));
                        break;
                    default:
                        return NotFound();
                }
            }

            model.Id = profileId;
            model.RoleId = profile.RoleId;
            model.IsBanned = profile.IsBanned;
            return View("Profiles/Edit", model);
        }

        [HttpPost("profiles/{profileId:long}/delete")]
        public async Task<IActionResult> ProfileDelete(long profileId)
        {
            switch (await _profiles.DeleteAsync(profileId))
            {
                case ProfileChangeStatus.NotFound: return NotFound();
                case ProfileChangeStatus.IsAdmin: TempData["Error"] = "Администратора удалить нельзя."; break;
                case ProfileChangeStatus.HasVisits:
                    TempData["Error"] = "У врача есть записи пациентов, удаление затронуло бы их визиты. Заблокируйте профиль, чтобы закрыть вход.";
                    break;
                default: TempData["Success"] = "Профиль удалён."; break;
            }
            return RedirectToAction("Profiles");
        }

        [HttpPost("profiles/{profileId:long}/ban")]
        public Task<IActionResult> Ban(long profileId) => SetBan(profileId, true);

        [HttpPost("profiles/{profileId:long}/unban")]
        public Task<IActionResult> Unban(long profileId) => SetBan(profileId, false);

        private async Task<IActionResult> SetBan(long profileId, bool banned)
        {
            switch (await _profiles.SetBannedAsync(profileId, banned))
            {
                case ProfileChangeStatus.NotFound: return NotFound();
                case ProfileChangeStatus.IsAdmin: return Forbid();
                default:
                    TempData["Success"] = banned ? "Аккаунт заблокирован." : "Аккаунт разблокирован.";
                    return RedirectToAction("Profiles");
            }
        }

        // ---------------------------------------------------------------- reviews

        [HttpGet("reviews")]
        public async Task<IActionResult> Reviews(string? filter, int page = 1)
        {
            var query = _context.Reviews.AsQueryable();
            query = filter switch
            {
                "pending" => query.Where(r => !r.IsVisible),
                "published" => query.Where(r => r.IsVisible),
                _ => query
            };

            ViewBag.Filter = filter is "pending" or "published" ? filter : "all";
            var paged = await query.OrderByDescending(r => r.CreatedAt).ToPagedAsync(page);
            var cards = await ToCardsAsync(paged.Items);
            return View("Reviews/Index", paged.Map(_ => cards));
        }

        [HttpGet("reviews/{reviewId:long}")]
        public async Task<IActionResult> Review(long reviewId)
        {
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null) return NotFound();

            return View("Reviews/Edit", review);
        }

        [HttpPost("reviews/{reviewId:long}")]
        public async Task<IActionResult> ReviewUpdate(long reviewId, Review updated)
        {
            if (!ModelState.IsValid)
            {
                updated.Id = reviewId;
                return View("Reviews/Edit", updated);
            }

            if (!await _reviews.UpdateTextAsync(reviewId, updated.Text)) return NotFound();

            TempData["Success"] = "Отзыв обновлён.";
            return RedirectToAction("Reviews");
        }

        [HttpPost("reviews/{reviewId:long}/delete")]
        public async Task<IActionResult> ReviewDelete(long reviewId)
        {
            if (await _reviews.DeleteAsync(reviewId)) TempData["Success"] = "Отзыв удалён.";
            return RedirectToAction("Reviews");
        }

        [HttpPost("reviews/{reviewId:long}/show")]
        public Task<IActionResult> ShowReview(long reviewId) => SetVisibility(reviewId, true);

        [HttpPost("reviews/{reviewId:long}/hide")]
        public Task<IActionResult> HideReview(long reviewId) => SetVisibility(reviewId, false);

        private async Task<IActionResult> SetVisibility(long reviewId, bool visible)
        {
            if (!await _reviews.SetVisibleAsync(reviewId, visible)) return NotFound();
            return RedirectToAction("Reviews");
        }
    }
}
