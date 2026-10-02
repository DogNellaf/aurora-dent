using System.Net;
using System.Text.RegularExpressions;
using DentalClinic.Localization;

namespace DentalClinic.Tests;

/// <summary>Russian is the source language, English, French and German are translations of the same pages.</summary>
public class LocalizationTests : IClassFixture<TestApp>
{
    private static readonly string[] Translated = { "en", "fr", "de" };
    private static readonly Regex Cyrillic = new(@"\p{IsCyrillic}", RegexOptions.Compiled);

    private readonly TestApp _app;
    public LocalizationTests(TestApp app) => _app = app;

    public static IEnumerable<object[]> Languages_ => Translated.Select(l => new object[] { l });

    private static HttpClient In(HttpClient client, string language)
    {
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        return client;
    }

    /// <summary>The page without scripts, styles and the language switcher.</summary>
    private static string Visible(string html)
    {
        html = Regex.Replace(html, "<(script|style)\\b.*?</\\1>", " ", RegexOptions.Singleline);

        // What a user typed (a review, a name in a form) is shown as typed.
        html = Regex.Replace(html, "<textarea\\b.*?</textarea>", " ", RegexOptions.Singleline);
        html = Regex.Replace(html, "\\svalue=\"[^\"]*\"", " ");

        // The category buttons carry the stored category as a value for the filter script.
        html = Regex.Replace(html, "data-(category|filter)=\"[^\"]*\"", " ");

        // The switcher names every language in itself, so "Русский" is meant to stay.
        return Regex.Replace(html, "<form class=\"lang\".*?</form>", " ", RegexOptions.Singleline);
    }

    // ------------------------------------------------------------------ translation tables

    /// <summary>Every Russian text the application asks the translation table for, found in the sources.</summary>
    private static HashSet<string> KeysUsedInSources()
    {
        var root = Repo.App;
        var keys = new HashSet<string>();
        var patterns = new[]
        {
            new Regex("\\bT\\[\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled),
            new Regex("(?:Translations\\.Get|\\bT)\\(\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled),
            new Regex("ErrorMessage\\s*=\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled)
        };

        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".cshtml"))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains("Migrations"));

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var pattern in patterns)
                foreach (Match m in pattern.Matches(source))
                    keys.Add(m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\"));
        }

        // The demo data (service names, doctors, reviews) is shown through the same table.
        var seeder = File.ReadAllText(Path.Combine(root, "Data", "DemoDataSeeder.cs"));
        foreach (Match m in Regex.Matches(seeder, "\"((?:[^\"\\\\]|\\\\.)*)\""))
            if (!m.Groups[1].Value.Contains('{')) keys.Add(m.Groups[1].Value);

        keys.RemoveWhere(k => !Cyrillic.IsMatch(k));
        return keys;
    }

    [Theory]
    [MemberData(nameof(Languages_))]
    public void Every_text_in_the_sources_is_translated(string language)
    {
        var table = Translations.For(language);
        var missing = KeysUsedInSources().Where(k => !table.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"Missing in {language}.json: {string.Join(" | ", missing)}");
    }

    [Theory]
    [MemberData(nameof(Languages_))]
    public void Translations_keep_placeholders_and_are_really_translated(string language)
    {
        foreach (var (key, value) in Translations.For(language))
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"Empty translation of '{key}'");
            Assert.False(Cyrillic.IsMatch(value), $"'{key}' is not translated to {language}");
            Assert.Equal(
                Regex.Matches(key, "\\{\\d+\\}").Select(m => m.Value).Order(),
                Regex.Matches(value, "\\{\\d+\\}").Select(m => m.Value).Order());
        }
    }

    [Fact]
    public void All_tables_translate_the_same_texts()
    {
        var reference = Translations.For("en").Keys.ToHashSet();
        foreach (var language in new[] { "fr", "de" })
            Assert.True(reference.SetEquals(Translations.For(language).Keys), $"{language}.json differs from en.json");
    }

    [Fact]
    public void Unknown_texts_are_returned_unchanged()
    {
        Assert.Equal("Какая-то услуга менеджера", Translations.Get("Какая-то услуга менеджера", "en"));
        Assert.Equal("Вход", Translations.Get("Вход", "ru"));
        Assert.Equal("Sign in", Translations.Get("Вход", "en"));
    }

    // ------------------------------------------------------------------ pages

    [Theory]
    [InlineData("/")]
    [InlineData("/services")]
    [InlineData("/services/1")]
    [InlineData("/doctors")]
    [InlineData("/about")]
    [InlineData("/faq")]
    [InlineData("/contacts")]
    [InlineData("/schedule")]
    [InlineData("/route/login")]
    [InlineData("/route/register")]
    [InlineData("/route/forgot")]
    [InlineData("/no/such/page")]
    public async Task Public_pages_have_no_russian_text_in_other_languages(string url)
    {
        foreach (var language in Translated)
        {
            var response = await In(_app.NewClient(), language).GetAsync(url);
            var html = Visible(await response.Content.ReadAsStringAsync());
            var left = Cyrillic.Matches(html).Count;
            Assert.True(left == 0, $"{url} in {language} still has Russian text: {Snippet(html)}");
            Assert.Contains($"<html lang=\"{language}\"", html);
        }
    }

    [Theory]
    [InlineData("client@clinic.demo", "/client")]
    [InlineData("client@clinic.demo", "/client/review")]
    [InlineData("client@clinic.demo", "/account")]
    [InlineData("doctor@clinic.demo", "/doctor")]
    [InlineData("doctor@clinic.demo", "/doctor/appointments")]
    [InlineData("doctor@clinic.demo", "/doctor/appointments?filter=past")]
    [InlineData("doctor@clinic.demo", "/doctor/schedule")]
    [InlineData("manager@clinic.demo", "/manager/reviews/all")]
    [InlineData("manager@clinic.demo", "/manager/reviews/hidden")]
    [InlineData("admin@clinic.demo", "/admin")]
    [InlineData("admin@clinic.demo", "/admin/appointments")]
    [InlineData("admin@clinic.demo", "/admin/appointments/new")]
    [InlineData("admin@clinic.demo", "/admin/schedule")]
    [InlineData("admin@clinic.demo", "/admin/profiles")]
    [InlineData("admin@clinic.demo", "/admin/profiles/create")]
    [InlineData("admin@clinic.demo", "/admin/reviews")]
    public async Task Cabinets_have_no_russian_text_in_other_languages(string email, string url)
    {
        foreach (var language in Translated)
        {
            var client = In(await _app.LoginAsync(email), language);
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = Visible(await response.Content.ReadAsStringAsync());
            Assert.True(!Cyrillic.IsMatch(html), $"{url} in {language} still has Russian text: {Snippet(html)}");
        }
    }

    [Fact]
    public async Task Detail_pages_of_visits_and_records_are_translated_too()
    {
        foreach (var language in Translated)
        {
            var client = In(await _app.LoginAsync("client@clinic.demo"), language);
            var list = await client.GetStringAsync("/client");
            var id = Regex.Match(list, "/client/appointments/(\\d+)").Groups[1].Value;
            var html = Visible(await client.GetStringAsync($"/client/appointments/{id}"));
            Assert.True(!Cyrillic.IsMatch(html), $"visit page in {language}: {Snippet(html)}");

            var doctor = In(await _app.LoginAsync("doctor@clinic.demo"), language);
            var dash = await doctor.GetStringAsync("/doctor");
            var visit = Regex.Match(dash, "/doctor/appointments/(\\d+)").Groups[1].Value;
            Assert.True(!Cyrillic.IsMatch(Visible(await doctor.GetStringAsync($"/doctor/appointments/{visit}"))), $"doctor visit in {language}");
            var record = Regex.Match(dash, "/doctor/clients/(\\d+)/record").Groups[1].Value;
            if (record.Length > 0)
                Assert.True(!Cyrillic.IsMatch(Visible(await doctor.GetStringAsync($"/doctor/clients/{record}/record"))), $"record in {language}");
        }
    }

    private static string Snippet(string html)
    {
        var m = Cyrillic.Match(html);
        return m.Success ? html.Substring(Math.Max(0, m.Index - 60), Math.Min(140, html.Length - Math.Max(0, m.Index - 60))).ReplaceLineEndings(" ") : "";
    }

    [Fact]
    public async Task Russian_stays_the_default_language()
    {
        var html = await _app.NewClient().GetStringAsync("/");
        Assert.Contains("<html lang=\"ru\"", html);
        Assert.Contains("Записаться на приём", html);
    }

    [Theory]
    [InlineData("en-US,en;q=0.9", "en", "Book an appointment")]
    [InlineData("fr-FR,fr;q=0.9,en;q=0.5", "fr", "Prendre rendez-vous")]
    [InlineData("de-DE", "de", "Termin buchen")]
    [InlineData("ja-JP", "ru", "Записаться на приём")]
    public async Task Language_follows_the_browser_setting(string header, string lang, string expected)
    {
        var client = _app.NewClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(header);
        var html = await client.GetStringAsync("/");
        Assert.Contains($"<html lang=\"{lang}\"", html);
        Assert.Contains(expected, html);
    }

    // ------------------------------------------------------------------ switcher

    [Fact]
    public async Task The_switcher_stores_the_language_in_a_cookie_and_returns_to_the_page()
    {
        var client = _app.NewClient();
        var response = await TestApp.PostFormAsync(client, "/about", "/language", new() { ["language"] = "de", ["returnUrl"] = "/about" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/about", TestApp.Where(response));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith($"{Languages.CookieName}=c%3Dde%7Cuic%3Dde"));

        // the cookie wins over the browser header and survives the next requests
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fr");
        var page = await client.GetStringAsync("/about");
        Assert.Contains("<html lang=\"de\"", page);
        Assert.Contains("Über die Klinik", page);
    }

    [Fact]
    public async Task The_switcher_never_redirects_to_another_site_and_ignores_unknown_languages()
    {
        var client = _app.NewClient();
        var response = await TestApp.PostFormAsync(client, "/about", "/language", new() { ["language"] = "xx", ["returnUrl"] = "https://evil.example/" });

        Assert.Equal("/", TestApp.Where(response));
        Assert.Contains("<html lang=\"ru\"", await client.GetStringAsync("/"));
    }

    [Fact]
    public async Task The_switcher_needs_an_antiforgery_token()
    {
        var response = await _app.NewClient().PostAsync("/language", new FormUrlEncodedContent(new Dictionary<string, string> { ["language"] = "en" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Every_page_offers_all_languages()
    {
        var html = await _app.NewClient().GetStringAsync("/");
        foreach (var language in Languages.All)
            Assert.Contains($"value=\"{language.Code}\"", html);
    }

    // ------------------------------------------------------------------ search

    [Theory]
    [InlineData("en", "Kuznetsova", "Anna Kuznetsova")]
    [InlineData("de", "Kusnezowa", "Anna Kusnezowa")]
    [InlineData("fr", "Vassiliev", "Igor Vassiliev")]
    public async Task Admin_search_finds_demo_people_by_their_translated_name(string language, string term, string shown)
    {
        var admin = In(await _app.LoginAsync("admin@clinic.demo"), language);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/profiles?q=" + term));
        Assert.Contains(shown, html);

        var visits = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/appointments?filter=past&q=" + term));
        Assert.DoesNotContain("Application error", visits);
    }

    [Fact]
    public async Task Admin_search_in_russian_still_works()
    {
        var admin = await _app.LoginAsync("admin@clinic.demo");
        var html = await admin.GetStringAsync("/admin/profiles?q=Кузнецова");
        Assert.Contains("Анна Кузнецова", html);
        Assert.DoesNotContain("Игорь Васильев", html);
    }

    // ------------------------------------------------------------------ messages and emails

    [Theory]
    [InlineData("ru", "Укажите email")]
    [InlineData("en", "Enter your email")]
    [InlineData("fr", "Indiquez votre e-mail")]
    [InlineData("de", "Geben Sie die E-Mail-Adresse an")]
    public async Task Validation_messages_follow_the_language(string language, string expected)
    {
        var client = In(_app.NewClient(), language);
        var response = await TestApp.PostFormAsync(client, "/route/login", "/route/login", new() { ["Email"] = "", ["Password"] = "x" });
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("en", "Wrong email or password")]
    [InlineData("fr", "E-mail ou mot de passe incorrect")]
    [InlineData("de", "E-Mail oder Passwort falsch")]
    public async Task Controller_messages_follow_the_language(string language, string expected)
    {
        var client = In(_app.NewClient(), language);
        var response = await TestApp.PostFormAsync(client, "/route/login", "/route/login", new() { ["Email"] = "nobody@clinic.demo", ["Password"] = "wrong-pass" });
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("ru", "Вы записаны на приём", 5)]
    [InlineData("en", "Your appointment is booked", 6)]
    [InlineData("fr", "Votre rendez-vous est réservé", 7)]
    [InlineData("de", "Ihr Termin ist gebucht", 8)]
    public async Task Confirmation_emails_are_written_in_the_language_of_the_booking(string language, string expected, int daysAhead)
    {
        var email = $"lang-{language}@example.com";
        var client = In(await _app.RegisterAsync(email, name: "Test Patient"), language);
        var slot = _app.WithDb(db => db.Appointments
            .Where(a => a.ClientId == null && a.StartAt > DateTime.UtcNow.AddDays(daysAhead))
            .OrderBy(a => a.StartAt).Select(a => a.Id).First());

        var response = await TestApp.PostFormAsync(client, "/schedule", "/appointments/book", new() { ["appointmentId"] = slot.ToString(), ["serviceId"] = "2" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var mail = Assert.Single(_app.Emails.Sent, m => m.To == email);
        var body = WebUtility.HtmlDecode(mail.HtmlBody);
        Assert.Contains(expected, body);
        Assert.Equal(language == "ru", Cyrillic.IsMatch(body + mail.Subject));
    }
}
