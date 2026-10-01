using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using DentalClinic.Models;

namespace DentalClinic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews(options =>
            {
                // Every POST form carries an antiforgery token (the form tag helper adds it automatically).
                options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
            });

            // Render Cyrillic as-is instead of &#x...; entities (smaller pages, readable HTML).
            builder.Services.AddWebEncoders(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic));

            builder.Services.AddDbContext<DatabaseContext>(options => ConfigureDatabase(options, builder.Configuration));

            builder.Services.AddIdentity<Profile, Role>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<DatabaseContext>()
            ;

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

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                db.Database.EnsureCreated();

                if (app.Configuration.GetValue("Seed:DemoData", true))
                    DemoDataSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();
            }

            app.UseForwardedHeaders();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
            }
            else
            {
                app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
            }

            if (app.Configuration.GetValue("Hosting:HttpsRedirection", false))
                app.UseHttpsRedirection();

            var ru = new CultureInfo("ru-RU");
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(ru),
                SupportedCultures = new List<CultureInfo> { ru },
                SupportedUICultures = new List<CultureInfo> { ru }
            });

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }

        /// <summary>SQLite by default (zero setup); set Database:Provider=SqlServer to use SQL Server.</summary>
        public static void ConfigureDatabase(DbContextOptionsBuilder options, IConfiguration configuration)
        {
            var provider = configuration["Database:Provider"] ?? "Sqlite";
            var connection = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connection);
                return;
            }

            // Make sure the folder for the SQLite file exists.
            var source = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connection).DataSource;
            var dir = Path.GetDirectoryName(Path.GetFullPath(source));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            options.UseSqlite(connection);
        }
    }
}
