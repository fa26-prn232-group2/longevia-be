using Progress.Application.Features.Dashboard;

namespace Progress.UnitTests.Dashboard;

public sealed class StreakTests
{
    [Fact]
    public void Calculate_Active1_2_3_5_ReturnsCurrent1AndBest3()
    {
        // Giả sử hôm nay là ngày 5 (2026-10-05)
        var today = new DateOnly(2026, 10, 5);
        var activeDates = new HashSet<DateOnly>
        {
            new(2026, 10, 1),
            new(2026, 10, 2),
            new(2026, 10, 3),
            // Ngày 4 nghỉ
            new(2026, 10, 5)
        };

        var (current, best) = StreakCalculator.Calculate(activeDates, today);

        // Ngày 1,2,3 là chuỗi dài nhất 3 ngày => best = 3
        // Ngày 5 hôm nay active, ngày 4 không active => current = 1
        Assert.Equal(1, current);
        Assert.Equal(3, best);
    }

    [Fact]
    public void Calculate_ActiveConsecutiveUntilYesterday_CurrentStreakAnchoredToYesterday()
    {
        var today = new DateOnly(2026, 10, 5);
        var activeDates = new HashSet<DateOnly>
        {
            new(2026, 10, 2),
            new(2026, 10, 3),
            new(2026, 10, 4) // Hôm qua active, hôm nay chưa active
        };

        var (current, best) = StreakCalculator.Calculate(activeDates, today);

        // Chuỗi kéo dài đến hôm qua = 3 ngày
        Assert.Equal(3, current);
        Assert.Equal(3, best);
    }

    [Fact]
    public void Calculate_GapTwoDays_CurrentStreakBecomes0()
    {
        var today = new DateOnly(2026, 10, 5);
        var activeDates = new HashSet<DateOnly>
        {
            new(2026, 10, 1),
            new(2026, 10, 2),
            new(2026, 10, 3) // Gap ngày 4 và 5
        };

        var (current, best) = StreakCalculator.Calculate(activeDates, today);

        Assert.Equal(0, current);
        Assert.Equal(3, best);
    }

    [Fact]
    public void Calculate_EmptyDates_Returns0And0()
    {
        var today = new DateOnly(2026, 10, 5);
        var (current, best) = StreakCalculator.Calculate(new HashSet<DateOnly>(), today);

        Assert.Equal(0, current);
        Assert.Equal(0, best);
    }
}
