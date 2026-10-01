using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models
{
    [Table("Service")]
    public class Service
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public double Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Icon { get; set; } = "tooth";
        public int DurationMinutes { get; set; } = 60;
        public List<Staff> Staff { get; set; } = new();
        public List<Appointment> Appointments { get; set; } = new();
    }
}
