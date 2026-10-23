using Recommendation.Application.Features.Plans;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T014 — ngày của kế hoạch là các ngày ISO liên tiếp bắt đầu từ
/// <c>week_start_date</c>, kể cả khi vắt qua tháng hoặc năm.
/// </summary>
public sealed class PlanDateRangeTests
{
    private readonly PlanDateRange _dateRange = new();

    [Fact]
    public void SingleDayReturnsOnlyWeekStart()
    {
        var dates = _dateRange.Build("2026-10-26", 1);

        Assert.Equal(["2026-10-26"], dates);
    }

    [Fact]
    public void SevenDaysAreConsecutive()
    {
        var dates = _dateRange.Build("2026-10-26", 7);

        Assert.Equal(
            [
                "2026-10-26",
                "2026-10-27",
                "2026-10-28",
                "2026-10-29",
                "2026-10-30",
                "2026-10-31",
                "2026-11-01",
            ],
            dates);
    }

    [Fact]
    public void RangeCrossesMonthAndYearBoundaries()
    {
        var dates = _dateRange.Build("2025-12-29", 7);

        Assert.Equal(
            [
                "2025-12-29",
                "2025-12-30",
                "2025-12-31",
                "2026-01-01",
                "2026-01-02",
                "2026-01-03",
                "2026-01-04",
            ],
            dates);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-1-5")]
    [InlineData("26/10/2026")]
    public void InvalidWeekStartThrows(string weekStart)
    {
        Assert.Throws<ArgumentException>(() => _dateRange.Build(weekStart, 7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveDayCountThrows(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _dateRange.Build("2026-10-26", days));
    }
}
