namespace Progress.Application.Features.Dashboard;

public static class StreakCalculator
{
    public static (int CurrentStreak, int BestStreak) Calculate(IReadOnlySet<DateOnly> activeDates, DateOnly today)
    {
        if (activeDates.Count == 0)
        {
            return (0, 0);
        }

        var sortedDates = activeDates.OrderBy(d => d).ToList();

        // 1. Best streak (chuỗi dài nhất trong lịch sử)
        int bestStreak = 0;
        int currentRun = 0;
        DateOnly? prev = null;

        foreach (var date in sortedDates)
        {
            if (prev == null)
            {
                currentRun = 1;
            }
            else if (date == prev.Value.AddDays(1))
            {
                currentRun++;
            }
            else
            {
                currentRun = 1;
            }

            if (currentRun > bestStreak)
            {
                bestStreak = currentRun;
            }

            prev = date;
        }

        // 2. Current streak
        // Q06: Nếu hôm nay active, streak kéo đến hôm nay; nếu hôm nay chưa active nhưng hôm qua active, streak neo ở hôm qua; nếu hôm qua không active (gap >= 2 ngày) trả 0.
        int currentStreak = 0;
        DateOnly checkDate;

        if (activeDates.Contains(today))
        {
            checkDate = today;
        }
        else if (activeDates.Contains(today.AddDays(-1)))
        {
            checkDate = today.AddDays(-1);
        }
        else
        {
            return (0, bestStreak);
        }

        while (activeDates.Contains(checkDate))
        {
            currentStreak++;
            checkDate = checkDate.AddDays(-1);
        }

        return (currentStreak, bestStreak);
    }
}
