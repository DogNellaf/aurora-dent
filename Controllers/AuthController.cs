using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Models.DTO;
using DentalClinic.Services;

namespace DentalClinic.Controllers
{
    [Route("route")]
    public class AuthController : BaseController
    {
        private readonly UserManager<Profile> _userManager;
        private readonly SignInManager<Profile> _signInManager;
        private readonly INotifier _notifier;
        private readonly ClinicOptions _clinic;
        private readonly bool _demoData;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            DatabaseContext context,
            UserManager<Profile> userManager,
            SignInManager<Profile> signInManager,
            INotifier notifier,
            IOptions<ClinicOptions> clinic,
            IConfiguration configuration,
            ILogger<AuthController> logger) : base(context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _notifier = notifier;
            _clinic = clinic.Value;
            _demoData = configuration.GetValue("Seed:DemoData", false);
            _logger = logger;
        }

        /// <summary>Sends the user to the cabinet that matches the role.</summary>
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var profile = await TryGetProfileAsync();
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
                _logger.LogInformation("Patient {ProfileId} registered", profile.Id);
                await _signInManager.SignInAsync(profile, isPersistent: true);
                return RedirectToAction("Index");
            }

            foreach (var error in registerResult.Errors)
                ModelState.AddModelError("", IdentityErrors.Translate(error));

            return View("Registration", model);
        }

        /// <summary>The demo account buttons are shown only while the demo data exists.</summary>
        private IActionResult LoginView(LoginViewModel model)
        {
            ViewData["ShowDemoAccounts"] = _demoData;
            return View("Login", model);
        }

        [HttpGet("login")]
        public IActionResult LoginPage(string? returnUrl = null) =>
            LoginView(new LoginViewModel { ReturnUrl = returnUrl });

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginViewModel data)
        {
            if (!ModelState.IsValid)
                return LoginView(data);

            var profile = await _userManager.FindByNameAsync(data.Email);
            if (profile != null && profile.IsBanned)
            {
                // The ban is only revealed to someone who knows the password, so the form cannot be used
                // to find out which addresses are registered and banned.
                var knowsPassword = await _userManager.CheckPasswordAsync(profile, data.Password);
                ModelState.AddModelError("", knowsPassword
                    ? "Аккаунт заблокирован. Свяжитесь с клиникой по телефону."
                    : "Неверный email или пароль");
                return LoginView(data);
            }

            // lockoutOnFailure counts wrong passwords and locks the account after too many of them.
            var result = await _signInManager.PasswordSignInAsync(data.Email, data.Password, data.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(data.ReturnUrl) && Url.IsLocalUrl(data.ReturnUrl))
                    return LocalRedirect(data.ReturnUrl);
                return RedirectToAction("Index");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Account {Email} is locked out after repeated failed sign-ins", data.Email);
                ModelState.AddModelError("", "Слишком много неудачных попыток. Вход закрыт на 15 минут, попробуйте позже или сбросьте пароль.");
            }
            else
            {
                ModelState.AddModelError("", "Неверный email или пароль");
            }

            return LoginView(data);
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

        // ------------------------------------------------------------------ password reset

        [HttpGet("forgot")]
        public IActionResult Forgot() => View(new ForgotPasswordModel());

        [HttpPost("forgot")]
        public async Task<IActionResult> Forgot(ForgotPasswordModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var profile = await _userManager.FindByEmailAsync(model.Email);
            if (profile != null && !profile.IsBanned)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(profile);
                var link = AbsoluteUrl(Url.Action("ResetPage", "Auth", new { email = profile.Email, token })!);
                await _notifier.PasswordResetAsync(profile, link);
                _logger.LogInformation("Password reset requested for profile {ProfileId}", profile.Id);
            }

            // The same answer for known and unknown addresses, so the form cannot be used to find registered emails.
            ViewData["Sent"] = true;
            return View(model);
        }

        /// <summary>
        /// Links in emails must not depend on the Host header, which a client controls.
        /// The configured public address wins, the request is only a fallback for local development.
        /// </summary>
        private string AbsoluteUrl(string path) =>
            string.IsNullOrWhiteSpace(_clinic.PublicUrl)
                ? $"{Request.Scheme}://{Request.Host}{path}"
                : _clinic.PublicUrl.TrimEnd('/') + path;

        [HttpGet("reset")]
        public IActionResult ResetPage(string? email, string? token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return RedirectToAction("Forgot");

            return View("Reset", new ResetPasswordModel { Email = email, Token = token });
        }

        [HttpPost("reset")]
        public async Task<IActionResult> Reset(ResetPasswordModel model)
        {
            if (!ModelState.IsValid)
                return View("Reset", model);

            var profile = await _userManager.FindByEmailAsync(model.Email);
            if (profile == null || profile.IsBanned)
            {
                ModelState.AddModelError("", "Ссылка недействительна. Запросите сброс пароля ещё раз.");
                return View("Reset", model);
            }

            var result = await _userManager.ResetPasswordAsync(profile, model.Token, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Code == "InvalidToken"
                        ? "Ссылка недействительна или устарела. Запросите сброс пароля ещё раз."
                        : IdentityErrors.Translate(error));
                return View("Reset", model);
            }

            // A reset also lifts a lockout caused by failed attempts.
            await _userManager.SetLockoutEndDateAsync(profile, null);
            await _userManager.ResetAccessFailedCountAsync(profile);

            TempData["Success"] = "Пароль изменён. Теперь можно войти.";
            return RedirectToAction("LoginPage");
        }
    }
}
