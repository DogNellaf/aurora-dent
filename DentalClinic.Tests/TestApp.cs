using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Tests;

/// <summary>
/// Boots the real app against a throw-away database seeded with the demo data: a temporary SQLite file by
/// default, or a fresh database on a SQL Server when TEST_SQLSERVER_CONNECTION is set (used by CI to prove
/// that the SQL Server provider works too). The value is a connection string without a database name.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    private static readonly string? SqlServer = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION");

    private readonly string _id = Guid.NewGuid().ToString("N");
    private string DbPath => Path.Combine(Path.GetTempPath(), $"clinic-test-{_id}.db");
    private string SqlDatabase => $"clinic_test_{_id}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = SqlServer is null
                ? $"Data Source={DbPath}"
                : $"{SqlServer.TrimEnd(';')};Database={SqlDatabase}",
            ["Database:Provider"] = SqlServer is null ? "Sqlite" : "SqlServer",
            ["Seed:DemoData"] = "true"
        }));
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (SqlServer is null)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(DbPath); } catch (IOException) { }
            return;
        }

        try
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(SqlServer);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{SqlDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{SqlDatabase}];";
            command.ExecuteNonQuery();
        }
        catch (Exception) { /* best effort: the CI database is thrown away anyway */ }
    }
}
