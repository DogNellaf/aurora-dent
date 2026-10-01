using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DentalClinic.Services;

namespace DentalClinic.Tests;

/// <summary>
/// Boots the real app against a fresh SQL Server database seeded with the demo data. The server comes from
/// TEST_SQLSERVER_CONNECTION (a connection string without a database name); the default matches the
/// `db` service of docker-compose. Every instance creates its own database and drops it on dispose.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    private const string DefaultServer = "Server=localhost,1433;User Id=sa;Password=Aurora_Dent_123;TrustServerCertificate=True";

    private static readonly string SqlServerConnection =
        (Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION") ?? DefaultServer).TrimEnd(';');

    private readonly string _database = $"clinic_test_{Guid.NewGuid():N}";

    /// <summary>Collects every email the application would send.</summary>
    public FakeEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"{SqlServerConnection};Database={_database}",
            ["Seed:DemoData"] = "true",

            // The tests compute times with DateTime.UtcNow, so the clinic clock runs in UTC.
            ["Clinic:TimeZone"] = "UTC"
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>Path and query of a redirect, whether the server sent a relative or an absolute Location.</summary>
    public static string Where(HttpResponseMessage response) =>
        new Uri(new Uri("http://localhost"), response.Headers.Location!.OriginalString).PathAndQuery;

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public T WithDb<T>(Func<DatabaseContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<DatabaseContext>());
    }

    /// <summary>Signs in through the real login form (including the antiforgery token).</summary>
    public async Task<HttpClient> LoginAsync(string email, string password = "Demo123!")
    {
        var client = NewClient();
        var response = await PostFormAsync(client, "/route/login", "/route/login", new()
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>GETs <paramref name="formPage"/> to obtain the antiforgery token, then POSTs the form.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string formPage, string action, Dictionary<string, string> fields)
    {
        var html = await (await client.GetAsync(formPage)).Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), $"No antiforgery token on {formPage}");

        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(action, new FormUrlEncodedContent(fields));
    }

    /// <summary>Registers a new patient through the real form and returns the signed-in client.</summary>
    public async Task<HttpClient> RegisterAsync(string email, string password = "secret1", string name = "Тест Пациентов")
    {
        var client = NewClient();
        var response = await PostFormAsync(client, "/route/register", "/route/register", new()
        {
            ["FullName"] = name,
            ["Email"] = email,
            ["Phone"] = "+79001112233",
            ["Password"] = password,
            ["ConfirmPassword"] = password
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(SqlServerConnection);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];";
            command.ExecuteNonQuery();
        }
        catch (Exception) { /* best effort: the CI database is thrown away anyway */ }
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent.ToArray();

    /// <summary>Makes every send fail, to prove that a broken mail server does not break the application.</summary>
    public bool ThrowOnSend { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend) throw new InvalidOperationException("SMTP is down");
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
