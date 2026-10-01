using System.Net;

namespace DentalClinic.Tests;

public class RoleCabinetTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public RoleCabinetTests(TestApp app) => _app = app;

    // ------------------------------------------------------------------ doctor

    [Theory]
    [InlineData("/doctor/appointments")]
    [InlineData("/doctor/appointments?filter=past")]
    public async Task Doctor_lists_render(string url)
    {
        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        Assert.Equal(HttpStatusCode.OK, (await doctor.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Doctor_dashboard_shows_the_visit_in_progress()
    {
        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        var html = await doctor.GetStringAsync("/doctor");
        Assert.Contains("Идёт приём", html);
        Assert.Contains("Мария Орлова", html);
    }

    private (long Id, short Duration) CurrentVisit() => _app.WithDb(db =>
    {
        var staff = db.Staffs.Single(s => s.ExternalLogin == "doctor@clinic.demo");
        var now = DateTime.Now;
        var a = db.Appointments.AsEnumerable().First(x => x.StaffId == staff.Id && x.ClientId != 0 && x.StartAt <= now && x.EndAt >= now);
        return (a.Id, a.Duration);
    });

    [Fact]
    public async Task Doctor_can_extend_the_current_visit()
    {
        var (id, before) = CurrentVisit();
        var doctor = await _app.LoginAsync("doctor@clinic.demo");

        await TestApp.PostFormAsync(doctor, "/doctor", $"/doctor/appointments/{id}/extend", new() { ["additionalMinutes"] = "15", ["reason"] = "сложный случай" });

        var after = _app.WithDb(db => db.Appointments.Single(a => a.Id == id));
        Assert.Equal(before + 15, after.Duration);
        Assert.Equal("сложный случай", after.DurationChangeReason);
    }

    [Fact]
    public async Task Extending_by_a_nonsense_amount_changes_nothing()
    {
        var (id, before) = CurrentVisit();
        var doctor = await _app.LoginAsync("doctor@clinic.demo");

        await TestApp.PostFormAsync(doctor, "/doctor", $"/doctor/appointments/{id}/extend", new() { ["additionalMinutes"] = "-5" });
        await TestApp.PostFormAsync(doctor, "/doctor", $"/doctor/appointments/{id}/extend", new() { ["additionalMinutes"] = "500" });

        Assert.Equal(before, _app.WithDb(db => db.Appointments.Single(a => a.Id == id).Duration));
    }

    [Fact]
    public async Task Doctor_can_finish_a_visit_early()
    {
        var id = _app.WithDb(db =>
        {
            var visit = new Models.Appointment
            {
                StaffId = db.Staffs.Single(s => s.ExternalLogin == "doctor@clinic.demo").Id,
                ClientId = db.Profiles.First(p => p.Email == "igor@clinic.demo").Id,
                StartAt = DateTime.Now.AddMinutes(-20),
                Duration = 60
            };
            db.Appointments.Add(visit);
            db.SaveChanges();
            return visit.Id;
        });

        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        await TestApp.PostFormAsync(doctor, "/doctor", $"/doctor/appointments/{id}/end", new() { ["reason"] = "всё выполнено" });

        var finished = _app.WithDb(db => db.Appointments.Single(a => a.Id == id));
        Assert.InRange(finished.Duration, 19, 22);
        Assert.Equal("всё выполнено", finished.DurationChangeReason);
    }

    [Fact]
    public async Task Doctor_sees_patient_records_but_not_other_doctors_visits()
    {
        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        var anna = _app.WithDb(db => db.Profiles.Single(p => p.Email == "client@clinic.demo").Id);
        Assert.Contains("Анна Кузнецова", await doctor.GetStringAsync($"/doctor/clients/{anna}/record"));

        var foreign = _app.WithDb(db => db.Appointments
            .First(a => a.ClientId != 0 && a.StaffId != db.Staffs.First(s => s.ExternalLogin == "doctor@clinic.demo").Id).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await doctor.GetAsync($"/doctor/appointments/{foreign}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await doctor.GetAsync("/doctor/clients/999999/record")).StatusCode);
    }

    // ------------------------------------------------------------------ manager

    [Fact]
    public async Task Manager_sees_both_review_lists_and_can_hide_a_published_review()
    {
        var manager = await _app.LoginAsync("manager@clinic.demo");
        Assert.Contains("пришлось немного подождать", await manager.GetStringAsync("/manager/reviews/hidden"));

        var published = await manager.GetStringAsync("/manager/reviews/all");
        Assert.Contains("Я боялась стоматологов", published);

        var review = _app.WithDb(db => db.Reviews.First(r => r.IsVisible));
        await TestApp.PostFormAsync(manager, "/manager/reviews/all", $"/manager/reviews/{review.Id}/hide", new());
        Assert.False(_app.WithDb(db => db.Reviews.Single(r => r.Id == review.Id).IsVisible));
    }

    [Fact]
    public async Task Manager_gets_404_for_a_missing_review()
    {
        var manager = await _app.LoginAsync("manager@clinic.demo");
        var response = await TestApp.PostFormAsync(manager, "/manager/reviews/hidden", "/manager/reviews/999999/show", new());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ patient & auth

    [Fact]
    public async Task Patient_without_a_past_visit_cannot_write_a_review()
    {
        var sveta = await _app.LoginAsync("sveta@clinic.demo");
        Assert.Contains("после первого визита", await sveta.GetStringAsync("/client/review"));

        var response = await TestApp.PostFormAsync(sveta, "/client", "/client/review/create", new() { ["Text"] = "Отзыв без визита, так нельзя", ["Rating"] = "5" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/route/denied", TestApp.Where(response));
    }

    [Fact]
    public async Task Patient_can_hide_a_published_review()
    {
        var igor = await _app.LoginAsync("igor@clinic.demo");
        var id = _app.WithDb(db => db.Profiles.Single(p => p.Email == "igor@clinic.demo").Id);

        await TestApp.PostFormAsync(igor, "/client/review", "/client/review/hide", new());
        Assert.False(_app.WithDb(db => db.Reviews.Single(r => r.ProfileId == id).IsVisible));
    }

    [Fact]
    public async Task Cancelling_too_late_is_refused()
    {
        var anna = _app.WithDb(db => db.Profiles.Single(p => p.Email == "client@clinic.demo").Id);
        var doctorId = _app.WithDb(db => db.Staffs.First().Id);
        var soon = _app.WithDb(db =>
        {
            db.Appointments.Add(new Models.Appointment { StaffId = doctorId, ClientId = anna, StartAt = DateTime.Now.AddMinutes(30), Duration = 30 });
            db.SaveChanges();
            return db.Appointments.OrderByDescending(a => a.Id).First().Id;
        });

        var client = await _app.LoginAsync("client@clinic.demo");
        await TestApp.PostFormAsync(client, "/client", $"/client/appointments/{soon}/cancel", new());

        Assert.Equal(anna, _app.WithDb(db => db.Appointments.Single(a => a.Id == soon).ClientId));
    }

    [Fact]
    public async Task Logout_signs_the_user_out()
    {
        var client = await _app.LoginAsync("client@clinic.demo");
        var response = await TestApp.PostFormAsync(client, "/client", "/route/logout", new());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/route/login", TestApp.Where(await client.GetAsync("/client")));
    }

    [Fact]
    public async Task Logout_via_GET_is_not_allowed()
    {
        var client = await _app.LoginAsync("client@clinic.demo");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/route/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/client")).StatusCode);
    }

    [Fact]
    public async Task Login_follows_a_local_return_url_but_ignores_an_external_one()
    {
        var local = await TestApp.PostFormAsync(_app.NewClient(), "/route/login", "/route/login",
            new() { ["Email"] = "client@clinic.demo", ["Password"] = "Demo123!", ["ReturnUrl"] = "/schedule?staffId=1" });
        Assert.Equal("/schedule?staffId=1", TestApp.Where(local));

        var external = await TestApp.PostFormAsync(_app.NewClient(), "/route/login", "/route/login",
            new() { ["Email"] = "client@clinic.demo", ["Password"] = "Demo123!", ["ReturnUrl"] = "https://evil.example/" });
        Assert.Equal("/route", TestApp.Where(external));
    }

    [Fact]
    public async Task Registration_rejects_a_duplicate_email_and_a_weak_password()
    {
        var duplicate = await TestApp.PostFormAsync(_app.NewClient(), "/route/register", "/route/register", new()
        {
            ["FullName"] = "Дубль",
            ["Email"] = "client@clinic.demo",
            ["Phone"] = "+79001112233",
            ["Password"] = "secret1",
            ["ConfirmPassword"] = "secret1"
        });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("уже зарегистрирован", await duplicate.Content.ReadAsStringAsync());

        var weak = await TestApp.PostFormAsync(_app.NewClient(), "/route/register", "/route/register", new()
        {
            ["FullName"] = "Слабый",
            ["Email"] = "weak@example.com",
            ["Phone"] = "+79001112233",
            ["Password"] = "abc",
            ["ConfirmPassword"] = "abc"
        });
        Assert.Equal(HttpStatusCode.OK, weak.StatusCode);
        Assert.False(_app.WithDb(db => db.Profiles.Any(p => p.Email == "weak@example.com")));
    }

    [Fact]
    public async Task Access_denied_page_is_a_403()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _app.NewClient().GetAsync("/route/denied")).StatusCode);
    }
}
