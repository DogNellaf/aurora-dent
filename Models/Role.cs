using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models
{
    [Table("Role")]
    public class Role : IdentityRole<long>
    {
        [Key]
        public override long Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }
}
