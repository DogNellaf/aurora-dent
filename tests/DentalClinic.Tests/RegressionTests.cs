using System.Net;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;
using DentalClinic.Models.DTO;
using DentalClinic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Tests;

/// <summary>Regressions of data and account handling found in code review: profile edits, reset links, released slots, legacy databases, SMTP security.</summary>
public class RegressionTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public RegressionTests(TestApp app) => _app = app;

    [Fact]
    public async Task Editing_a_profile_does_not_block_the_user()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Email == "sveta@clinic.demo"));

        var response = await TestApp.PostFormAsync(admin, $"/admin/profiles/{profile.Id}", $"/admin/profiles/{profile.Id}", new()
        {
            ["FullName"] = "Светлана Ким-Новая",
            ["Email"] = "sveta@clinic.demo",
            ["Phone"] = "+79770000000"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.False(_app.WithDb(db => db.Profiles.Single(p => p.Id == profile.Id).IsBanned));
        await _app.LoginAsync("sveta@clinic.demo");
    }

    [Fact]
    public async Task Changing_the_email_keeps_the_user_able_to_sign_in_with_the_new_address()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Email == "maria@clinic.demo"));

        await TestApp.PostFormAsync(admin, $"/admin/profiles/{profile.Id}", $"/admin/profiles/{profile.Id}", new()
        {
            ["FullName"] = "Мария Орлова",
            ["Email"] = "maria.new@clinic.demo",
            ["Phone"] = "+79250000000"
        });

        await _app.LoginAsync("maria.new@clinic.demo");
    }

    [Fact]
    public async Task Password_reset_link_ignores_a_forged_host_header()
    {
        var client = _app.NewClient();
        client.DefaultRequestHeaders.Host = "evil.example";

        await TestApp.PostFormAsync(client, "/route/forgot", "/route/forgot", new() { ["Email"] = "client@clinic.demo" });

        var mail = _app.Emails.Sent.Last(m => m.To == "client@clinic.demo" && m.Subject == "Сброс пароля");
        Assert.Contains("href=\"https://clinic.example/route/reset?", mail.HtmlBody);
        Assert.DoesNotContain("evil.example", mail.HtmlBody);
    }

    [Fact]
    public async Task A_cancelled_slot_does_not_keep_the_previous_visit_data()
    {
        var client = await _app.RegisterAsync("leftovers@example.com");
        var slot = _app.WithDb(db => db.Appointments.Where(a => a.ClientId == null && a.StartAt > DateTime.UtcNow.AddDays(10)).OrderBy(a => a.StartAt).Select(a => a.Id).First());
        await TestApp.PostFormAsync(client, "/schedule", "/appointments/book", new() { ["appointmentId"] = slot.ToString(), ["serviceId"] = "1" });
        _app.WithDb(db =>
        {
            var a = db.Appointments.Single(x => x.Id == slot);
            a.Recommendation = "Личная рекомендация предыдущего пациента";
            a.DurationChangeReason = "причина прошлого визита";
            db.SaveChanges();
            return 0;
        });

        await TestApp.PostFormAsync(client, "/client", $"/client/appointments/{slot}/cancel", new());

        var freed = _app.WithDb(db => db.Appointments.Include(a => a.Services).Single(a => a.Id == slot));
        Assert.Null(freed.ClientId);
        Assert.Empty(freed.Services);
        Assert.Equal(string.Empty, freed.Recommendation);
        Assert.Equal(string.Empty, freed.DurationChangeReason);
    }

    [Fact]
    public async Task Releasing_a_slot_in_the_admin_form_clears_the_old_visit_too()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var igor = _app.WithDb(db => db.Profiles.Single(p => p.Email == "igor@clinic.demo").Id);
        var slot = _app.WithDb(db =>
        {
            var a = db.Appointments.Include(x => x.Services).First(x => x.ClientId == null && x.StartAt > DateTime.UtcNow.AddDays(12));
            a.ClientId = igor;
            a.Recommendation = "Старая рекомендация";
            a.Services.Add(db.Services.First());
            db.SaveChanges();
            return new { a.Id, a.StaffId, a.StartAt };
        });

        await TestApp.PostFormAsync(admin, $"/admin/appointments/{slot.Id}", $"/admin/appointments/{slot.Id}", new()
        {
            ["StaffId"] = slot.StaffId.ToString(),
            ["ClientId"] = "",
            ["StartAt"] = slot.StartAt.ToString("yyyy-MM-ddTHH:mm"),
            ["Duration"] = "60",
            ["Recommendation"] = "Старая рекомендация",
            ["DurationChangeReason"] = ""
        });

        var freed = _app.WithDb(db => db.Appointments.Include(a => a.Services).Single(a => a.Id == slot.Id));
        Assert.Null(freed.ClientId);
        Assert.Empty(freed.Services);
        Assert.Equal(string.Empty, freed.Recommendation);
    }

    [Fact]
    public async Task Generating_slots_for_an_unknown_doctor_is_a_form_error_not_a_server_error()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var before = _app.WithDb(db => db.Appointments.Count());

        var response = await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", new()
        {
            ["StaffId"] = "99999",
            ["From"] = "2031-01-10",
            ["To"] = "2031-01-10",
            ["StartTime"] = "10:00",
            ["EndTime"] = "12:00",
            ["SlotMinutes"] = "60",
            ["BreakMinutes"] = "0",
            ["SkipWeekends"] = "false"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Выберите врача из списка", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, _app.WithDb(db => db.Appointments.Count()));
    }

    [Fact]
    public async Task Concurrent_generation_for_the_same_day_never_fails_and_never_duplicates()
    {
        var staffId = _app.WithDb(db => db.Staffs.OrderBy(s => s.Id).Last().Id);
        var day = DateTime.UtcNow.Date.AddDays(300);
        var model = () => new GenerateScheduleModel
        {
            StaffId = staffId,
            From = day,
            To = day,
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            SlotMinutes = 30,
            SkipWeekends = false
        };

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            using var scope = _app.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IScheduleService>().GenerateAsync(model());
        }));

        Assert.All(results, r => Assert.True(r.Status is GenerateStatus.Done or GenerateStatus.Conflict));
        Assert.Contains(results, r => r.Status == GenerateStatus.Done);

        // whatever the interleaving, a final run completes the day exactly once
        using (var scope = _app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IScheduleService>().GenerateAsync(model());
        Assert.Equal(16, _app.WithDb(db => db.Appointments.Count(a => a.StaffId == staffId && a.StartAt >= day && a.StartAt < day.AddDays(1))));
    }

    [Fact]
    public void A_database_without_migration_history_is_refused_with_a_clear_message()
    {
        using var legacy = new TestApp();
        legacy.CreateLegacyDatabase();

        var error = Record.Exception(() => legacy.NewClient());

        var messages = new List<string>();
        for (var e = error; e != null; e = e.InnerException) messages.Add(e.Message);
        Assert.Contains(messages, m => m.Contains("no EF Core migration history"));
    }

    [Theory]
    [InlineData(false, 25, MailKit.Security.SecureSocketOptions.None)]
    [InlineData(false, 465, MailKit.Security.SecureSocketOptions.None)]
    [InlineData(true, 465, MailKit.Security.SecureSocketOptions.SslOnConnect)]
    [InlineData(true, 587, MailKit.Security.SecureSocketOptions.StartTls)]
    [InlineData(true, 25, MailKit.Security.SecureSocketOptions.StartTls)]
    public void Smtp_encryption_is_required_or_absent_never_opportunistic(bool useSsl, int port, MailKit.Security.SecureSocketOptions expected)
    {
        var chosen = SmtpEmailSender.ChooseSecurity(new EmailOptions { UseSsl = useSsl, Port = port });

        Assert.Equal(expected, chosen);
        Assert.NotEqual(MailKit.Security.SecureSocketOptions.Auto, chosen);
        Assert.NotEqual(MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable, chosen);
    }

    [Fact]
    public async Task Moving_a_slot_to_another_patient_does_not_pass_on_the_old_visit_data()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var (igor, other) = _app.WithDb(db => (db.Profiles.Single(p => p.Email == "igor@clinic.demo").Id, db.Profiles.Single(p => p.Email == "oleg@clinic.demo").Id));
        var slot = _app.WithDb(db =>
        {
            var a = db.Appointments.Include(x => x.Services).Where(x => x.ClientId == null && x.StartAt > DateTime.UtcNow.AddDays(5)).OrderByDescending(x => x.StartAt).First();
            a.ClientId = igor;
            a.Recommendation = "Рекомендация для Игоря";
            a.DurationChangeReason = "причина Игоря";
            a.Services.Add(db.Services.First());
            db.SaveChanges();
            return new { a.Id, a.StaffId, a.StartAt };
        });

        await TestApp.PostFormAsync(admin, $"/admin/appointments/{slot.Id}", $"/admin/appointments/{slot.Id}", new()
        {
            ["StaffId"] = slot.StaffId.ToString(),
            ["ClientId"] = other.ToString(),
            ["StartAt"] = slot.StartAt.ToString("yyyy-MM-ddTHH:mm"),
            ["Duration"] = "60",
            ["Recommendation"] = "Рекомендация для Игоря",
            ["DurationChangeReason"] = "причина Игоря"
        });

        var moved = _app.WithDb(db => db.Appointments.Include(a => a.Services).Single(a => a.Id == slot.Id));
        Assert.Equal(other, moved.ClientId);
        Assert.Empty(moved.Services);
        Assert.Equal(string.Empty, moved.Recommendation);
        Assert.Equal(string.Empty, moved.DurationChangeReason);
    }

    [Fact]
    public async Task A_rejected_profile_edit_changes_nothing()
    {
        var original = _app.WithDb(db => db.Profiles.Single(p => p.Email == "doctor3@clinic.demo"));
        var staffName = _app.WithDb(db => db.Staffs.Single(s => s.ProfileId == original.Id).FullName);

        using var scope = _app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IProfileService>().UpdateAsync(original.Id, new ProfileEditModel
        {
            Id = original.Id,
            FullName = "Не должно сохраниться",
            Email = "not a valid login with spaces",
            Phone = "+70000000000"
        });

        Assert.Equal(UpdateProfileStatus.Invalid, result.Status);
        Assert.NotEmpty(result.Errors);

        var after = _app.WithDb(db => db.Profiles.Single(p => p.Id == original.Id));
        Assert.Equal(original.Email, after.Email);
        Assert.Equal(original.FullName, after.FullName);
        Assert.Equal(staffName, _app.WithDb(db => db.Staffs.Single(s => s.ProfileId == original.Id).FullName));
    }
}
