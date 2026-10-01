using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models.DTO
{
    public class ProfileEditModel
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "Укажите имя")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Некорректный номер телефона")]
        public string? Phone { get; set; }

        public long RoleId { get; set; }
        public bool IsBanned { get; set; }
    }
}
