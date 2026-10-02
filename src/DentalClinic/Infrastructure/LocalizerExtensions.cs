using Microsoft.Extensions.Localization;
using DentalClinic.Models;

namespace DentalClinic.Infrastructure
{
    public static class LocalizerExtensions
    {
        /// <summary>Titles of the services of a visit, or a generic label for a visit without services.</summary>
        public static string ServiceList(this IStringLocalizer localizer, IEnumerable<Service> services, string? empty = null)
        {
            var titles = services.Select(s => localizer[s.Title].Value).ToList();
            return titles.Count > 0 ? string.Join(", ", titles) : empty ?? localizer["Приём врача"].Value;
        }
    }
}
