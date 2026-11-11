namespace Progress.Domain.Entities;

/// <summary>
/// Hộp thư idempotency cho sự kiện xoá bữa ăn, đồng thời là "bia mộ" (tombstone)
/// để một <c>MealLoggedEvent</c> đến trễ không làm sống lại đóng góp hoạt động.
/// </summary>
public sealed class ProcessedMealRemoval
{
    private ProcessedMealRemoval() { }

    public Guid EventId { get; private set; }
    public Guid MealLogId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedMealRemoval Create(Guid eventId, Guid mealLogId, Guid userId,
        DateOnly businessDate, DateTimeOffset processedAt) =>
        new()
        {
            EventId = eventId,
            MealLogId = mealLogId,
            UserId = userId,
            BusinessDate = businessDate,
            ProcessedAt = processedAt
        };
}
