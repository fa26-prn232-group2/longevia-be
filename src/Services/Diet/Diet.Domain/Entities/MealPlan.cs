namespace Diet.Domain.Entities;

/// <summary>
/// Kế hoạch ăn uống theo tuần đã lưu. Nội dung chi tiết (ngày/bữa/món/citation)
/// được giữ dưới dạng JSON snapshot để bảo toàn đúng dữ liệu AI đã trả tại thời
/// điểm tạo; không tính lại khi đọc.
/// </summary>
public sealed class MealPlan
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly WeekStartDate { get; private set; }
    public int Days { get; private set; }
    public int MealsPerDay { get; private set; }
    public string PlanJson { get; private set; } = "{}";
    public DateTimeOffset GeneratedAt { get; private set; }

    private MealPlan() { }

    public static MealPlan Create(Guid userId, DateOnly weekStartDate, int days, int mealsPerDay,
        string planJson, DateTimeOffset generatedAt)
    {
        if (weekStartDate.DayOfWeek != DayOfWeek.Monday)
            throw new ArgumentException("Tuần phải bắt đầu từ thứ Hai.", nameof(weekStartDate));
        if (days is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(days));
        if (mealsPerDay is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(mealsPerDay));
        ArgumentException.ThrowIfNullOrWhiteSpace(planJson);
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WeekStartDate = weekStartDate,
            Days = days,
            MealsPerDay = mealsPerDay,
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
