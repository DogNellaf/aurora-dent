using System.Net;

namespace DentalClinic.Tests;

public class AccessControlTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AccessControlTests(TestApp app) => _app = app;

    [Theory]
    [InlineData("/client")]
    [InlineData("/doctor")]
    [InlineData("/manager/reviews/hidden")]
    [InlineData("/admin")]
    public async Task Anonymous_user_is_sent_to_login(string url)
    {
        var response = await _app.NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/route/login", TestApp.Where(response));
    }

    [Theory]
    [InlineData("client@clinic.demo", "/admin")]
    [InlineData("client@clinic.demo", "/doctor")]
    [InlineData("doctor@clinic.demo", "/admin/profiles")]
    [InlineData("manager@clinic.demo", "/admin")]
    [InlineData("admin@clinic.demo", "/client")]
    public async Task Wrong_role_is_denied(string email, string url)
    {
        var client = await _app.LoginAsync(email);
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/route/denied", TestApp.Where(response));
    }

    [Theory]
    [InlineData("client@clinic.demo", "/client")]
    [InlineData("doctor@clinic.demo", "/doctor")]
    [InlineData("manager@clinic.demo", "/manager/reviews/hidden")]
    [InlineData("admin@clinic.demo", "/admin")]
    public async Task Each_role_reaches_its_own_cabinet(string email, string url)
    {
        var client = await _app.LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Cabinet_redirect_matches_role()
    {
        var client = await _app.LoginAsync("doctor@clinic.demo");
        var response = await client.GetAsync("/route");
        Assert.Equal("/doctor", TestApp.Where(response));
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var client = await _app.LoginAsync("client@clinic.demo");
        var response = await client.PostAsync("/client/review/hide", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_does_not_sign_in()
    {
        var client = _app.NewClient();
        var response = await TestApp.PostFormAsync(client, "/route/login", "/route/login", new()
        {
            ["Email"] = "client@clinic.demo", ["Password"] = "nope"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Неверный email или пароль", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Banned_user_cannot_sign_in_and_unbanned_can()
    {
        const string email = "oleg@clinic.demo";
        _app.WithDb(db => { db.Profiles.First(p => p.Email == email).EmailConfirmed = false; db.SaveChanges(); return 0; });

        var client = _app.NewClient();
        var response = await TestApp.PostFormAsync(client, "/route/login", "/route/login", new() { ["Email"] = email, ["Password"] = "Demo123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("заблокирован", await response.Content.ReadAsStringAsync());

        _app.WithDb(db => { db.Profiles.First(p => p.Email == email).EmailConfirmed = true; db.SaveChanges(); return 0; });
        await _app.LoginAsync(email);
    }

    [Fact]
    public async Task Registration_creates_a_patient_and_signs_in()
    {
        var client = _app.NewClient();
        var response = await TestApp.PostFormAsync(client, "/route/register", "/route/register", new()
        {
            ["FullName"] = "Новый Пациент", ["Email"] = "new@example.com", ["Phone"] = "+79001234567",
            ["Password"] = "secret1", ["ConfirmPassword"] = "secret1"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/client")).StatusCode);
        Assert.Equal(1L, _app.WithDb(db => db.Profiles.Single(p => p.Email == "new@example.com").RoleId));
    }
}
