using System.Net;

namespace DentalClinic.Tests;

public class OperationsTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public OperationsTests(TestApp app) => _app = app;

    [Fact]
    public async Task Health_endpoint_reports_healthy_database()
    {
        var response = await _app.NewClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await _app.NewClient().GetAsync("/");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Pages_do_not_use_inline_scripts()
    {
        // The CSP forbids inline scripts, so none of the public pages may rely on them.
        foreach (var url in new[] { "/", "/services", "/schedule?staffId=1", "/route/login", "/route/register", "/contacts" })
        {
            var html = await _app.NewClient().GetStringAsync(url);
            Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)[^>]*>", html);
            Assert.DoesNotMatch(@"\son(click|submit|load|change)\s*=", html);
        }
    }

    [Fact]
    public async Task Unknown_url_shows_the_friendly_404_page()
    {
        var response = await _app.NewClient().GetAsync("/no/such/page");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Такой страницы нет", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Static_assets_are_cached()
    {
        var response = await _app.NewClient().GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("max-age=604800", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Data_protection_keys_are_stored_in_the_database_so_sessions_survive_a_restart()
    {
        // signing in makes the application protect a cookie, which creates the key
        await _app.LoginAsync("client@clinic.demo");

        Assert.True(_app.WithDb(db => db.DataProtectionKeys.Count()) >= 1);
    }
}
