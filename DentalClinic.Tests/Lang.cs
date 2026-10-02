using System.Globalization;

namespace DentalClinic.Tests;

/// <summary>Switches the culture of the current test for the code that formats without a request (Fmt).</summary>
public sealed class Lang : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    private Lang(string code)
    {
        var culture = DentalClinic.Localization.Languages.Find(code).Culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static Lang Use(string code) => new(code);

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
