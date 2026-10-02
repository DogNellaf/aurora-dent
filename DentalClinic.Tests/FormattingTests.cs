using DentalClinic.Infrastructure;

namespace DentalClinic.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData(0, "Бесплатно")]
    [InlineData(5500, "5 500 ₽")]
    [InlineData(95000, "95 000 ₽")]
    public void Money_uses_russian_grouping(decimal value, string expected)
    {
        using var _ = Lang.Use("ru");
        Assert.Equal(expected, Fmt.Money(value));
    }

    [Theory]
    [InlineData(1, "1 год")]
    [InlineData(3, "3 года")]
    [InlineData(5, "5 лет")]
    [InlineData(11, "11 лет")]
    [InlineData(12, "12 лет")]
    [InlineData(21, "21 год")]
    public void Years_declension(int n, string expected)
    {
        using var _ = Lang.Use("ru");
        Assert.Equal(expected, Fmt.Years(n));
    }

    [Theory]
    [InlineData(30, "30 мин")]
    [InlineData(60, "1 ч")]
    [InlineData(90, "1 ч 30 мин")]
    [InlineData(120, "2 ч")]
    public void Minutes_are_humanised(int minutes, string expected)
    {
        using var _ = Lang.Use("ru");
        Assert.Equal(expected, Fmt.Minutes(minutes));
    }

    [Theory]
    [InlineData("en", 1, "1 year")]
    [InlineData("en", 3, "3 years")]
    [InlineData("fr", 1, "1 an")]
    [InlineData("fr", 12, "12 ans")]
    [InlineData("de", 1, "1 Jahr")]
    [InlineData("de", 5, "5 Jahre")]
    public void Years_follow_the_language(string language, int n, string expected)
    {
        using var _ = Lang.Use(language);
        Assert.Equal(expected, Fmt.Years(n));
    }

    [Theory]
    [InlineData("en", "Free", "95,000 ₽", "1 h 30 min")]
    [InlineData("fr", "Gratuit", "95 000 ₽", "1 h 30 min")]
    [InlineData("de", "Kostenlos", "95.000 ₽", "1 Std. 30 Min.")]
    public void Money_and_durations_follow_the_language(string language, string free, string price, string duration)
    {
        using var _ = Lang.Use(language);
        Assert.Equal(free, Fmt.Money(0));
        Assert.Equal(price, Fmt.Money(95000));
        Assert.Equal(duration, Fmt.Minutes(90));
    }

    [Theory]
    [InlineData("ru", "1 октября 2026")]
    [InlineData("en", "1 October 2026")]
    [InlineData("fr", "1 octobre 2026")]
    [InlineData("de", "1. Oktober 2026")]
    public void Dates_follow_the_language(string language, string expected)
    {
        using var _ = Lang.Use(language);
        Assert.Equal(expected, Fmt.Date(new DateTime(2026, 10, 1)));
    }

    [Fact]
    public void Initials_come_from_first_two_words() => Assert.Equal("АК", Fmt.Initials("Анна Кузнецова"));
}
