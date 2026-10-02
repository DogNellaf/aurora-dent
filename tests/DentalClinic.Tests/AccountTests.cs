using System.Net;
using System.Text.RegularExpressions;

namespace DentalClinic.Tests;

public class AccountTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AccountTests(TestApp app) => _app = app;

    private async Task<HttpResponseMessage> TryLogin(HttpClient client, string email, string password) =>
        await TestApp.PostFormAsync(client, "/route/login", "/route/login", new() { ["Email"] = email, ["Password"] = password });

    // ------------------------------------------------------------------ lockout

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        const string email = "lockout@example.com";
        await _app.RegisterAsync(email);
        var client = _app.NewClient();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await TryLogin(client, email, "wrong-password");
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        }

        var locked = await TryLogin(client, email, "secret1");
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Contains("Слишком много неудачных попыток", await locked.Content.ReadAsStringAsync());
        Assert.NotNull(_app.WithDb(db => db.Profiles.Single(p => p.Email == email).LockoutEnd));
    }

    [Fact]
    public async Task A_few_wrong_passwords_do_not_lock_the_account()
    {
        const string email = "almost@example.com";
        await _app.RegisterAsync(email);
        var client = _app.NewClient();

        for (var i = 0; i < 3; i++) await TryLogin(client, email, "wrong-password");

        Assert.Equal(HttpStatusCode.Redirect, (await TryLogin(client, email, "secret1")).StatusCode);
    }

    // ------------------------------------------------------------------ password reset

    private static string ResetLink(IEnumerable<Services.EmailMessage> sent, string to)
    {
        var message = sent.Last(m => m.To == to && m.Subject == "Сброс пароля");
        var href = Regex.Match(message.HtmlBody, "href=\"([^\"]+)\"").Groups[1].Value;
        return System.Net.WebUtility.HtmlDecode(href);
    }

    [Fact]
    public async Task Password_reset_by_email_link_works_once_and_lifts_a_lockout()
    {
        const string email = "reset@example.com";
        await _app.RegisterAsync(email);

        // lock the account first
        var locker = _app.NewClient();
        for (var i = 0; i < 5; i++) await TryLogin(locker, email, "wrong-password");

        var anonymous = _app.NewClient();
        var asked = await TestApp.PostFormAsync(anonymous, "/route/forgot", "/route/forgot", new() { ["Email"] = email });
        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);

        var link = new Uri(ResetLink(_app.Emails.Sent, email));
        Assert.Equal("/route/reset", link.AbsolutePath);

        var form = await anonymous.GetAsync(link.PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);

        var token = Regex.Match(link.Query, "token=([^&]+)").Groups[1].Value;
        var fields = () => new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Token"] = Uri.UnescapeDataString(token),
            ["Password"] = "brand-new-1",
            ["ConfirmPassword"] = "brand-new-1"
        };

        var reset = await TestApp.PostFormAsync(anonymous, link.PathAndQuery, "/route/reset", fields());
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TryLogin(_app.NewClient(), email, "brand-new-1")).StatusCode);

        // the same token cannot be used twice
        var again = await TestApp.PostFormAsync(anonymous, link.PathAndQuery, "/route/reset", fields());
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("недействительна", await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reset_request_for_an_unknown_address_looks_the_same_and_sends_nothing()
    {
        var before = _app.Emails.Sent.Count;
        var response = await TestApp.PostFormAsync(_app.NewClient(), "/route/forgot", "/route/forgot", new() { ["Email"] = "nobody@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Если адрес зарегистрирован", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, _app.Emails.Sent.Count);
    }

    [Fact]
    public async Task Reset_with_a_forged_token_is_rejected()
    {
        var response = await TestApp.PostFormAsync(_app.NewClient(), "/route/reset?email=client@clinic.demo&token=abc", "/route/reset", new()
        {
            ["Email"] = "client@clinic.demo",
            ["Token"] = "forged",
            ["Password"] = "hacked-1",
            ["ConfirmPassword"] = "hacked-1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TryLogin(_app.NewClient(), "client@clinic.demo", "Demo123!")).StatusCode);
    }

    // ------------------------------------------------------------------ own profile

    [Fact]
    public async Task Any_role_can_open_its_profile_page()
    {
        foreach (var email in new[] { "client@clinic.demo", "doctor@clinic.demo", "manager@clinic.demo", "admin@clinic.demo" })
        {
            var client = await _app.LoginAsync(email);
            var html = await client.GetStringAsync("/account");
            Assert.Contains("Смена пароля", html);
            Assert.Contains(email, html);
        }
    }

    [Fact]
    public async Task Profile_page_requires_a_login()
    {
        var response = await _app.NewClient().GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Patient_can_change_name_and_phone()
    {
        const string email = "rename@example.com";
        var client = await _app.RegisterAsync(email);

        var response = await TestApp.PostFormAsync(client, "/account", "/account/profile", new()
        {
            ["Profile.FullName"] = "Новое Имя",
            ["Profile.Phone"] = "+79998887766"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var profile = _app.WithDb(db => db.Profiles.Single(p => p.Email == email));
        Assert.Equal("Новое Имя", profile.FullName);
        Assert.Equal("+79998887766", profile.PhoneNumber);
    }

    [Fact]
    public async Task Doctor_name_change_is_copied_to_the_staff_card()
    {
        var client = await _app.LoginAsync("manager@clinic.demo");
        var doctor = await _app.LoginAsync("doctor2@clinic.demo");

        await TestApp.PostFormAsync(doctor, "/account", "/account/profile", new()
        {
            ["Profile.FullName"] = "Максим Беляев-Младший",
            ["Profile.Phone"] = "+74950000010"
        });

        Assert.Equal("Максим Беляев-Младший", _app.WithDb(db => db.Staffs.Single(s => s.ExternalLogin == "doctor2@clinic.demo").FullName));
        Assert.NotNull(client);
    }

    [Fact]
    public async Task Invalid_profile_data_is_rejected()
    {
        var client = await _app.RegisterAsync("badprofile@example.com");
        var response = await TestApp.PostFormAsync(client, "/account", "/account/profile", new() { ["Profile.FullName"] = "", ["Profile.Phone"] = "nonsense" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Укажите имя", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Password_change_needs_the_current_password_and_keeps_the_session()
    {
        const string email = "changepw@example.com";
        var client = await _app.RegisterAsync(email);

        var wrong = await TestApp.PostFormAsync(client, "/account", "/account/password", new()
        {
            ["Password.Current"] = "not-my-password",
            ["Password.New"] = "second-1",
            ["Password.Confirm"] = "second-1"
        });
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains("указан неверно", await wrong.Content.ReadAsStringAsync());

        var ok = await TestApp.PostFormAsync(client, "/account", "/account/password", new()
        {
            ["Password.Current"] = "secret1",
            ["Password.New"] = "second-1",
            ["Password.Confirm"] = "second-1"
        });
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/client")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await TryLogin(_app.NewClient(), email, "second-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TryLogin(_app.NewClient(), email, "secret1")).StatusCode);
    }
}
