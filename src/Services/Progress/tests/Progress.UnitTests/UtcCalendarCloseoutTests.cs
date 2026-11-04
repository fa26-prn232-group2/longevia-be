using Progress.Domain;

namespace Progress.UnitTests;

public sealed class UtcCalendarCloseoutTests
{
    [Theory]
    [InlineData("2026-11-04T23:59:59Z", "2026-11-04")]
    [InlineData("2026-11-05T00:00:00Z", "2026-11-05")]
    [InlineData("2026-11-05T00:30:00+07:00", "2026-11-04")]
    [InlineData("2026-11-05T07:00:00+07:00", "2026-11-05")]
    public void Day_ApprovedUtcCalendar_IgnoresInputOffset(string instant, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), UtcCalendar.Day(DateTimeOffset.Parse(instant)));
    }

    [Theory]
    [InlineData("2026-11-08T23:59:59Z", "2026-11-02T00:00:00Z")]
    [InlineData("2026-11-09T00:00:00Z", "2026-11-09T00:00:00Z")]
    [InlineData("2026-11-09T00:30:00+07:00", "2026-11-02T00:00:00Z")]
    public void StartOfWeek_MondayUtcBoundary_DoesNotUseVietnamMonday(string instant, string expected)
    {
        Assert.Equal(DateTimeOffset.Parse(expected), UtcCalendar.StartOfWeek(DateTimeOffset.Parse(instant)));
    }
}
