using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models
{
    [Table("Appointment")]
    public class Appointment
    {
        public long Id { get; set; }
        public long StaffId { get; set; }
        public Staff? Staff { get; set; }

        /// <summary>Profile id of the patient; 0 means the slot is still free.</summary>
        public long ClientId { get; set; }
        public DateTime StartAt { get; set; }

        [Range(1, 480, ErrorMessage = "Длительность — от 1 до 480 минут")]
        public short Duration { get; set; } = 60;
        public string Recommendation { get; set; } = string.Empty;
        public string DurationChangeReason { get; set; } = string.Empty;
        public List<Service> Services { get; set; } = new();

        [NotMapped]
        public bool IsBooked => ClientId != 0;

        [NotMapped]
        public DateTime EndAt => StartAt.AddMinutes(Duration);

        [NotMapped]
        public bool IsPast => EndAt < DateTime.Now;

        [NotMapped]
        public bool IsActive => StartAt <= DateTime.Now && DateTime.Now <= EndAt;
    }
}
