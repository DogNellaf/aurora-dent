using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models
{
    [Table("Review")]
    public class Review
    {
        public long Id { get; set; }
        public long ProfileId { get; set; }

        [Required(ErrorMessage = "Напишите несколько слов об опыте посещения")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Отзыв должен содержать от 10 до 1000 символов")]
        public string Text { get; set; } = string.Empty;

        public bool IsVisible { get; set; }
        public int Rating { get; set; } = 5;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
