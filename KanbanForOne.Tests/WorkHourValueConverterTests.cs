using KanbanForOne.Services;

namespace KanbanForOne.Tests;

public sealed class WorkHourValueConverterTests
{
    [Theory]
    [InlineData(" ab 12 ", "AB12")]
    [InlineData("a\tb\r\n01", "AB01")]
    [InlineData(" 项 目 a-1 ", "项目A-1")]
    public void NormalizeProjectNumber_removes_whitespace_and_uppercases_letters(string input, string expected)
    {
        Assert.Equal(expected, WorkHourValueConverter.NormalizeProjectNumber(input));
    }

    [Theory]
    [InlineData("0.5", 50)]
    [InlineData("1.25", 125)]
    [InlineData("8", 800)]
    [InlineData("0,33", 33)]
    public void TryParseHours_accepts_positive_values_with_up_to_two_decimals(string input, int expectedUnits)
    {
        Assert.True(WorkHourValueConverter.TryParseHours(input, out var units));
        Assert.Equal(expectedUnits, units);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.234")]
    [InlineData("24.01")]
    [InlineData("text")]
    public void TryParseHours_rejects_invalid_values(string input)
    {
        Assert.False(WorkHourValueConverter.TryParseHours(input, out _));
    }
}
