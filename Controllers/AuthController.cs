using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.DTO;

namespace DentalClinic.Controllers
{
    [Route("route")]
    public class AuthController : BaseController
    {
        private readonly UserManager<Profile> _userManager;
        private readonly SignInManager<Profile> _signInManager;

        public AuthController(
            DatabaseContext context,
            UserManager<Profile> userManager,
            SignInManager<Profile> signInManager) : base(context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        /// <summary>Sends the user to the cabinet that matches their role.</summary>
        [HttpGet("")]
        public IActionResult Index()
        {
            var profile = TryGetProfile();
            if (profile == null) return RedirectToAction("LoginPage");

            if (profile.IsAdmin) return RedirectToAction("Overview", "Admin");
            if (profile.IsManager) return RedirectToAction("HiddenReviews", "Manager");
            if (profile.IsDoctor) return RedirectToAction("Index", "Doctor");
            return RedirectToAction("Index", "Client");
        }

        [HttpGet("register")]
        public IActionResult Registration() => View();

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Registration", model);

            var profile = new Profile
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                PhoneNumber = model.Phone,
                FullName = model.FullName.Trim(),
                RoleId = RoleIds.Client
            };

            var registerResult = await _userManager.CreateAsync(profile, model.Password);
            if (registerResult.Succeeded)
            {
                await _signInManager.SignInAsync(profile, isPersistent: true);
                return RedirectToAction("Index");
            }

            foreach (var error in registerResult.Errors)
                ModelState.AddModelError("", IdentityErrors.Translate(error));

            return View("Registration", model);
        }

        [HttpGet("login")]
        public IActionResult LoginPage(string? returnUrl = null) =>
            View("Login", new LoginViewModel { ReturnUrl = returnUrl });

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginViewModel data)
        {
            if (!ModelState.IsValid)
                return View("Login", data);

            var profile = await _userManager.FindByNameAsync(data.Email);
            if (profile != null && profile.IsBanned)
            {
                ModelState.AddModelError("", "Аккаунт заблокирован. Свяжитесь с клиникой по телефону.");
                return View("Login", data);
            }

            var result = await _signInManager.PasswordSignInAsync(data.Email, data.Password, data.RememberMe, false);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(data.ReturnUrl) && Url.IsLocalUrl(data.ReturnUrl))
                    return LocalRedirect(data.ReturnUrl);
                return RedirectToAction("Index");
            }

            ModelState.AddModelError("", "Неверный email или пароль");
            return View("Login", data);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("denied")]
        public IActionResult Denied()
        {
            Response.StatusCode = 403;
            return View();
        }
    }
}
