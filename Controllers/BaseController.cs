using Microsoft.AspNetCore.Mvc;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.ViewModels;

namespace DentalClinic.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly DatabaseContext _context;

        protected BaseController(DatabaseContext context)
        {
            _context = context;
        }

        /// <summary>Profile of the signed-in user (cached by <see cref="RoleRequiredAttribute"/> when present).</summary>
        protected Profile GetProfile()
        {
            if (HttpContext.Items[RoleRequiredAttribute.ProfileItemKey] is Profile cached)
                return cached;

            return _context.Profiles.First(p => p.UserName == User.Identity!.Name);
        }

        protected Profile? TryGetProfile()
        {
            if (User.Identity?.IsAuthenticated != true) return null;
            return _context.Profiles.FirstOrDefault(p => p.UserName == User.Identity!.Name);
        }

        /// <summary>Wraps appointments with patient names so views do not need extra queries.</summary>
        protected List<AppointmentRow> ToRows(IEnumerable<Appointment> appointments)
        {
            var list = appointments.ToList();
            var ids = list.Where(a => a.IsBooked).Select(a => a.ClientId).Distinct().ToList();
            var names = _context.Profiles
                .Where(p => ids.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.DisplayName);

            return list.Select(a => new AppointmentRow
            {
                Appointment = a,
                ClientName = a.IsBooked ? names.GetValueOrDefault(a.ClientId, $"Пациент #{a.ClientId}") : string.Empty
            }).ToList();
        }

        protected List<ReviewCard> ToCards(IEnumerable<Review> reviews)
        {
            var list = reviews.ToList();
            var ids = list.Select(r => r.ProfileId).Distinct().ToList();
            var profiles = _context.Profiles.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);

            return list.Select(r => new ReviewCard(
                r.Id,
                profiles.TryGetValue(r.ProfileId, out var p) ? p.PublicName : "Пациент клиники",
                r.Text, r.Rating, r.CreatedAt, r.IsVisible, r.ProfileId)).ToList();
        }
    }
}
