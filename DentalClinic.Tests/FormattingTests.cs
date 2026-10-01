using DentalClinic.Infrastructure;

namespace DentalClinic.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData(0, "Бесплатно")]
    [InlineData(5500, "5 500 ₽")]
    [InlineData(95000, "95 000 ₽")]
    public void Money_uses_russian_grouping(double value, string expected) => Assert.Equal(expected, Fmt.Money(value));

    [Theory]
    [InlineData(1, "1 год")]
    [InlineData(3, "3 года")]
    [InlineData(5, "5 лет")]
    [InlineData(11, "11 лет")]
    [InlineData(12, "12 лет")]
    [InlineData(21, "21 год")]
    public void Years_declension(int n, string expected) => Assert.Equal(expected, Fmt.Years(n));

    [Theory]
    [InlineData(30, "30 мин")]
    [InlineData(60, "1 ч")]
    [InlineData(90, "1 ч 30 мин")]
    [InlineData(120, "2 ч")]
    public void Minutes_are_humanised(int minutes, string expected) => Assert.Equal(expected, Fmt.Minutes(minutes));

    [Fact]
    public void Initials_come_from_first_two_words() => Assert.Equal("АК", Fmt.Initials("Анна Кузнецова"));
}
