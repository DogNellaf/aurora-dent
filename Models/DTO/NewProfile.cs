using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models.DTO
{
    public class NewProfile
    {
        [Required(ErrorMessage = "Укажите имя")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Укажите пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Некорректный номер телефона")]
        public string Phone { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(RoleTitle))]
        public RoleTitle RoleTitle { get; set; }
    }
}
