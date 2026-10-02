using DentalClinic.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Data
{
    /// <summary>
    /// Creates the first administrator of a real deployment from configuration (Bootstrap:AdminEmail and
    /// Bootstrap:AdminPassword), when the demo data is off and no administrator exists yet.
    /// </summary>
    public class AdminBootstrapper
    {
        private readonly DatabaseContext _db;
        private readonly UserManager<Profile> _users;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminBootstrapper> _logger;

        public AdminBootstrapper(DatabaseContext db, UserManager<Profile> users, IConfiguration configuration, ILogger<AdminBootstrapper> logger)
        {
            _db = db;
            _users = users;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var email = _configuration["Bootstrap:AdminEmail"];
            var password = _configuration["Bootstrap:AdminPassword"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
            if (await _db.Profiles.AnyAsync(p => p.RoleId == RoleIds.Admin)) return;

            var admin = new Profile
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                RoleId = RoleIds.Admin,
                FullName = _configuration["Bootstrap:AdminName"] ?? "Администратор"
            };

            var result = await _users.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Cannot create the first administrator: " + string.Join(", ", result.Errors.Select(e => e.Description)));

            _logger.LogInformation("The first administrator {Email} was created from the Bootstrap configuration", email);
        }
    }
}
