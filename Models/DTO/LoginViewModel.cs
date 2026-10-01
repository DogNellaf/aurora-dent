using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models.DTO
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Введите пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        public bool RememberMe { get; set; } = true;

        public string? ReturnUrl { get; set; }
    }
}
