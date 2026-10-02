using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using DentalClinic.Infrastructure;
using DentalClinic.Localization;
using DentalClinic.Models;
using DentalClinic.Models.ViewModels;
using DentalClinic.Services;

namespace DentalClinic.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IScheduleService _schedule;
        private readonly IBookingService _booking;

        public HomeController(DatabaseContext context, IScheduleService schedule, IBookingService booking) : base(context)
        {
            _schedule = schedule;
            _booking = booking;
        }

        /// <summary>Remembers the language in a cookie and returns to the page the visitor came from.</summary>
        [HttpPost("language")]
        public IActionResult SetLanguage(string language, string? returnUrl)
        {
            var chosen = Languages.Find(language);
            Response.Cookies.Append(Languages.CookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(chosen.Culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var visible = _context.Reviews.Where(r => r.IsVisible);
            var model = new HomeViewModel
            {
                Reviews = await ToCardsAsync(await visible.OrderByDescending(r => r.CreatedAt).Take(6).ToListAsync()),
                Services = await _context.Services.OrderBy(s => s.Id).Take(6).ToListAsync(),
                Doctors = await _context.Staffs.Active().OrderBy(s => s.Id).ToListAsync(),
                PatientsCount = await _context.Profiles.CountAsync(p => p.RoleId == RoleIds.Client),
                AverageRating = await visible.AnyAsync() ? Math.Round(await visible.AverageAsync(r => r.Rating), 1) : 5
            };
            return View(model);
        }

        [HttpGet("faq")]
        public IActionResult FAQ() => View();

        [HttpGet("about")]
        public async Task<IActionResult> About() => View(await _context.Staffs.Active().OrderBy(s => s.Id).ToListAsync());

        [HttpGet("contacts")]
        public IActionResult Contacts() => View();

        [HttpGet("doctors")]
        public async Task<IActionResult> Doctors() =>
            View(await _context.Staffs.Active().Include(s => s.Services).OrderBy(s => s.Id).ToListAsync());

        [HttpGet("services")]
        public async Task<IActionResult> Services() => View(await _context.Services.OrderBy(s => s.Id).ToListAsync());

        [HttpGet("services/{serviceId:long}")]
        public async Task<IActionResult> ServiceDetail(long serviceId)
        {
            var service = await _context.Services.Include(s => s.Staff.Where(x => !x.Profile.IsBanned)).FirstOrDefaultAsync(s => s.Id == serviceId);
            if (service == null) return NotFound();

            return View(new ServiceDetailViewModel
            {
                Service = service,
                Doctors = service.Staff.OrderBy(s => s.Id).ToList(),
                Related = await _context.Services.Where(s => s.Id != serviceId && s.Category == service.Category).OrderBy(s => s.Id).Take(3).ToListAsync()
            });
        }

        /// <summary>
        /// Step-by-step booking: pick a service (optional), then a doctor, then a free time.
        /// Browsing is public, booking itself requires a patient account.
        /// </summary>
        [HttpGet("schedule")]
        public async Task<IActionResult> Schedule(long? serviceId, long? staffId) =>
            View(await _schedule.BrowseAsync(serviceId, staffId, await TryGetProfileAsync()));

        [HttpPost("appointments/book")]
        public async Task<IActionResult> Book(long appointmentId, long? serviceId)
        {
            var profile = await TryGetProfileAsync();
            if (profile == null)
                return RedirectToAction("LoginPage", "Auth", new { returnUrl = Url.Action("Schedule", "Home", new { serviceId }) });

            if (!profile.IsClient)
            {
                TempData["Error"] = T["Записаться на приём можно только с аккаунта пациента."].Value;
                return RedirectToAction("Schedule", new { serviceId });
            }

            var result = await _booking.BookAsync(profile, appointmentId, serviceId);
            switch (result.Status)
            {
                case BookingStatus.Booked:
                    TempData["Success"] = T["Вы записаны на приём. Подтверждение отправлено на почту."].Value;
                    return RedirectToAction("Index", "Client");
                case BookingStatus.NotFound:
                    return NotFound();
                case BookingStatus.InThePast:
                    TempData["Error"] = T["Это время уже прошло. Выберите другое."].Value;
                    break;
                default:
                    TempData["Error"] = T["Это время только что заняли. Пожалуйста, выберите другое."].Value;
                    break;
            }

            var staffId = await _context.Appointments.Where(a => a.Id == appointmentId).Select(a => (long?)a.StaffId).FirstOrDefaultAsync();
            return RedirectToAction("Schedule", new { serviceId, staffId });
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
