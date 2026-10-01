using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models.DTO
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Укажите имя")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "От 2 до 80 символов")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Некорректный номер телефона")]
        public string Phone { get; set; } = null!;

        [Required(ErrorMessage = "Придумайте пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string? ConfirmPassword { get; set; }
    }
}
