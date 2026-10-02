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

        /// <summary>Banned accounts keep their data but cannot sign in. Stored separately from Identity's EmailConfirmed.</summary>
        public bool IsBanned { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? (UserName ?? string.Empty) : FullName;

        /// <summary>"Анна К." for public reviews, so patients are not fully identified. The names are stored as typed, a translator may turn them into the language of the reader.</summary>
        public string PublicName(Func<string, string>? translate = null)
        {
            if (string.IsNullOrWhiteSpace(FullName)) return translate?.Invoke("Пациент клиники") ?? "Пациент клиники";
            var parts = (translate?.Invoke(FullName) ?? FullName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? $"{parts[0]} {parts[1][0]}." : parts[0];
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
