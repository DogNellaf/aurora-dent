using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using DentalClinic.Models;

namespace DentalClinic.Infrastructure
{
    /// <summary>
    /// Restricts a controller or action to signed-in, non-banned users with one of the given roles.
    /// Anonymous users are sent to the login page, users with the wrong role get "access denied".
    /// The resolved profile is cached in HttpContext.Items for <see cref="Controllers.BaseController.GetProfile"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RoleRequiredAttribute : Attribute, IAsyncActionFilter
    {
        public const string ProfileItemKey = "__profile";
        private readonly long[] _roles;

        public RoleRequiredAttribute(params long[] roles) => _roles = roles;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            if (http.User.Identity?.IsAuthenticated != true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var db = http.RequestServices.GetRequiredService<DatabaseContext>();
            var name = http.User.Identity!.Name;
            var profile = db.Profiles.FirstOrDefault(p => p.UserName == name);

            if (profile is null || profile.IsBanned)
            {
                // Account was deleted or banned while the cookie was still valid.
                var signIn = http.RequestServices.GetRequiredService<SignInManager<Profile>>();
                await signIn.SignOutAsync();
                context.Result = new ChallengeResult();
                return;
            }

            if (_roles.Length > 0 && !_roles.Contains(profile.RoleId))
            {
                context.Result = new ForbidResult();
                return;
            }

            http.Items[ProfileItemKey] = profile;
            await next();
        }
    }
}
