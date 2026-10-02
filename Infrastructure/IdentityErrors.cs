using Microsoft.AspNetCore.Identity;
using DentalClinic.Localization;

namespace DentalClinic.Infrastructure
{
    public static class IdentityErrors
    {
        /// <summary>Translated text for the Identity errors a user can realistically trigger.</summary>
        public static string Translate(IdentityError error) => error.Code switch
        {
            "DuplicateUserName" or "DuplicateEmail" => Translations.Get("Пользователь с таким email уже зарегистрирован"),
            "PasswordTooShort" => Translations.Get("Пароль слишком короткий (минимум 6 символов)"),
            "PasswordRequiresDigit" => Translations.Get("Пароль должен содержать хотя бы одну цифру"),
            "PasswordRequiresLower" => Translations.Get("Пароль должен содержать строчную букву"),
            "InvalidEmail" or "InvalidUserName" => Translations.Get("Некорректный email"),
            _ => error.Description
        };
    }
}
