using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using DentalClinic.Data;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Structured logs to the console; levels and sinks come from the "Serilog" section of the configuration.
            builder.Host.UseSerilog((context, services, logger) => logger
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console());

            builder.Services.AddControllersWithViews(options =>
            {
                // A non-nullable string such as Appointment.Recommendation is optional in the forms,
                // required fields carry an explicit [Required].
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

                // Every POST form carries an antiforgery token (the form tag helper adds it automatically).
                options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
            });

            // Render Cyrillic as-is instead of &#x...; entities (smaller pages, readable HTML).
            builder.Services.AddWebEncoders(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic));

            // The connection string is read lazily, so configuration added after Main starts (tests) still applies.
            // The database container may still be starting when the app boots, so transient failures are retried.
            builder.Services.AddDbContext<DatabaseContext>((services, options) =>
            {
                var connection = services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null));
            });

            builder.Services.AddIdentity<Profile, Role>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;

                // Five wrong passwords lock the account for fifteen minutes.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<DatabaseContext>()
            .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/route/login";
                options.AccessDeniedPath = "/route/denied";
                options.SlidingExpiration = true;
            });

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddHealthChecks().AddDbContextCheck<DatabaseContext>("database");

            // Application services. Controllers only translate HTTP to these calls.
            builder.Services.Configure<ClinicOptions>(builder.Configuration.GetSection(ClinicOptions.Section));
            builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IClinicClock, ClinicClock>();
            builder.Services.AddScoped<IBookingService, BookingService>();
            builder.Services.AddScoped<IScheduleService, ScheduleService>();
            builder.Services.AddScoped<IProfileService, ProfileService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddSingleton<ICalendarExporter, CalendarExporter>();
            builder.Services.AddScoped<INotifier, Notifier>();
            builder.Services.AddScoped<DemoDataSeeder>();
            if (string.IsNullOrWhiteSpace(builder.Configuration[$"{EmailOptions.Section}:Host"]))
                builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
            else
                builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<DatabaseContext>().Database.Migrate();

                if (app.Configuration.GetValue("Seed:DemoData", true))
                    scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync().GetAwaiter().GetResult();
            }

            app.UseForwardedHeaders();
            app.UseSecurityHeaders();
            app.UseSerilogRequestLogging();

            if (!app.Environment.IsDevelopment())
                app.UseExceptionHandler("/Home/Error");
            app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

            if (app.Configuration.GetValue("Hosting:HttpsRedirection", false))
                app.UseHttpsRedirection();

            var ru = new CultureInfo("ru-RU");
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(ru),
                SupportedCultures = new List<CultureInfo> { ru },
                SupportedUICultures = new List<CultureInfo> { ru }
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                // CSS and JS are versioned with asp-append-version, so they can be cached for a long time.
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=604800"
            });
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHealthChecks("/health");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
