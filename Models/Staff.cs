using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models
{
    [Table("Staff")]
    public class Staff
    {
        public long Id { get; set; }
        public Profile Profile { get; set; } = null!;
        public string ExternalLogin { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public int ExperienceYears { get; set; }
        public List<Service> Services { get; set; } = new();

        public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? ExternalLogin : FullName;
    }
}
