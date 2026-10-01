using Microsoft.AspNetCore.Identity;

namespace DentalClinic.Infrastructure
{
    public static class IdentityErrors
    {
        /// <summary>Russian text for the Identity errors a user can realistically trigger.</summary>
        public static string Translate(IdentityError error) => error.Code switch
        {
            "DuplicateUserName" or "DuplicateEmail" => "Пользователь с таким email уже зарегистрирован",
            "PasswordTooShort" => "Пароль слишком короткий (минимум 6 символов)",
            "PasswordRequiresDigit" => "Пароль должен содержать хотя бы одну цифру",
            "PasswordRequiresLower" => "Пароль должен содержать строчную букву",
            "InvalidEmail" or "InvalidUserName" => "Некорректный email",
            _ => error.Description
        };
    }
}
