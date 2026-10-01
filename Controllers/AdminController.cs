using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.DTO;
using DentalClinic.Models.ViewModels;

namespace DentalClinic.Controllers
{
    [Route("admin")]
    [RoleRequired(RoleIds.Admin)]
    public class AdminController : BaseController
    {
        private readonly UserManager<Profile> _userManager;

        public AdminController(DatabaseContext context, UserManager<Profile> userManager) : base(context)
        {
            _userManager = userManager;
        }

        // ---------------------------------------------------------------- overview

        [HttpGet("")]
        public IActionResult Overview()
        {
            var now = DateTime.Now;
            var next = _context.Appointments.Include(a => a.Staff).Include(a => a.Services)
                .Where(a => a.ClientId != 0 && a.StartAt > now)
                .OrderBy(a => a.StartAt).Take(6).ToList();

            return View(new AdminOverview
            {
                Upcoming = _context.Appointments.Count(a => a.ClientId != 0 && a.StartAt > now),
                FreeSlots = _context.Appointments.Count(a => a.ClientId == 0 && a.StartAt > now),
                Clients = _context.Profiles.Count(p => p.RoleId == RoleIds.Client),
                Doctors = _context.Staffs.Count(),
                PendingReviews = _context.Reviews.Count(r => !r.IsVisible),
                NextAppointments = ToRows(next)
            });
        }

        // ---------------------------------------------------------------- appointments

        [HttpGet("appointments")]
        public IActionResult Index(string? filter)
        {
            var now = DateTime.Now;
            var query = _context.Appointments.Include(a => a.Staff).AsQueryable();

            query = filter switch
            {
                "free" => query.Where(a => a.ClientId == 0 && a.StartAt > now).OrderBy(a => a.StartAt),
                "past" => query.Where(a => a.StartAt <= now).OrderByDescending(a => a.StartAt),
                _ => query.Where(a => a.ClientId != 0 && a.StartAt > now).OrderBy(a => a.StartAt)
            };

            ViewBag.Filter = filter is "free" or "past" ? filter : "upcoming";
            return View("Appointments/Index", ToRows(query.Take(200).ToList()));
        }

        private AppointmentEditViewModel EditModel(Appointment appointment, bool isNew) => new()
        {
            Appointment = appointment,
            IsNew = isNew,
            Staff = _context.Staffs.OrderBy(s => s.Id).ToList(),
            Clients = _context.Profiles.Where(p => p.RoleId == RoleIds.Client).OrderBy(p => p.FullName).ToList()
        };

        [HttpGet("appointments/new")]
        public IActionResult AppointmentCreate()
        {
            var start = DateTime.Today.AddDays(1).AddHours(10);
            return View("Appointments/Edit", EditModel(new Models.Appointment { StartAt = start, Duration = 60 }, true));
        }

        [HttpPost("appointments/new")]
        public IActionResult AppointmentStore(Appointment appointment)
        {
            if (!_context.Staffs.Any(s => s.Id == appointment.StaffId))
                ModelState.AddModelError("StaffId", "Выберите врача");

            if (!ModelState.IsValid)
                return View("Appointments/Edit", EditModel(appointment, true));

            appointment.Id = 0;
            appointment.Recommendation ??= string.Empty;
            appointment.DurationChangeReason ??= string.Empty;
            _context.Appointments.Add(appointment);
            _context.SaveChanges();

            TempData["Success"] = "Приём добавлен в расписание.";
            return RedirectToAction("Index", new { filter = appointment.IsBooked ? null : "free" });
        }

        [HttpGet("appointments/{appointmentId:long}")]
        public IActionResult Appointment(long appointmentId)
        {
            var appointment = _context.Appointments.FirstOrDefault(a => a.Id == appointmentId);
            if (appointment == null) return NotFound();

            return View("Appointments/Edit", EditModel(appointment, false));
        }

        [HttpPost("appointments/{appointmentId:long}")]
        public IActionResult AppointmentUpdate(long appointmentId, Appointment appointment)
        {
            var existing = _context.Appointments.FirstOrDefault(a => a.Id == appointmentId);
            if (existing == null) return NotFound();

            if (!_context.Staffs.Any(s => s.Id == appointment.StaffId))
                ModelState.AddModelError("StaffId", "Выберите врача");

            if (!ModelState.IsValid)
            {
                appointment.Id = appointmentId;
                return View("Appointments/Edit", EditModel(appointment, false));
            }

            existing.StaffId = appointment.StaffId;
            existing.ClientId = appointment.ClientId;
            existing.StartAt = appointment.StartAt;
            existing.Duration = appointment.Duration;
            existing.Recommendation = appointment.Recommendation ?? string.Empty;
            existing.DurationChangeReason = appointment.DurationChangeReason ?? string.Empty;
            _context.SaveChanges();

            TempData["Success"] = "Изменения сохранены.";
            return RedirectToAction("Index");
        }

        [HttpPost("appointments/{appointmentId:long}/delete")]
        public IActionResult AppointmentDelete(long appointmentId)
        {
            var appointment = _context.Appointments.FirstOrDefault(a => a.Id == appointmentId);
            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                _context.SaveChanges();
                TempData["Success"] = "Приём удалён.";
            }

            return RedirectToAction("Index");
        }

        // ---------------------------------------------------------------- profiles

        [HttpGet("profiles")]
        public IActionResult Profiles()
        {
            var profiles = _context.Profiles.OrderBy(p => p.RoleId).ThenBy(p => p.FullName).ToList();
            return View("Profiles/Index", profiles);
        }

        [HttpGet("profiles/{profileId:long}")]
        public IActionResult Profile(long profileId)
        {
            var profile = _context.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return NotFound();

            return View("Profiles/Edit", new ProfileEditModel
            {
                Id = profile.Id, FullName = profile.FullName, Email = profile.Email ?? string.Empty,
                Phone = profile.PhoneNumber, RoleId = profile.RoleId, IsBanned = profile.IsBanned
            });
        }

        [HttpGet("profiles/create")]
        public IActionResult ProfileCreate() => View("Profiles/Create", new NewProfile { RoleTitle = RoleTitle.Клиент });

        [HttpPost("profiles/create")]
        public async Task<IActionResult> ProfileStore(NewProfile model)
        {
            if (!ModelState.IsValid)
                return View("Profiles/Create", model);

            long roleId = model.RoleTitle switch
            {
                RoleTitle.Администратор => RoleIds.Admin,
                RoleTitle.Менеджер => RoleIds.Manager,
                RoleTitle.Доктор => RoleIds.Doctor,
                _ => RoleIds.Client
            };

            var profile = new Profile
            {
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.Phone,
                FullName = model.FullName.Trim(),
                EmailConfirmed = true,
                RoleId = roleId
            };

            var result = await _userManager.CreateAsync(profile, model.Password);
            if (result.Succeeded)
            {
                // A doctor needs a staff card, otherwise they cannot appear in the schedule or open their cabinet.
                if (roleId == RoleIds.Doctor)
                {
                    _context.Staffs.Add(new Staff
                    {
                        Profile = profile, ExternalLogin = model.Email, FullName = profile.FullName,
                        Specialty = "Стоматолог", Bio = "Информация о враче скоро появится."
                    });
                    _context.SaveChanges();
                }

                TempData["Success"] = "Профиль создан.";
                return RedirectToAction("Profiles");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View("Profiles/Create", model);
        }

        [HttpPost("profiles/{profileId:long}")]
        public async Task<IActionResult> ProfileUpdate(long profileId, ProfileEditModel model)
        {
            var profile = _context.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Id = profileId; model.RoleId = profile.RoleId; model.IsBanned = profile.IsBanned;
                return View("Profiles/Edit", model);
            }

            var duplicate = _context.Profiles.Any(p => p.Id != profileId && p.NormalizedEmail == model.Email.ToUpperInvariant());
            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.Email), "Этот email уже занят");
                model.Id = profileId; model.RoleId = profile.RoleId; model.IsBanned = profile.IsBanned;
                return View("Profiles/Edit", model);
            }

            profile.FullName = model.FullName.Trim();
            profile.PhoneNumber = model.Phone;
            await _userManager.SetEmailAsync(profile, model.Email);
            await _userManager.SetUserNameAsync(profile, model.Email);

            var staff = _context.Staffs.FirstOrDefault(s => s.Profile.Id == profileId);
            if (staff != null)
            {
                staff.FullName = profile.FullName;
                staff.ExternalLogin = model.Email;
                _context.SaveChanges();
            }

            TempData["Success"] = "Профиль обновлён.";
            return RedirectToAction("Profiles");
        }

        [HttpPost("profiles/{profileId:long}/delete")]
        public IActionResult ProfileDelete(long profileId)
        {
            var profile = _context.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return NotFound();
            if (profile.IsAdmin)
            {
                TempData["Error"] = "Администратора удалить нельзя.";
                return RedirectToAction("Profiles");
            }

            // Keep the data consistent: free the patient's future slots and drop their reviews and staff card.
            var now = DateTime.Now;
            foreach (var a in _context.Appointments.Include(a => a.Services).Where(a => a.ClientId == profileId && a.StartAt > now))
            {
                a.ClientId = 0;
                a.Services.Clear();
            }
            _context.Reviews.RemoveRange(_context.Reviews.Where(r => r.ProfileId == profileId));

            var staff = _context.Staffs.FirstOrDefault(s => s.Profile.Id == profileId);
            if (staff != null)
            {
                _context.Appointments.RemoveRange(_context.Appointments.Where(a => a.StaffId == staff.Id));
                _context.Staffs.Remove(staff);
            }

            _context.Profiles.Remove(profile);
            _context.SaveChanges();

            TempData["Success"] = "Профиль удалён.";
            return RedirectToAction("Profiles");
        }

        [HttpPost("profiles/{profileId:long}/ban")]
        public IActionResult Ban(long profileId) => SetBan(profileId, true);

        [HttpPost("profiles/{profileId:long}/unban")]
        public IActionResult Unban(long profileId) => SetBan(profileId, false);

        private IActionResult SetBan(long profileId, bool banned)
        {
            var user = _context.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (user == null) return NotFound();
            if (user.IsAdmin) return Forbid();

            user.EmailConfirmed = !banned;
            _context.SaveChanges();

            TempData["Success"] = banned ? "Аккаунт заблокирован." : "Аккаунт разблокирован.";
            return RedirectToAction("Profiles");
        }

        // ---------------------------------------------------------------- reviews

        [HttpGet("reviews")]
        public IActionResult Reviews()
        {
            var reviews = _context.Reviews.OrderByDescending(r => r.CreatedAt).ToList();
            return View("Reviews/Index", ToCards(reviews));
        }

        [HttpGet("reviews/{reviewId:long}")]
        public IActionResult Review(long reviewId)
        {
            var review = _context.Reviews.FirstOrDefault(r => r.Id == reviewId);
            if (review == null) return NotFound();

            return View("Reviews/Edit", review);
        }

        [HttpPost("reviews/{reviewId:long}")]
        public IActionResult ReviewUpdate(long reviewId, Review updated)
        {
            var review = _context.Reviews.FirstOrDefault(r => r.Id == reviewId);
            if (review == null) return NotFound();

            if (!ModelState.IsValid)
            {
                updated.Id = reviewId;
                return View("Reviews/Edit", updated);
            }

            review.Text = updated.Text;
            _context.SaveChanges();

            TempData["Success"] = "Отзыв обновлён.";
            return RedirectToAction("Reviews");
        }

        [HttpPost("reviews/{reviewId:long}/delete")]
        public IActionResult ReviewDelete(long reviewId)
        {
            var review = _context.Reviews.FirstOrDefault(r => r.Id == reviewId);
            if (review != null)
            {
                _context.Reviews.Remove(review);
                _context.SaveChanges();
                TempData["Success"] = "Отзыв удалён.";
            }

            return RedirectToAction("Reviews");
        }

        [HttpPost("reviews/{reviewId:long}/show")]
        public IActionResult ShowReview(long reviewId) => SetVisibility(reviewId, true);

        [HttpPost("reviews/{reviewId:long}/hide")]
        public IActionResult HideReview(long reviewId) => SetVisibility(reviewId, false);

        private IActionResult SetVisibility(long reviewId, bool visible)
        {
            var review = _context.Reviews.FirstOrDefault(r => r.Id == reviewId);
            if (review == null) return NotFound();

            review.IsVisible = visible;
            _context.SaveChanges();
            return RedirectToAction("Reviews");
        }
    }
}
