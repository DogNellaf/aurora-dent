using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.DTO;
using DentalClinic.Models.ViewModels;

namespace DentalClinic.Controllers
{
    /// <summary>Own profile and password, available to every signed-in role.</summary>
    [Route("account")]
    [RoleRequired]
    public class AccountController : BaseController
    {
        private readonly UserManager<Profile> _userManager;
        private readonly SignInManager<Profile> _signInManager;

        public AccountController(DatabaseContext context, UserManager<Profile> userManager, SignInManager<Profile> signInManager) : base(context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        private IActionResult Page(AccountViewModel? model = null)
        {
            var profile = GetProfile();
            model ??= new AccountViewModel();
            model.Email = profile.Email ?? string.Empty;
            model.RoleName = Fmt.RoleName(profile);
            if (string.IsNullOrEmpty(model.Profile.FullName))
            {
                model.Profile.FullName = profile.FullName;
                model.Profile.Phone = profile.PhoneNumber ?? string.Empty;
            }

            ViewData["Cabinet"] = profile.IsAdmin ? "admin" : profile.IsManager ? "manager" : profile.IsDoctor ? "doctor" : "client";
            return View("Index", model);
        }

        [HttpGet("")]
        public IActionResult Index() => Page();

        [HttpPost("profile")]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] AccountProfileModel model)
        {
            if (!ModelState.IsValid)
                return Page(new AccountViewModel { Profile = model });

            var profile = await _context.Profiles.FirstAsync(p => p.Id == GetProfile().Id);
            profile.FullName = model.FullName.Trim();
            profile.PhoneNumber = model.Phone;

            var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.ProfileId == profile.Id);
            if (staff != null) staff.FullName = profile.FullName;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Профиль сохранён.";
            return RedirectToAction("Index");
        }

        [HttpPost("password")]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordModel model)
        {
            if (!ModelState.IsValid)
                return Page(new AccountViewModel { Password = model });

            var profile = await _userManager.FindByIdAsync(GetProfile().Id.ToString());
            var result = await _userManager.ChangePasswordAsync(profile!, model.Current, model.New);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("Password." + (error.Code == "PasswordMismatch" ? nameof(model.Current) : nameof(model.New)),
                        error.Code == "PasswordMismatch" ? "Текущий пароль указан неверно" : IdentityErrors.Translate(error));
                return Page(new AccountViewModel { Password = new ChangePasswordModel() });
            }

            // The security stamp changed, so keep the current session signed in.
            await _signInManager.RefreshSignInAsync(profile!);
            TempData["Success"] = "Пароль изменён.";
            return RedirectToAction("Index");
        }
    }
}
