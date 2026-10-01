using System.Net;
namespace DentalClinic.Tests;

public class BookingTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public BookingTests(TestApp app) => _app = app;

    private long FreeSlotId() => _app.WithDb(db => db.Appointments
        .Where(a => a.ClientId == 0 && a.StartAt > DateTime.Now.AddDays(3))
        .OrderBy(a => a.StartAt).Select(a => a.Id).First());

    [Fact]
    public async Task Client_can_book_a_free_slot_and_a_second_client_cannot_take_it()
    {
        var slot = FreeSlotId();
        var first = await _app.LoginAsync("igor@clinic.demo");
        var second = await _app.LoginAsync("maria@clinic.demo");

        var r1 = await TestApp.PostFormAsync(first, "/schedule", "/appointments/book", new() { ["appointmentId"] = slot.ToString(), ["serviceId"] = "1" });
        Assert.Equal("/client", TestApp.Where(r1));

        var r2 = await TestApp.PostFormAsync(second, "/schedule", "/appointments/book", new() { ["appointmentId"] = slot.ToString() });
        Assert.StartsWith("/schedule", TestApp.Where(r2));

        var igor = _app.WithDb(db => db.Profiles.First(p => p.Email == "igor@clinic.demo").Id);
        var owner = _app.WithDb(db => db.Appointments.Single(a => a.Id == slot).ClientId);
        Assert.Equal(igor, owner);
    }

    [Fact]
    public async Task Staff_member_cannot_book()
    {
        var slot = FreeSlotId();
        var doctor = await _app.LoginAsync("doctor@clinic.demo");

        await TestApp.PostFormAsync(doctor, "/schedule", "/appointments/book", new() { ["appointmentId"] = slot.ToString() });

        Assert.Equal(0L, _app.WithDb(db => db.Appointments.Single(a => a.Id == slot).ClientId));
    }

    [Fact]
    public async Task Anonymous_booking_is_sent_to_login()
    {
        var client = _app.NewClient();
        var response = await TestApp.PostFormAsync(client, "/route/login", "/appointments/book", new() { ["appointmentId"] = FreeSlotId().ToString() });
        Assert.StartsWith("/route/login", TestApp.Where(response));
    }

    [Fact]
    public async Task Client_can_cancel_an_upcoming_appointment_and_the_slot_is_freed()
    {
        var anna = _app.WithDb(db => db.Profiles.First(p => p.Email == "client@clinic.demo").Id);
        var appt = _app.WithDb(db => db.Appointments.Where(a => a.ClientId == anna && a.StartAt > DateTime.Now.AddHours(3)).Select(a => a.Id).First());

        var client = await _app.LoginAsync("client@clinic.demo");
        var response = await TestApp.PostFormAsync(client, "/client", $"/client/appointments/{appt}/cancel", new());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.Equal(0L, _app.WithDb(db => db.Appointments.Single(a => a.Id == appt).ClientId));
    }

    [Fact]
    public async Task Client_cannot_open_someone_elses_appointment()
    {
        var foreign = _app.WithDb(db => db.Appointments
            .Where(a => a.ClientId != 0 && a.ClientId != db.Profiles.First(p => p.Email == "client@clinic.demo").Id)
            .Select(a => a.Id).First());

        var client = await _app.LoginAsync("client@clinic.demo");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/client/appointments/{foreign}")).StatusCode);
    }

    [Fact]
    public async Task Doctor_saves_recommendation_and_patient_sees_it()
    {
        var doctorId = _app.WithDb(db => db.Staffs.First(s => s.ExternalLogin == "doctor@clinic.demo").Id);
        var anna = _app.WithDb(db => db.Profiles.First(p => p.Email == "client@clinic.demo").Id);
        var appt = _app.WithDb(db => db.Appointments.First(a => a.StaffId == doctorId && a.ClientId == anna && a.StartAt > DateTime.Now).Id);

        var doctor = await _app.LoginAsync("doctor@clinic.demo");
        await TestApp.PostFormAsync(doctor, $"/doctor/appointments/{appt}", $"/doctor/appointments/{appt}/recommend",
            new() { ["recommendation"] = "Не есть два часа", ["returnTo"] = "detail" });

        var client = await _app.LoginAsync("client@clinic.demo");
        Assert.Contains("Не есть два часа", await client.GetStringAsync($"/client/appointments/{appt}"));
    }

    [Fact]
    public async Task Edited_review_goes_back_to_moderation_until_the_manager_publishes_it()
    {
        var oleg = _app.WithDb(db => db.Profiles.First(p => p.Email == "oleg@clinic.demo").Id);
        Assert.True(_app.WithDb(db => db.Reviews.Single(r => r.ProfileId == oleg).IsVisible));

        var client = await _app.LoginAsync("oleg@clinic.demo");
        await TestApp.PostFormAsync(client, "/client/review", "/client/review/create",
            new() { ["Text"] = "Обновлённый отзыв: всё прошло отлично, спасибо!", ["Rating"] = "5" });

        var review = _app.WithDb(db => db.Reviews.Single(r => r.ProfileId == oleg));
        Assert.False(review.IsVisible);
        Assert.DoesNotContain("Обновлённый отзыв", await _app.NewClient().GetStringAsync("/"));

        var manager = await _app.LoginAsync("manager@clinic.demo");
        await TestApp.PostFormAsync(manager, "/manager/reviews/hidden", $"/manager/reviews/{review.Id}/show", new());

        Assert.Contains("Обновлённый отзыв", await _app.NewClient().GetStringAsync("/"));
    }

    [Fact]
    public async Task Review_text_is_validated()
    {
        var client = await _app.LoginAsync("client@clinic.demo");
        var response = await TestApp.PostFormAsync(client, "/client/review", "/client/review/create", new() { ["Text"] = "ok", ["Rating"] = "5" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("от 10 до 1000 символов", await response.Content.ReadAsStringAsync());
    }
}
