using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Tests;

/// <summary>Bans, proxies, concurrency, the mail queue and the production defaults.</summary>
public class HardeningTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public HardeningTests(TestApp app) => _app = app;

    // ------------------------------------------------------------------ visit controls

    [Fact]
    public async Task Finishing_early_or_extending_a_finished_visit_is_refused_and_changes_nothing()
    {
        var staffId = _app.WithDb(db => db.Staffs.Single(s => s.ExternalLogin == "doctor@clinic.demo").Id);
        var longPast = _app.WithDb(db =>
        {
            var visit = new Appointment
            {
                StaffId = staffId,
                ClientId = db.Profiles.Single(p => p.Email == "igor@clinic.demo").Id,
                StartAt = DateTime.UtcNow.AddDays(-400),
                Duration = 45,
                DurationChangeReason = "было"
            };
            db.Appointments.Add(visit);
            db.SaveChanges();
            return visit;
        });
        var doctor = await _app.LoginAsync("doctor@clinic.demo");

        await TestApp.PostFormAsync(doctor, "/doctor", $"/doctor/appointments/{longPast.Id}/end", new() { ["reason"] = "поздно" });
        await TestApp.PostFormAsync(doctor, "/doctor", $"/doctor/appointments/{longPast.Id}/extend", new() { ["additionalMinutes"] = "30", ["reason"] = "поздно" });

        var after = _app.WithDb(db => db.Appointments.Single(a => a.Id == longPast.Id));
        Assert.Equal(longPast.Duration, after.Duration);
        Assert.Equal(longPast.DurationChangeReason, after.DurationChangeReason);
    }

    // ------------------------------------------------------------------ banned accounts

    [Fact]
    public async Task A_banned_patient_with_a_live_session_cannot_book()
    {
        var client = await _app.RegisterAsync("banned.booker@example.com");
        var slot = _app.WithDb(db => db.Appointments.Where(a => a.ClientId == null && a.StartAt > DateTime.UtcNow.AddDays(4)).OrderBy(a => a.StartAt).Select(a => a.Id).First());
        _app.WithDb(db => { db.Profiles.Single(p => p.Email == "banned.booker@example.com").IsBanned = true; db.SaveChanges(); return 0; });

        var response = await TestApp.PostFormAsync(client, "/route/login", "/appointments/book", new() { ["appointmentId"] = slot.ToString() });

        Assert.StartsWith("/route/login", TestApp.Where(response));
        Assert.Null(_app.WithDb(db => db.Appointments.Single(a => a.Id == slot).ClientId));
    }

    [Fact]
    public async Task The_ban_message_is_shown_only_after_the_right_password()
    {
        await _app.RegisterAsync("ban.message@example.com");
        _app.WithDb(db => { db.Profiles.Single(p => p.Email == "ban.message@example.com").IsBanned = true; db.SaveChanges(); return 0; });

        var wrong = await TestApp.PostFormAsync(_app.NewClient(), "/route/login", "/route/login", new() { ["Email"] = "ban.message@example.com", ["Password"] = "not-it" });
        var right = await TestApp.PostFormAsync(_app.NewClient(), "/route/login", "/route/login", new() { ["Email"] = "ban.message@example.com", ["Password"] = "secret1" });

        Assert.Contains("Неверный email или пароль", await wrong.Content.ReadAsStringAsync());
        Assert.DoesNotContain("заблокирован", await wrong.Content.ReadAsStringAsync());
        Assert.Contains("заблокирован", await right.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_banned_doctor_disappears_from_the_site_and_cannot_be_booked()
    {
        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Email == "doctor3@clinic.demo"));
        var staff = _app.WithDb(db => db.Staffs.Single(s => s.ProfileId == profile.Id));
        var slot = _app.WithDb(db => db.Appointments.Where(a => a.StaffId == staff.Id && a.ClientId == null && a.StartAt > DateTime.UtcNow.AddDays(3)).Select(a => a.Id).First());

        var admin = await _app.LoginAsync("admin@clinic.demo");
        await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{profile.Id}/ban", new());

        var anonymous = _app.NewClient();
        Assert.DoesNotContain(staff.FullName, await anonymous.GetStringAsync("/doctors"));
        Assert.DoesNotContain(staff.FullName, await anonymous.GetStringAsync("/schedule"));
        Assert.DoesNotContain(staff.FullName, await anonymous.GetStringAsync("/"));

        var patient = await _app.RegisterAsync("late.booker@example.com");
        var response = await TestApp.PostFormAsync(patient, "/route/login", "/appointments/book", new() { ["appointmentId"] = slot.ToString() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(_app.WithDb(db => db.Appointments.Single(a => a.Id == slot).ClientId));
    }

    // ------------------------------------------------------------------ medical records

    [Fact]
    public async Task A_doctor_opens_records_only_of_patients_of_that_doctor()
    {
        await _app.RegisterAsync("stranger@example.com");
        var stranger = _app.WithDb(db => db.Profiles.Single(p => p.Email == "stranger@example.com").Id);
        var annaId = _app.WithDb(db => db.Profiles.Single(p => p.Email == "client@clinic.demo").Id);
        var doctorB = await _app.LoginAsync("doctor2@clinic.demo");
        var doctorA = await _app.LoginAsync("doctor@clinic.demo");

        // Anna is a patient of the first doctor only, a stranger is nobody's patient
        Assert.Equal(HttpStatusCode.OK, (await doctorA.GetAsync($"/doctor/clients/{annaId}/record")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await doctorA.GetAsync($"/doctor/clients/{stranger}/record")).StatusCode);
        var hasVisitWithB = _app.WithDb(db => db.Appointments.Any(a => a.ClientId == annaId && a.Staff!.ExternalLogin == "doctor2@clinic.demo"));
        Assert.Equal(hasVisitWithB ? HttpStatusCode.OK : HttpStatusCode.NotFound, (await doctorB.GetAsync($"/doctor/clients/{annaId}/record")).StatusCode);
    }

    // ------------------------------------------------------------------ reviews

    [Fact]
    public async Task Submitting_a_review_many_times_at_once_never_fails_and_leaves_one_review()
    {
        var oleg = _app.WithDb(db => db.Profiles.Single(p => p.Email == "oleg@clinic.demo"));
        _app.WithDb(db => { db.Reviews.RemoveRange(db.Reviews.Where(r => r.ProfileId == oleg.Id)); db.SaveChanges(); return 0; });

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            using var scope = _app.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IReviewService>().SubmitAsync(oleg, $"Одновременный отзыв номер {i}", 5);
        }));

        Assert.All(results, r => Assert.Equal(ReviewSubmitStatus.Saved, r));
        Assert.Equal(1, _app.WithDb(db => db.Reviews.Count(r => r.ProfileId == oleg.Id)));
    }

    // ------------------------------------------------------------------ proxies

    [Fact]
    public void Forwarded_headers_are_trusted_only_from_configured_networks()
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Apply(options, new[] { "10.0.0.0/8", "127.0.0.0/8", "fc00::/7" });

        Assert.True(TrustedProxies.IsTrusted(options, IPAddress.Parse("10.20.30.40")));
        Assert.True(TrustedProxies.IsTrusted(options, IPAddress.Parse("127.0.0.1")));
        Assert.True(TrustedProxies.IsTrusted(options, IPAddress.Parse("fd12:3456::1")));
        Assert.False(TrustedProxies.IsTrusted(options, IPAddress.Parse("203.0.113.9")));
        Assert.False(TrustedProxies.IsTrusted(options, IPAddress.Parse("2001:db8::1")));
    }

    [Fact]
    public void Without_configuration_no_proxy_is_trusted()
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Apply(options, Array.Empty<string>());
        Assert.False(TrustedProxies.IsTrusted(options, IPAddress.Parse("10.0.0.1")));
    }

    // ------------------------------------------------------------------ mail queue

    private sealed class FlakySender : IEmailSender
    {
        public readonly List<string> Delivered = new();
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (message.Subject == "boom") throw new InvalidOperationException("SMTP is down");
            lock (Delivered) Delivered.Add(message.Subject);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task The_mail_queue_delivers_in_the_background_and_survives_a_failing_message()
    {
        var sender = new FlakySender();
        var queue = new QueuedEmailDispatcher(sender, NullLogger<QueuedEmailDispatcher>.Instance);
        await queue.StartAsync(CancellationToken.None);

        queue.Enqueue(new EmailMessage("a@example.com", "A", "first", "<p>1</p>"));
        queue.Enqueue(new EmailMessage("b@example.com", "B", "boom", "<p>2</p>"));
        queue.Enqueue(new EmailMessage("c@example.com", "C", "third", "<p>3</p>"));

        for (var i = 0; i < 100 && sender.Delivered.Count < 2; i++) await Task.Delay(50);
        await queue.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { "first", "third" }, sender.Delivered);
    }

    private sealed class SlowSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
    }

    [Fact]
    public async Task Enqueueing_an_email_does_not_wait_for_the_mail_server()
    {
        var queue = new QueuedEmailDispatcher(new SlowSender(), NullLogger<QueuedEmailDispatcher>.Instance);
        await queue.StartAsync(CancellationToken.None);

        var started = DateTime.UtcNow;
        queue.Enqueue(new EmailMessage("a@example.com", "A", "slow", "<p>1</p>"));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(300));

        await queue.StopAsync(CancellationToken.None);
    }
}

/// <summary>The defaults of a real deployment, without the test settings that turn the demo data on.</summary>
public class ProductionDefaultsTests
{
    [Fact]
    public void The_shipped_configuration_has_no_demo_data_and_no_default_admin()
    {
        var file = new[] { Path.Combine(AppContext.BaseDirectory, "appsettings.json"), Path.Combine(Repo.App, "appsettings.json") }.First(File.Exists);
        var configuration = new ConfigurationBuilder().AddJsonFile(file).Build();

        Assert.False(configuration.GetValue("Seed:DemoData", true));
        Assert.True(string.IsNullOrEmpty(configuration["Bootstrap:AdminPassword"]));
        Assert.Contains("10.0.0.0/8", configuration.GetSection("Hosting:TrustedProxies").Get<string[]>()!);
        Assert.DoesNotContain("0.0.0.0/0", configuration.GetSection("Hosting:TrustedProxies").Get<string[]>()!);
    }

    [Fact]
    public void Without_demo_data_the_database_is_empty()
    {
        using var app = TestApp.WithSettings(new() { ["Seed:DemoData"] = "false" });

        Assert.Equal(0, app.WithDb(db => db.Profiles.Count()));
        Assert.Equal(0, app.WithDb(db => db.Staffs.Count()));
        Assert.Equal(4, app.WithDb(db => db.Roles.Count()));
    }

    [Fact]
    public async Task The_first_administrator_comes_from_configuration_and_is_created_once()
    {
        using var app = TestApp.WithSettings(new()
        {
            ["Seed:DemoData"] = "false",
            ["Bootstrap:AdminEmail"] = "owner@clinic.example",
            ["Bootstrap:AdminPassword"] = "S3cret-pass",
            ["Bootstrap:AdminName"] = "Владелец"
        });

        var admin = app.WithDb(db => db.Profiles.Single());
        Assert.Equal("owner@clinic.example", admin.Email);
        Assert.True(admin.IsAdmin);

        var signedIn = await app.LoginAsync("owner@clinic.example", "S3cret-pass");
        Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public void Demo_data_still_works_when_requested()
    {
        using var app = TestApp.WithSettings(new() { ["Seed:DemoData"] = "true" });
        Assert.True(app.WithDb(db => db.Profiles.Count()) > 5);
    }
}
