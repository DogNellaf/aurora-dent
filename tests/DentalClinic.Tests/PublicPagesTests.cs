using System.Net;

namespace DentalClinic.Tests;

public class PublicPagesTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public PublicPagesTests(TestApp app) => _app = app;

    [Theory]
    [InlineData("/")]
    [InlineData("/services")]
    [InlineData("/services/1")]
    [InlineData("/doctors")]
    [InlineData("/schedule")]
    [InlineData("/schedule?serviceId=3&staffId=1")]
    [InlineData("/about")]
    [InlineData("/faq")]
    [InlineData("/contacts")]
    [InlineData("/route/login")]
    [InlineData("/route/register")]
    public async Task Public_page_renders(string url)
    {
        var response = await _app.NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_service_is_404()
    {
        var response = await _app.NewClient().GetAsync("/services/9999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Home_shows_only_published_reviews()
    {
        var html = await _app.NewClient().GetStringAsync("/");
        Assert.Contains("Я боялась стоматологов", html);
        Assert.DoesNotContain("пришлось немного подождать", html);
    }

    [Fact]
    public void Demo_data_is_seeded_with_all_four_roles()
    {
        var (roles, doctors, services) = _app.WithDb(db => (db.Roles.Count(), db.Staffs.Count(), db.Services.Count()));
        Assert.Equal(4, roles);
        Assert.Equal(3, doctors);
        Assert.True(services >= 10);
    }
}
