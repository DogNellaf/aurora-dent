using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models
{
    [Table("Client")]
    public class Client
    {
        public long Id { get; set; }
        public Profile Profile { get; set; } = null!;
        public string Phone { get; set; } = string.Empty;
    }
}
