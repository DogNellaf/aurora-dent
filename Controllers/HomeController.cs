using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using DentalClinic.Models;
using DentalClinic.Models.ViewModels;

namespace DentalClinic.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController(DatabaseContext context) : base(context) { }

        [HttpGet("")]
        public IActionResult Index()
        {
            var visible = _context.Reviews.Where(r => r.IsVisible);
            var model = new HomeViewModel
            {
                Reviews = ToCards(visible.OrderByDescending(r => r.CreatedAt).Take(6).ToList()),
                Services = _context.Services.OrderBy(s => s.Id).Take(6).ToList(),
                Doctors = _context.Staffs.OrderBy(s => s.Id).ToList(),
                PatientsCount = _context.Profiles.Count(p => p.RoleId == RoleIds.Client),
                AverageRating = visible.Any() ? Math.Round(visible.Average(r => r.Rating), 1) : 5
            };
            return View(model);
        }

        [HttpGet("faq")]
        public IActionResult FAQ() => View();

        [HttpGet("about")]
        public IActionResult About() => View(_context.Staffs.OrderBy(s => s.Id).ToList());

        [HttpGet("contacts")]
        public IActionResult Contacts() => View();

        [HttpGet("doctors")]
        public IActionResult Doctors()
        {
            var doctors = _context.Staffs.Include(s => s.Services).OrderBy(s => s.Id).ToList();
            return View(doctors);
        }

        [HttpGet("services")]
        public IActionResult Services()
        {
            var services = _context.Services.OrderBy(s => s.Id).ToList();
            return View(services);
        }

        [HttpGet("services/{serviceId:long}")]
        public IActionResult ServiceDetail(long serviceId)
        {
            var service = _context.Services.Include(s => s.Staff).FirstOrDefault(s => s.Id == serviceId);
            if (service == null) return NotFound();

            return View(new ServiceDetailViewModel
            {
                Service = service,
                Doctors = service.Staff.OrderBy(s => s.Id).ToList(),
                Related = _context.Services.Where(s => s.Id != serviceId && s.Category == service.Category).Take(3).ToList()
            });
        }

        /// <summary>
        /// Step-by-step booking: pick a service (optional) → pick a doctor → pick a free time.
        /// Browsing is public; booking itself requires a patient account.
        /// </summary>
        [HttpGet("schedule")]
        public IActionResult Schedule(long? serviceId, long? staffId)
        {
            var now = DateTime.Now;
            var profile = TryGetProfile();

            var model = new ScheduleViewModel
            {
                Services = _context.Services.OrderBy(s => s.Category).ThenBy(s => s.Title).ToList(),
                ServiceId = serviceId,
                IsAuthenticated = profile != null,
                CanBook = profile?.IsClient == true
            };

            model.SelectedService = serviceId == null ? null : model.Services.FirstOrDefault(s => s.Id == serviceId);

            var doctorsQuery = _context.Staffs.Include(s => s.Services).AsQueryable();
            if (model.SelectedService != null)
                doctorsQuery = doctorsQuery.Where(s => s.Services.Any(x => x.Id == model.SelectedService.Id));
            var doctors = doctorsQuery.OrderBy(s => s.Id).ToList();

            var ids = doctors.Select(d => d.Id).ToList();
            var free = _context.Appointments
                .Where(a => ids.Contains(a.StaffId) && a.ClientId == 0 && a.StartAt > now)
                .OrderBy(a => a.StartAt)
                .ToList();

            model.Doctors = doctors.Select(d =>
            {
                var slots = free.Where(a => a.StaffId == d.Id).ToList();
                return new DoctorSlots { Doctor = d, NextSlot = slots.FirstOrDefault(), FreeCount = slots.Count };
            }).ToList();

            if (staffId != null)
            {
                model.SelectedDoctor = doctors.FirstOrDefault(d => d.Id == staffId);
                if (model.SelectedDoctor != null)
                    model.Days = free.Where(a => a.StaffId == staffId).GroupBy(a => a.StartAt.Date).ToList();
            }

            return View(model);
        }

        [HttpPost("appointments/book")]
        public IActionResult Book(long appointmentId, long? serviceId)
        {
            var profile = TryGetProfile();
            if (profile == null)
                return RedirectToAction("LoginPage", "Auth", new { returnUrl = Url.Action("Schedule", "Home", new { serviceId }) });

            if (!profile.IsClient)
            {
                TempData["Error"] = "Записаться на приём можно только с аккаунта пациента.";
                return RedirectToAction("Schedule", new { serviceId });
            }

            var appointment = _context.Appointments.AsNoTracking().FirstOrDefault(a => a.Id == appointmentId);
            if (appointment == null) return NotFound();

            if (appointment.StartAt <= DateTime.Now)
            {
                TempData["Error"] = "Это время уже прошло. Выберите другое.";
                return RedirectToAction("Schedule", new { serviceId, staffId = appointment.StaffId });
            }

            // Atomic claim: only one patient can win a slot even if two click at the same moment.
            var claimed = _context.Appointments
                .Where(a => a.Id == appointmentId && a.ClientId == 0)
                .ExecuteUpdate(s => s.SetProperty(a => a.ClientId, profile.Id));

            if (claimed == 0)
            {
                TempData["Error"] = "Это время только что заняли. Пожалуйста, выберите другое.";
                return RedirectToAction("Schedule", new { serviceId, staffId = appointment.StaffId });
            }

            if (serviceId != null)
            {
                var service = _context.Services.FirstOrDefault(s => s.Id == serviceId);
                var booked = _context.Appointments.Include(a => a.Services).First(a => a.Id == appointmentId);
                if (service != null)
                {
                    booked.Services.Add(service);
                    _context.SaveChanges();
                }
            }

            TempData["Success"] = "Вы записаны на приём. Ждём вас в клинике!";
            return RedirectToAction("Index", "Client");
        }

        [Route("Home/Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? code)
        {
            var status = code ?? HttpContext.Response.StatusCode;
            if (status < 400) status = 500;
            Response.StatusCode = status;
            ViewData["Status"] = status;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
