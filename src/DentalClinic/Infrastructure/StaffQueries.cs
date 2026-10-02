using DentalClinic.Models;

namespace DentalClinic.Infrastructure
{
    public static class StaffQueries
    {
        /// <summary>Doctors whose accounts are not banned. Banned doctors disappear from the site and cannot be booked.</summary>
        public static IQueryable<Staff> Active(this IQueryable<Staff> staff) => staff.Where(s => !s.Profile.IsBanned);
    }
}
