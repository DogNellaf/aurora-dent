using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Tests;

public class FeatureTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public FeatureTests(TestApp app) => _app = app;

    private long FreeSlotId(int minDaysAhead = 5) => _app.WithDb(db => db.Appointments
        .Where(a => a.ClientId == null && a.StartAt > DateTime.UtcNow.AddDays(minDaysAhead))
        .OrderBy(a => a.StartAt).Select(a => a.Id).First());

    // ------------------------------------------------------------------ emails and calendar

    [Fact]
    public async Task Booking_sends_a_confirmation_with_a_calendar_file_and_cancelling_sends_a_notice()
    {
        const string email = "mail@example.com";
        var client = await _app.RegisterAsync(email, name: "Почта Тестова");
        var slot = FreeSlotId();

        await TestApp.PostFormAsync(client, "/schedule", "/appointments/book", new() { ["appointmentId"] = slot.ToString(), ["serviceId"] = "2" });

        var confirmation = _app.Emails.Sent.Single(m => m.To == email && m.Subject.StartsWith("Запись на"));
        Assert.Contains("Вы записаны на приём", confirmation.HtmlBody);
        Assert.Contains("Профессиональная гигиена", confirmation.HtmlBody);
        Assert.NotNull(confirmation.Attachment);
        var ics = Encoding.UTF8.GetString(confirmation.Attachment!.Content);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains($"UID:appointment-{slot}@aurora-dent", ics);
        Assert.Matches(@"DTSTART:\d{8}T\d{6}Z", ics);

        await TestApp.PostFormAsync(client, "/client", $"/client/appointments/{slot}/cancel", new());
        Assert.Single(_app.Emails.Sent, m => m.To == email && m.Subject.StartsWith("Отмена записи"));
    }

    [Fact]
    public async Task A_broken_mail_server_does_not_break_booking()
    {
        var client = await _app.RegisterAsync("nomail@example.com");
        var slot = FreeSlotId(6);

        _app.Emails.ThrowOnSend = true;
        try
        {
            var response = await TestApp.PostFormAsync(client, "/schedule", "/appointments/book", new() { ["appointmentId"] = slot.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        finally
        {
            _app.Emails.ThrowOnSend = false;
        }

        Assert.NotNull(_app.WithDb(db => db.Appointments.Single(a => a.Id == slot).ClientId));
    }

    [Fact]
    public async Task Patient_can_download_the_calendar_file_of_own_visit_only()
    {
        var anna = _app.WithDb(db => db.Profiles.Single(p => p.Email == "client@clinic.demo").Id);
        var own = _app.WithDb(db => db.Appointments.Where(a => a.ClientId == anna).OrderByDescending(a => a.StartAt).Select(a => a.Id).First());
        var foreign = _app.WithDb(db => db.Appointments.First(a => a.ClientId != null && a.ClientId != anna).Id);

        var client = await _app.LoginAsync("client@clinic.demo");
        var response = await client.GetAsync($"/client/appointments/{own}/calendar.ics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/calendar", response.Content.Headers.ContentType!.ToString());
        Assert.Contains("BEGIN:VCALENDAR", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/client/appointments/{foreign}/calendar.ics")).StatusCode);
    }

    // ------------------------------------------------------------------ bulk schedule

    [Fact]
    public async Task Admin_generates_slots_for_a_doctor_and_a_second_run_creates_nothing_new()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var staffId = _app.WithDb(db => db.Staffs.OrderBy(s => s.Id).First().Id);
        var from = DateTime.UtcNow.Date.AddDays(60);
        var fields = () => new Dictionary<string, string>
        {
            ["StaffId"] = staffId.ToString(),
            ["From"] = from.ToString("yyyy-MM-dd"),
            ["To"] = from.AddDays(1).ToString("yyyy-MM-dd"),
            ["StartTime"] = "10:00",
            ["EndTime"] = "13:00",
            ["SlotMinutes"] = "60",
            ["BreakMinutes"] = "0",
            ["SkipWeekends"] = "false"
        };

        var first = await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", fields());
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(6, _app.WithDb(db => db.Appointments.Count(a => a.StaffId == staffId && a.StartAt >= from && a.StartAt < from.AddDays(2))));

        await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", fields());
        Assert.Equal(6, _app.WithDb(db => db.Appointments.Count(a => a.StaffId == staffId && a.StartAt >= from && a.StartAt < from.AddDays(2))));
    }

    [Fact]
    public async Task Generation_honours_breaks_and_can_skip_weekends()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var staffId = _app.WithDb(db => db.Staffs.OrderBy(s => s.Id).Skip(1).First().Id);

        // find the next Saturday far in the future, generate Saturday and Sunday with weekends skipped
        var saturday = DateTime.UtcNow.Date.AddDays(90);
        while (saturday.DayOfWeek != DayOfWeek.Saturday) saturday = saturday.AddDays(1);

        await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", new()
        {
            ["StaffId"] = staffId.ToString(),
            ["From"] = saturday.ToString("yyyy-MM-dd"),
            ["To"] = saturday.AddDays(1).ToString("yyyy-MM-dd"),
            ["StartTime"] = "09:00",
            ["EndTime"] = "12:00",
            ["SlotMinutes"] = "30",
            ["BreakMinutes"] = "0",
            ["SkipWeekends"] = "true"
        });
        Assert.Equal(0, _app.WithDb(db => db.Appointments.Count(a => a.StaffId == staffId && a.StartAt >= saturday && a.StartAt < saturday.AddDays(2))));

        // 09:00-12:00 with 30 minute slots and 30 minute breaks gives 09:00, 10:00, 11:00
        await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", new()
        {
            ["StaffId"] = staffId.ToString(),
            ["From"] = saturday.ToString("yyyy-MM-dd"),
            ["To"] = saturday.ToString("yyyy-MM-dd"),
            ["StartTime"] = "09:00",
            ["EndTime"] = "12:00",
            ["SlotMinutes"] = "30",
            ["BreakMinutes"] = "30",
            ["SkipWeekends"] = "false"
        });
        var times = _app.WithDb(db => db.Appointments.Where(a => a.StaffId == staffId && a.StartAt >= saturday && a.StartAt < saturday.AddDays(1))
            .OrderBy(a => a.StartAt).Select(a => a.StartAt.Hour).ToList());
        Assert.Equal(new[] { 9, 10, 11 }, times);
    }

    [Fact]
    public async Task Invalid_generation_requests_are_rejected_with_messages()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var before = _app.WithDb(db => db.Appointments.Count());

        var reversed = await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", new()
        {
            ["From"] = "2030-05-10",
            ["To"] = "2030-05-01",
            ["StartTime"] = "09:00",
            ["EndTime"] = "18:00",
            ["SlotMinutes"] = "60",
            ["BreakMinutes"] = "0"
        });
        Assert.Contains("раньше первого", await reversed.Content.ReadAsStringAsync());

        var tooLong = await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", new()
        {
            ["From"] = "2030-01-01",
            ["To"] = "2030-12-31",
            ["StartTime"] = "09:00",
            ["EndTime"] = "18:00",
            ["SlotMinutes"] = "60",
            ["BreakMinutes"] = "0"
        });
        Assert.Contains("Не более 62 дней", await tooLong.Content.ReadAsStringAsync());

        var backwards = await TestApp.PostFormAsync(admin, "/admin/schedule", "/admin/schedule", new()
        {
            ["From"] = "2030-05-01",
            ["To"] = "2030-05-02",
            ["StartTime"] = "18:00",
            ["EndTime"] = "09:00",
            ["SlotMinutes"] = "60",
            ["BreakMinutes"] = "0"
        });
        Assert.Contains("позже начала", await backwards.Content.ReadAsStringAsync());

        Assert.Equal(before, _app.WithDb(db => db.Appointments.Count()));
    }

    [Fact]
    public async Task Doctor_generates_slots_only_for_self()
    {
        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        var own = _app.WithDb(db => db.Staffs.Single(s => s.ExternalLogin == "doctor@clinic.demo").Id);
        var other = _app.WithDb(db => db.Staffs.First(s => s.ExternalLogin != "doctor@clinic.demo").Id);
        var day = DateTime.UtcNow.Date.AddDays(120);
        var otherBefore = _app.WithDb(db => db.Appointments.Count(a => a.StaffId == other && a.StartAt >= day));

        // the form tries to smuggle in another doctor's id
        await TestApp.PostFormAsync(doctor, "/doctor/schedule", "/doctor/schedule", new()
        {
            ["StaffId"] = other.ToString(),
            ["From"] = day.ToString("yyyy-MM-dd"),
            ["To"] = day.ToString("yyyy-MM-dd"),
            ["StartTime"] = "14:00",
            ["EndTime"] = "16:00",
            ["SlotMinutes"] = "60",
            ["BreakMinutes"] = "0",
            ["SkipWeekends"] = "false"
        });

        Assert.Equal(2, _app.WithDb(db => db.Appointments.Count(a => a.StaffId == own && a.StartAt >= day && a.StartAt < day.AddDays(1))));
        Assert.Equal(otherBefore, _app.WithDb(db => db.Appointments.Count(a => a.StaffId == other && a.StartAt >= day)));
    }

    [Fact]
    public async Task Only_doctors_and_administrators_can_open_the_generators()
    {
        var patient = await _app.LoginAsync("client@clinic.demo");
        Assert.StartsWith("/route/denied", TestApp.Where(await patient.GetAsync("/admin/schedule")));
        Assert.StartsWith("/route/denied", TestApp.Where(await patient.GetAsync("/doctor/schedule")));

        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        Assert.Equal(HttpStatusCode.OK, (await doctor.GetAsync("/doctor/schedule")).StatusCode);
        Assert.StartsWith("/route/denied", TestApp.Where(await doctor.GetAsync("/admin/schedule")));
    }

    // ------------------------------------------------------------------ search and paging

    [Fact]
    public async Task Admin_profile_search_and_role_filter()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");

        var byName = await admin.GetStringAsync("/admin/profiles?q=Кузнецова");
        Assert.Contains("Анна Кузнецова", byName);
        Assert.DoesNotContain("Игорь Васильев", byName);

        var byEmail = await admin.GetStringAsync("/admin/profiles?q=igor@");
        Assert.Contains("Игорь Васильев", byEmail);

        var doctors = await admin.GetStringAsync("/admin/profiles?role=4");
        Assert.Contains("Елена Сорокина", doctors);
        Assert.DoesNotContain("Анна Кузнецова", doctors);
    }

    [Fact]
    public async Task Admin_appointment_search_and_paging()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");

        var free = await admin.GetStringAsync("/admin/appointments?filter=free");
        Assert.Contains("Страница 1 из", free);

        var second = await admin.GetStringAsync("/admin/appointments?filter=free&page=2");
        Assert.Contains("Страница 2 из", second);
        Assert.Contains("filter=free", second);

        // asking for a page far beyond the end shows the last page instead of failing
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/admin/appointments?filter=free&page=9999")).StatusCode);

        var search = await admin.GetStringAsync("/admin/appointments?q=Игорь");
        Assert.Contains("Игорь Васильев", search);
        Assert.DoesNotContain("Олег Петров", search);
    }

    [Fact]
    public async Task Admin_review_filters()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var pending = await admin.GetStringAsync("/admin/reviews?filter=pending");
        Assert.Contains("пришлось немного подождать", pending);
        Assert.DoesNotContain("Я боялась стоматологов", pending);

        var published = await admin.GetStringAsync("/admin/reviews?filter=published");
        Assert.Contains("Я боялась стоматологов", published);
    }

    [Fact]
    public async Task Doctor_history_is_paged()
    {
        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        Assert.Equal(HttpStatusCode.OK, (await doctor.GetAsync("/doctor/appointments?page=2")).StatusCode);
        Assert.Contains("Страница", await doctor.GetStringAsync("/doctor/appointments"));
    }

    // ------------------------------------------------------------------ data model rules

    [Fact]
    public void Database_refuses_two_appointments_for_a_doctor_at_the_same_time()
    {
        var staffId = _app.WithDb(db => db.Staffs.First().Id);
        var at = DateTime.UtcNow.Date.AddDays(200).AddHours(10);
        _app.WithDb(db => { db.Appointments.Add(new Appointment { StaffId = staffId, StartAt = at }); db.SaveChanges(); return 0; });

        var error = Assert.Throws<DbUpdateException>(() =>
            _app.WithDb(db => { db.Appointments.Add(new Appointment { StaffId = staffId, StartAt = at }); db.SaveChanges(); return 0; }));
        Assert.Contains("IX_Appointment_StaffId_StartAt", error.InnerException!.Message);
    }

    [Fact]
    public async Task Admin_gets_a_readable_message_for_a_double_booked_time()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var existing = _app.WithDb(db => db.Appointments.OrderBy(a => a.Id).First());

        var response = await TestApp.PostFormAsync(admin, "/admin/appointments/new", "/admin/appointments/new", new()
        {
            ["StaffId"] = existing.StaffId.ToString(),
            ["ClientId"] = "",
            ["StartAt"] = existing.StartAt.ToString("yyyy-MM-ddTHH:mm"),
            ["Duration"] = "60"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("уже есть приём на это время", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Appointment_client_must_be_a_real_profile()
    {
        var staffId = _app.WithDb(db => db.Staffs.First().Id);
        var at = DateTime.UtcNow.Date.AddDays(201).AddHours(10);

        Assert.Throws<DbUpdateException>(() =>
            _app.WithDb(db => { db.Appointments.Add(new Appointment { StaffId = staffId, ClientId = 987654321, StartAt = at }); db.SaveChanges(); return 0; }));
    }

    [Fact]
    public void A_patient_cannot_have_two_reviews()
    {
        var igor = _app.WithDb(db => db.Profiles.Single(p => p.Email == "igor@clinic.demo").Id);

        Assert.Throws<DbUpdateException>(() =>
            _app.WithDb(db => { db.Reviews.Add(new Review { ProfileId = igor, Text = "Второй отзыв подряд", CreatedAt = DateTime.UtcNow }); db.SaveChanges(); return 0; }));
    }

    [Fact]
    public void Prices_are_stored_as_exact_decimals()
    {
        var price = _app.WithDb(db => db.Services.Single(s => s.Title == "Профессиональная гигиена").Price);
        Assert.Equal(5500.00m, price);

        var id = _app.WithDb(db =>
        {
            var service = new Service { Title = "Тестовая услуга", Price = 1234.56m, Category = "Тест" };
            db.Services.Add(service);
            db.SaveChanges();
            return service.Id;
        });
        Assert.Equal(1234.56m, _app.WithDb(db => db.Services.Single(s => s.Id == id).Price));
    }

    [Fact]
    public async Task Deleting_a_patient_keeps_finished_visits_in_the_history_without_a_patient()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var oleg = _app.WithDb(db => db.Profiles.Single(p => p.Email == "oleg@clinic.demo").Id);
        var pastVisit = _app.WithDb(db => db.Appointments.Single(a => a.ClientId == oleg && a.StartAt < DateTime.UtcNow).Id);

        await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{oleg}/delete", new());

        var visit = _app.WithDb(db => db.Appointments.Single(a => a.Id == pastVisit));
        Assert.Null(visit.ClientId);
        Assert.Contains("Холод на область операции", visit.Recommendation);
    }
}
