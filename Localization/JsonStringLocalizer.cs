using System.Globalization;
using Microsoft.Extensions.Localization;

namespace DentalClinic.Localization
{
    /// <summary>Plugs <see cref="Translations"/> into the standard localization APIs (views, data annotations).</summary>
    public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => new JsonStringLocalizer();
        public IStringLocalizer Create(string baseName, string location) => new JsonStringLocalizer();
    }

    public sealed class JsonStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name]
        {
            get
            {
                var value = Translations.Get(name);
                return new LocalizedString(name, value, resourceNotFound: ReferenceEquals(value, name));
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var template = Translations.Get(name);
                return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, template, arguments),
                    resourceNotFound: ReferenceEquals(template, name));
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Translations.For(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
                .Select(p => new LocalizedString(p.Key, p.Value));
    }
}
