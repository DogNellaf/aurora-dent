using DentalClinic.Models;
using DentalClinic.Models.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services
{
    public enum UpdateProfileStatus { Updated, NotFound, EmailTaken, Invalid }

    public enum ProfileChangeStatus { Done, NotFound, IsAdmin, HasVisits }

    public record UpdateProfileResult(UpdateProfileStatus Status, IReadOnlyList<IdentityError> Errors);

    public interface IProfileService
    {
        Task<(IdentityResult Result, Profile? Profile)> CreateAsync(NewProfile model);
        Task<UpdateProfileResult> UpdateAsync(long id, ProfileEditModel model);
        Task<ProfileChangeStatus> SetBannedAsync(long id, bool banned);
        Task<ProfileChangeStatus> DeleteAsync(long id);
    }

    public sealed class ProfileService : IProfileService
    {
        private readonly DatabaseContext _db;
        private readonly UserManager<Profile> _users;
        private readonly IClinicClock _clock;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(DatabaseContext db, UserManager<Profile> users, IClinicClock clock, ILogger<ProfileService> logger)
        {
            _db = db;
            _users = users;
            _clock = clock;
            _logger = logger;
        }

        public async Task<(IdentityResult, Profile?)> CreateAsync(NewProfile model)
        {
            var roleId = model.RoleTitle switch
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

            var result = await _users.CreateAsync(profile, model.Password);
            if (!result.Succeeded) return (result, null);

            // A doctor needs a staff card, otherwise there is nobody to show in the schedule or the cabinet.
            if (roleId == RoleIds.Doctor)
            {
                _db.Staffs.Add(new Staff
                {
                    ProfileId = profile.Id,
                    ExternalLogin = model.Email,
                    FullName = profile.FullName,
                    Specialty = "Стоматолог",
                    Bio = "Информация о враче скоро появится."
                });
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Profile {ProfileId} created with role {RoleId}", profile.Id, roleId);
            return (result, profile);
        }

        public async Task<UpdateProfileResult> UpdateAsync(long id, ProfileEditModel model)
        {
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile is null) return new UpdateProfileResult(UpdateProfileStatus.NotFound, Array.Empty<IdentityError>());

            var normalized = _users.NormalizeEmail(model.Email);
            if (await _db.Profiles.AnyAsync(p => p.Id != id && p.NormalizedEmail == normalized))
                return new UpdateProfileResult(UpdateProfileStatus.EmailTaken, Array.Empty<IdentityError>());

            profile.FullName = model.FullName.Trim();
            profile.PhoneNumber = model.Phone;

            // The email is also the login. The fields are set directly and validated and saved once by UpdateAsync,
            // because SetEmailAsync and SetUserNameAsync save one by one and could leave a half applied change.
            // SetEmailAsync would also mark the address as unconfirmed. Banning has its own IsBanned column.
            profile.Email = model.Email;
            profile.UserName = model.Email;
            profile.EmailConfirmed = true;
            await _users.UpdateNormalizedEmailAsync(profile);
            await _users.UpdateNormalizedUserNameAsync(profile);

            var staff = await _db.Staffs.FirstOrDefaultAsync(s => s.ProfileId == id);
            if (staff != null)
            {
                staff.FullName = profile.FullName;
                staff.ExternalLogin = model.Email;
            }

            var result = await _users.UpdateAsync(profile);
            if (!result.Succeeded)
            {
                // Nothing was saved. Forget the tracked changes so the failed edit cannot leak into later work.
                foreach (var entry in _db.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;
                return new UpdateProfileResult(UpdateProfileStatus.Invalid, result.Errors.ToList());
            }

            return new UpdateProfileResult(UpdateProfileStatus.Updated, Array.Empty<IdentityError>());
        }

        public async Task<ProfileChangeStatus> SetBannedAsync(long id, bool banned)
        {
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile is null) return ProfileChangeStatus.NotFound;
            if (profile.IsAdmin) return ProfileChangeStatus.IsAdmin;

            profile.IsBanned = banned;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Profile {ProfileId} {Action}", id, banned ? "banned" : "unbanned");
            return ProfileChangeStatus.Done;
        }

        public async Task<ProfileChangeStatus> DeleteAsync(long id)
        {
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile is null) return ProfileChangeStatus.NotFound;
            if (profile.IsAdmin) return ProfileChangeStatus.IsAdmin;

            // Deleting a doctor would silently delete the visits of other patients through the foreign keys.
            // A doctor who has patients is banned instead, and a doctor without patients can be deleted.
            var staff = await _db.Staffs.FirstOrDefaultAsync(s => s.ProfileId == id);
            if (staff != null && await _db.Appointments.AnyAsync(a => a.StaffId == staff.Id && a.ClientId != null))
                return ProfileChangeStatus.HasVisits;

            // Future visits return to the schedule, finished visits stay in the doctors' history without a patient.
            var future = await _db.Appointments.Include(a => a.Services)
                .Where(a => a.ClientId == id && a.StartAt > _clock.Now).ToListAsync();
            foreach (var appointment in future)
                appointment.Release();
            await _db.SaveChangesAsync();

            await _db.Appointments.Where(a => a.ClientId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClientId, (long?)null));

            // The staff card and the whole schedule of a deleted doctor go away through the foreign keys.
            _db.Profiles.Remove(profile);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Profile {ProfileId} deleted", id);
            return ProfileChangeStatus.Done;
        }
    }
}
