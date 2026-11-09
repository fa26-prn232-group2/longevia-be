namespace Progress.Domain.Entities;

/// <summary>Kế hoạch tập luyện tuần đã lưu dưới dạng JSON snapshot.</summary>
public sealed class WorkoutPlan
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly WeekStartDate { get; private set; }
    public int Days { get; private set; }
    public int SessionsPerWeek { get; private set; }
    public int MinutesPerSession { get; private set; }
    public string Goal { get; private set; } = "";
    public string PlanJson { get; private set; } = "{}";
    public DateTimeOffset GeneratedAt { get; private set; }

    private WorkoutPlan() { }

    public static WorkoutPlan Create(Guid userId, DateOnly weekStartDate, int days, int sessionsPerWeek,
        int minutesPerSession, string goal, string planJson, DateTimeOffset generatedAt)
    {
        if (weekStartDate.DayOfWeek != DayOfWeek.Monday)
            throw new ArgumentException("Tuần phải bắt đầu từ thứ Hai.", nameof(weekStartDate));
        if (days is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(days));
        if (sessionsPerWeek is < 1 or > 7 || sessionsPerWeek > days)
            throw new ArgumentOutOfRangeException(nameof(sessionsPerWeek));
        if (minutesPerSession is < 10 or > 120) throw new ArgumentOutOfRangeException(nameof(minutesPerSession));
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(planJson);
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WeekStartDate = weekStartDate,
            Days = days,
            SessionsPerWeek = sessionsPerWeek,
            MinutesPerSession = minutesPerSession,
            Goal = goal,
            PlanJson = planJson,
            GeneratedAt = generatedAt,
        };
    }

    public void Replace(string planJson, DateTimeOffset generatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planJson);
        PlanJson = planJson;
        GeneratedAt = generatedAt;
    }
}
