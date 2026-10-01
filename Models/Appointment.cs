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

        /// <summary>The patient's profile id, or null while the slot is still free.</summary>
        public long? ClientId { get; set; }
        public Profile? Client { get; set; }

        /// <summary>Start of the visit in the clinic's local time (see <c>IClinicClock</c>).</summary>
        public DateTime StartAt { get; set; }

        [Range(1, 480, ErrorMessage = "Длительность от 1 до 480 минут")]
        public short Duration { get; set; } = 60;
        public string Recommendation { get; set; } = string.Empty;
        public string DurationChangeReason { get; set; } = string.Empty;
        public List<Service> Services { get; set; } = new();

        [NotMapped]
        public bool IsBooked => ClientId.HasValue;

        [NotMapped]
        public DateTime EndAt => StartAt.AddMinutes(Duration);

        public bool IsPastAt(DateTime now) => EndAt < now;

        public bool IsActiveAt(DateTime now) => StartAt <= now && now <= EndAt;
    }
}
