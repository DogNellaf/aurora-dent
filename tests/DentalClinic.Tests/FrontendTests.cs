using System.Net;

namespace DentalClinic.Tests;

/// <summary>Views, scripts and styles: demo box, pager, reveal animation, hidden cards.</summary>
public class FrontendTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public FrontendTests(TestApp app) => _app = app;

    [Fact]
    public async Task Demo_account_buttons_are_shown_while_demo_data_is_on()
    {
        var html = await _app.NewClient().GetStringAsync("/route/login");
        Assert.Contains("data-demo-email=\"client@clinic.demo\"", html);
        Assert.Contains("Demo123!", html);
    }

    [Fact]
    public async Task Demo_account_buttons_and_the_shared_password_are_hidden_without_demo_data()
    {
        using var app = TestApp.WithSettings(new() { ["Seed:DemoData"] = "false" });
        var client = app.NewClient();

        var page = await client.GetStringAsync("/route/login");
        Assert.DoesNotContain("data-demo-email", page);
        Assert.DoesNotContain("Demo123!", page);

        // the page shown again after a failed sign in follows the same rule
        var failed = await TestApp.PostFormAsync(client, "/route/login", "/route/login", new() { ["Email"] = "a@b.co", ["Password"] = "nope" });
        Assert.DoesNotContain("Demo123!", await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_pager_keeps_working_with_an_upper_case_page_parameter()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");

        var html = await admin.GetStringAsync("/admin/appointments?filter=free&Page=2");

        Assert.Contains("Страница 2 из", html);
        Assert.Matches("href=\"[^\"]*[?&;]?(page|Page)=3", html);
        Assert.DoesNotMatch("(page|Page)=\\d+[^\"]*[?&;](page|Page)=", html);
    }

    [Fact]
    public async Task Cards_stay_visible_without_scripts()
    {
        var css = await _app.NewClient().GetStringAsync("/css/site.css");
        var layout = await _app.NewClient().GetStringAsync("/");

        // the entrance animation hides content only when boot.js has marked the page as script enabled
        Assert.Contains(".js .reveal", css);
        Assert.DoesNotMatch(@"(^|\n)\.reveal \{ opacity: 0", css);
        Assert.Contains("/js/boot.js", layout);
        Assert.Equal(HttpStatusCode.OK, (await _app.NewClient().GetAsync("/js/boot.js")).StatusCode);
    }

    [Fact]
    public async Task The_hidden_attribute_always_hides_cards_that_set_their_own_display()
    {
        var css = await _app.NewClient().GetStringAsync("/css/site.css");
        Assert.Contains("[hidden] { display: none !important; }", css);
    }

    [Fact]
    public void Every_view_of_the_public_site_belongs_to_an_existing_page()
    {
        // leftovers of the old design pointed to actions that no longer exist
        var views = Directory.GetFiles(Path.Combine(Repo.App, "Views", "Home"), "*.cshtml").Select(f => Path.GetFileNameWithoutExtension(f)!).ToHashSet();
        var actions = typeof(Controllers.HomeController).GetMethods().Select(m => m.Name).ToHashSet();

        Assert.All(views, view => Assert.Contains(view, actions));
    }
}
