using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Tests;

/// <summary>Talks to the application services directly, without HTTP.</summary>
public class ServiceTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public ServiceTests(TestApp app) => _app = app;

    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _app.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Profile Patient(string email) => _app.WithDb(db => db.Profiles.Single(p => p.Email == email));

    // ------------------------------------------------------------------ clinic clock

    [Theory]
    [InlineData("2026-10-01T20:30:00Z", "Europe/Moscow", "2026-10-01 23:30")]
    [InlineData("2026-10-01T21:30:00Z", "Europe/Moscow", "2026-10-02 00:30")]
    [InlineData("2026-10-01T21:30:00Z", "UTC", "2026-10-01 21:30")]
    [InlineData("2026-01-15T12:00:00Z", "Asia/Yekaterinburg", "2026-01-15 17:00")]
    public void Clock_converts_to_the_clinic_time_zone(string utc, string zone, string expected)
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(utc));
        var clock = new ClinicClock(time, Options.Create(new ClinicOptions { TimeZone = zone }));

        Assert.Equal(DateTime.Parse(expected), clock.Now);
        Assert.Equal(DateTime.Parse(expected).Date, clock.Today);
        Assert.Equal(DateTimeKind.Unspecified, clock.Now.Kind);
    }

    [Fact]
    public void Clock_follows_the_time_provider()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-01T10:00:00Z"));
        var clock = new ClinicClock(time, Options.Create(new ClinicOptions { TimeZone = "UTC" }));

        time.Advance(TimeSpan.FromHours(5));
        Assert.Equal(DateTime.Parse("2026-10-01 15:00"), clock.Now);
    }

    [Fact]
    public void Calendar_file_uses_utc_and_escapes_text()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-01T00:00:00Z"));
        var exporter = new CalendarExporter(Options.Create(new ClinicOptions { TimeZone = "Europe/Moscow", Name = "Клиника; тест", Address = "ул. Мира, 1" }), time);

        var ics = exporter.Export(new Appointment
        {
            Id = 7,
            StartAt = DateTime.Parse("2026-10-02 13:00"),
            Duration = 45,
            Staff = new Staff { FullName = "Елена Сорокина" },
            Services = { new Service { Title = "Лечение кариеса" } }
        });

        Assert.Contains("DTSTART:20261002T100000Z", ics);   // 13:00 Moscow is 10:00 UTC
        Assert.Contains("DTEND:20261002T104500Z", ics);
        Assert.Contains("SUMMARY:Клиника\\; тест: Лечение кариеса", ics);
        Assert.Contains("LOCATION:ул. Мира\\, 1", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
    }

    // ------------------------------------------------------------------ booking service

    [Fact]
    public async Task Booking_service_reports_each_outcome()
    {
        var igor = Patient("igor@clinic.demo");
        var maria = Patient("maria@clinic.demo");
        var slot = _app.WithDb(db => db.Appointments.Where(a => a.ClientId == null && a.StartAt > DateTime.UtcNow.AddDays(8)).OrderBy(a => a.StartAt).Select(a => a.Id).First());
        var past = _app.WithDb(db =>
        {
            var visit = new Appointment { StaffId = db.Staffs.First().Id, StartAt = DateTime.UtcNow.AddDays(-400), Duration = 30 };
            db.Appointments.Add(visit);
            db.SaveChanges();
            return visit.Id;
        });

        Assert.Equal(BookingStatus.NotFound, (await InScope(s => s.GetRequiredService<IBookingService>().BookAsync(igor, 99999999, null))).Status);
        Assert.Equal(BookingStatus.InThePast, (await InScope(s => s.GetRequiredService<IBookingService>().BookAsync(igor, past, null))).Status);

        var booked = await InScope(s => s.GetRequiredService<IBookingService>().BookAsync(igor, slot, 1));
        Assert.Equal(BookingStatus.Booked, booked.Status);
        Assert.Equal(igor.Id, booked.Appointment!.ClientId);
        Assert.Single(booked.Appointment.Services);

        Assert.Equal(BookingStatus.AlreadyTaken, (await InScope(s => s.GetRequiredService<IBookingService>().BookAsync(maria, slot, null))).Status);
    }

    [Fact]
    public async Task Cancel_service_checks_ownership_and_the_two_hour_window()
    {
        var oleg = Patient("oleg@clinic.demo");
        var igor = Patient("igor@clinic.demo");
        var far = _app.WithDb(db => db.Appointments.Where(a => a.ClientId == oleg.Id && a.StartAt > DateTime.UtcNow.AddDays(1)).Select(a => a.Id).First());
        var soon = _app.WithDb(db =>
        {
            var visit = new Appointment { StaffId = db.Staffs.First().Id, ClientId = oleg.Id, StartAt = DateTime.UtcNow.AddMinutes(61).AddSeconds(7), Duration = 30 };
            db.Appointments.Add(visit);
            db.SaveChanges();
            return visit.Id;
        });

        Assert.Equal(CancelStatus.NotFound, await InScope(s => s.GetRequiredService<IBookingService>().CancelAsync(igor, far)));
        Assert.Equal(CancelStatus.TooLate, await InScope(s => s.GetRequiredService<IBookingService>().CancelAsync(oleg, soon)));
        Assert.Equal(CancelStatus.Cancelled, await InScope(s => s.GetRequiredService<IBookingService>().CancelAsync(oleg, far)));
        Assert.Null(_app.WithDb(db => db.Appointments.Single(a => a.Id == far).ClientId));
    }

    // ------------------------------------------------------------------ migrations

    [Fact]
    public void The_model_matches_the_latest_migration_snapshot()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var snapshot = ((IInfrastructure<IServiceProvider>)db).Instance.GetRequiredService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = ((IInfrastructure<IServiceProvider>)db).Instance.GetRequiredService<IModelRuntimeInitializer>().Initialize(snapshot);
        var current = ((IInfrastructure<IServiceProvider>)db).Instance.GetRequiredService<IDesignTimeModel>().Model;
        var differ = ((IInfrastructure<IServiceProvider>)db).Instance.GetRequiredService<IMigrationsModelDiffer>();

        Assert.False(differ.HasDifferences(initialized.GetRelationalModel(), current.GetRelationalModel()),
            "The model changed after the last migration. Run `dotnet ef migrations add <Name>`.");
    }

    [Fact]
    public void All_migrations_are_applied_on_startup()
    {
        Assert.Empty(_app.WithDb(db => db.Database.GetPendingMigrations().ToList()));
        Assert.NotEmpty(_app.WithDb(db => db.Database.GetAppliedMigrations().ToList()));
    }
}
