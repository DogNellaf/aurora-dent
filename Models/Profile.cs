using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models
{
    [Table("Profile")]
    public class Profile : IdentityUser<long>
    {
        [Key]
        public override long Id { get; set; }
        public long RoleId { get; set; }
        public string FullName { get; set; } = string.Empty;

        public bool IsClient => RoleId == RoleIds.Client;
        public bool IsAdmin => RoleId == RoleIds.Admin;
        public bool IsManager => RoleId == RoleIds.Manager;
        public bool IsDoctor => RoleId == RoleIds.Doctor;

        /// <summary>Banned accounts keep their data but cannot sign in.</summary>
        public bool IsBanned => !EmailConfirmed;

        public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? (UserName ?? string.Empty) : FullName;

        /// <summary>"Анна К." — used for public reviews so patients are not fully identified.</summary>
        public string PublicName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName)) return "Пациент клиники";
                var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 1 ? $"{parts[0]} {parts[1][0]}." : parts[0];
            }
        }
    }

    public static class RoleIds
    {
        public const long Client = 1;
        public const long Admin = 2;
        public const long Manager = 3;
        public const long Doctor = 4;
    }
}
