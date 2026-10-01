using System.Net;

namespace DentalClinic.Tests;

public class AdminTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AdminTests(TestApp app) => _app = app;

    private static string Local(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm");

    [Theory]
    [InlineData("/admin/appointments")]
    [InlineData("/admin/appointments?filter=free")]
    [InlineData("/admin/appointments?filter=past")]
    [InlineData("/admin/appointments/new")]
    [InlineData("/admin/profiles")]
    [InlineData("/admin/profiles/create")]
    [InlineData("/admin/reviews")]
    public async Task Admin_pages_render(string url)
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Overview_shows_clinic_counters()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var html = await admin.GetStringAsync("/admin");
        Assert.Contains("Обзор клиники", html);
        Assert.Contains("свободных окон", html);
    }

    // ------------------------------------------------------------------ schedule

    [Fact]
    public async Task Admin_can_create_edit_and_delete_a_slot()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var staffId = _app.WithDb(db => db.Staffs.First().Id);
        var start = DateTime.Today.AddDays(30).AddHours(9);

        var created = await TestApp.PostFormAsync(admin, "/admin/appointments/new", "/admin/appointments/new", new()
        {
            ["StaffId"] = staffId.ToString(),
            ["ClientId"] = "0",
            ["StartAt"] = Local(start),
            ["Duration"] = "45"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var slot = _app.WithDb(db => db.Appointments.Single(a => a.StartAt == start && a.StaffId == staffId));
        Assert.Equal(45, slot.Duration);

        var edited = await TestApp.PostFormAsync(admin, $"/admin/appointments/{slot.Id}", $"/admin/appointments/{slot.Id}", new()
        {
            ["StaffId"] = staffId.ToString(),
            ["ClientId"] = "0",
            ["StartAt"] = Local(start),
            ["Duration"] = "90",
            ["Recommendation"] = "",
            ["DurationChangeReason"] = "по просьбе врача"
        });
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        Assert.Equal(90, _app.WithDb(db => db.Appointments.Single(a => a.Id == slot.Id).Duration));

        var deleted = await TestApp.PostFormAsync(admin, "/admin/appointments", $"/admin/appointments/{slot.Id}/delete", new());
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.False(_app.WithDb(db => db.Appointments.Any(a => a.Id == slot.Id)));
    }

    [Fact]
    public async Task Slot_with_unknown_doctor_or_invalid_duration_is_rejected()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var before = _app.WithDb(db => db.Appointments.Count());

        var response = await TestApp.PostFormAsync(admin, "/admin/appointments/new", "/admin/appointments/new", new()
        {
            ["StaffId"] = "9999",
            ["ClientId"] = "0",
            ["StartAt"] = Local(DateTime.Today.AddDays(31)),
            ["Duration"] = "0"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, _app.WithDb(db => db.Appointments.Count()));
    }

    [Fact]
    public async Task Missing_appointment_is_404()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/admin/appointments/999999")).StatusCode);
    }

    // ------------------------------------------------------------------ profiles

    [Fact]
    public async Task Creating_a_doctor_creates_a_staff_card_and_the_doctor_can_sign_in()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var response = await TestApp.PostFormAsync(admin, "/admin/profiles/create", "/admin/profiles/create", new()
        {
            ["FullName"] = "Николай Новиков",
            ["Email"] = "nikolay@clinic.demo",
            ["Phone"] = "+79000000000",
            ["Password"] = "secret1",
            ["ConfirmPassword"] = "secret1",
            ["RoleTitle"] = "Доктор"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.True(_app.WithDb(db => db.Staffs.Any(s => s.ExternalLogin == "nikolay@clinic.demo" && s.FullName == "Николай Новиков")));

        var doctor = await _app.LoginAsync("nikolay@clinic.demo", "secret1");
        Assert.Equal(HttpStatusCode.OK, (await doctor.GetAsync("/doctor")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_reported_when_creating_a_profile()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var response = await TestApp.PostFormAsync(admin, "/admin/profiles/create", "/admin/profiles/create", new()
        {
            ["FullName"] = "Копия",
            ["Email"] = "client@clinic.demo",
            ["Phone"] = "+79000000001",
            ["Password"] = "secret1",
            ["ConfirmPassword"] = "secret1",
            ["RoleTitle"] = "Клиент"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("уже", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Profile_update_changes_name_and_syncs_the_doctor_card()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Email == "doctor3@clinic.demo"));

        var response = await TestApp.PostFormAsync(admin, $"/admin/profiles/{profile.Id}", $"/admin/profiles/{profile.Id}", new()
        {
            ["FullName"] = "Тимур Хакимов-Старший",
            ["Email"] = "doctor3@clinic.demo",
            ["Phone"] = "+74950000099"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("Тимур Хакимов-Старший", _app.WithDb(db => db.Staffs.Single(s => s.ExternalLogin == "doctor3@clinic.demo").FullName));
    }

    [Fact]
    public async Task Profile_update_rejects_an_email_that_belongs_to_someone_else()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Email == "maria@clinic.demo"));

        var response = await TestApp.PostFormAsync(admin, $"/admin/profiles/{profile.Id}", $"/admin/profiles/{profile.Id}", new()
        {
            ["FullName"] = "Мария Орлова",
            ["Email"] = "igor@clinic.demo"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("maria@clinic.demo", _app.WithDb(db => db.Profiles.Single(p => p.Id == profile.Id).Email));
    }

    [Fact]
    public async Task Ban_blocks_an_active_session_and_unban_restores_access()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var sveta = await _app.LoginAsync("sveta@clinic.demo");
        var id = _app.WithDb(db => db.Profiles.Single(p => p.Email == "sveta@clinic.demo").Id);

        Assert.Equal(HttpStatusCode.OK, (await sveta.GetAsync("/client")).StatusCode);

        await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{id}/ban", new());
        var blocked = await sveta.GetAsync("/client");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.StartsWith("/route/login", TestApp.Where(blocked));

        await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{id}/unban", new());
        var again = await _app.LoginAsync("sveta@clinic.demo");
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/client")).StatusCode);
    }

    [Fact]
    public async Task Administrator_can_be_neither_banned_nor_deleted()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var id = _app.WithDb(db => db.Profiles.Single(p => p.Email == "admin@clinic.demo").Id);

        await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{id}/ban", new());
        await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{id}/delete", new());

        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Id == id));
        Assert.False(profile.IsBanned);
    }

    [Fact]
    public async Task Deleting_a_patient_frees_their_future_slots_and_removes_their_reviews()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var id = _app.WithDb(db => db.Profiles.Single(p => p.Email == "oleg@clinic.demo").Id);
        Assert.True(_app.WithDb(db => db.Reviews.Any(r => r.ProfileId == id)));
        var future = _app.WithDb(db => db.Appointments.Where(a => a.ClientId == id && a.StartAt > DateTime.Now).Select(a => a.Id).ToList());
        Assert.NotEmpty(future);

        var response = await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{id}/delete", new());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.False(_app.WithDb(db => db.Profiles.Any(p => p.Id == id)));
        Assert.False(_app.WithDb(db => db.Reviews.Any(r => r.ProfileId == id)));
        Assert.All(future, slot => Assert.Equal(0L, _app.WithDb(db => db.Appointments.Single(a => a.Id == slot).ClientId)));
    }

    [Fact]
    public async Task Deleting_a_doctor_removes_the_staff_card_and_the_schedule()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Email == "doctor2@clinic.demo"));
        var staffId = _app.WithDb(db => db.Staffs.Single(s => s.ExternalLogin == "doctor2@clinic.demo").Id);

        await TestApp.PostFormAsync(admin, "/admin/profiles", $"/admin/profiles/{profile.Id}/delete", new());

        Assert.False(_app.WithDb(db => db.Staffs.Any(s => s.Id == staffId)));
        Assert.False(_app.WithDb(db => db.Appointments.Any(a => a.StaffId == staffId)));
    }

    // ------------------------------------------------------------------ reviews

    [Fact]
    public async Task Admin_can_hide_show_edit_and_delete_reviews()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var review = _app.WithDb(db => db.Reviews.First(r => r.IsVisible));

        await TestApp.PostFormAsync(admin, "/admin/reviews", $"/admin/reviews/{review.Id}/hide", new());
        Assert.False(_app.WithDb(db => db.Reviews.Single(r => r.Id == review.Id).IsVisible));

        await TestApp.PostFormAsync(admin, "/admin/reviews", $"/admin/reviews/{review.Id}/show", new());
        Assert.True(_app.WithDb(db => db.Reviews.Single(r => r.Id == review.Id).IsVisible));

        await TestApp.PostFormAsync(admin, $"/admin/reviews/{review.Id}", $"/admin/reviews/{review.Id}", new() { ["Text"] = "Отредактировано администратором" });
        Assert.Equal("Отредактировано администратором", _app.WithDb(db => db.Reviews.Single(r => r.Id == review.Id).Text));

        var tooShort = await TestApp.PostFormAsync(admin, $"/admin/reviews/{review.Id}", $"/admin/reviews/{review.Id}", new() { ["Text"] = "x" });
        Assert.Equal(HttpStatusCode.OK, tooShort.StatusCode);

        await TestApp.PostFormAsync(admin, "/admin/reviews", $"/admin/reviews/{review.Id}/delete", new());
        Assert.False(_app.WithDb(db => db.Reviews.Any(r => r.Id == review.Id)));
    }
}
