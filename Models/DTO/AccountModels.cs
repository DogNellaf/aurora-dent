using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models.DTO
{
    public class AccountProfileModel
    {
        [Required(ErrorMessage = "Укажите имя")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "От 2 до 80 символов")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Некорректный номер телефона")]
        public string Phone { get; set; } = string.Empty;
    }

    public class ChangePasswordModel
    {
        [Required(ErrorMessage = "Введите текущий пароль")]
        [DataType(DataType.Password)]
        public string Current { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите новый пароль")]
        [DataType(DataType.Password)]
        public string New { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(New), ErrorMessage = "Пароли не совпадают")]
        public string? Confirm { get; set; }
    }

    public class ForgotPasswordModel
    {
        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordModel
    {
        [Required] public string Email { get; set; } = string.Empty;
        [Required] public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите новый пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
        public string? ConfirmPassword { get; set; }
    }
}
