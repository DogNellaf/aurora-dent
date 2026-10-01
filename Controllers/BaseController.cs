using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        /// <summary>Profile of the signed-in user, cached for the request by <see cref="RoleRequiredAttribute"/>.</summary>
        protected Profile GetProfile()
        {
            if (HttpContext.Items[RoleRequiredAttribute.ProfileItemKey] is Profile cached)
                return cached;

            return _context.Profiles.First(p => p.UserName == User.Identity!.Name);
        }

        protected async Task<Profile?> TryGetProfileAsync()
        {
            if (User.Identity?.IsAuthenticated != true) return null;
            var name = User.Identity!.Name;
            var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserName == name);

            // A banned account with a still valid cookie is treated as signed out on the public pages too.
            return profile is { IsBanned: true } ? null : profile;
        }

        /// <summary>Wraps appointments with the patient's name. The queries must include <c>Client</c>.</summary>
        protected static List<AppointmentRow> ToRows(IEnumerable<Appointment> appointments) =>
            appointments.Select(a => new AppointmentRow
            {
                Appointment = a,
                ClientName = a.ClientId is null ? string.Empty : a.Client?.DisplayName ?? $"Пациент #{a.ClientId}"
            }).ToList();

        protected async Task<List<ReviewCard>> ToCardsAsync(IEnumerable<Review> reviews)
        {
            var list = reviews.ToList();
            var ids = list.Select(r => r.ProfileId).Distinct().ToList();
            var profiles = await _context.Profiles.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            return list.Select(r => new ReviewCard(
                r.Id,
                profiles.TryGetValue(r.ProfileId, out var p) ? p.PublicName : "Пациент клиники",
                r.Text, r.Rating, r.CreatedAt, r.IsVisible, r.ProfileId)).ToList();
        }
    }
}
