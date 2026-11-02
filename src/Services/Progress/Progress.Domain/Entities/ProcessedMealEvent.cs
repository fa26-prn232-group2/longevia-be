namespace Progress.Domain.Entities;

public sealed class ProcessedMealEvent
{
    private ProcessedMealEvent() { }

    public Guid EventId { get; private set; }
    public Guid MealLogId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedMealEvent Create(
        Guid eventId, Guid mealLogId, Guid userId, DateOnly businessDate, DateTimeOffset processedAt)
    {
        return new ProcessedMealEvent
        {
            EventId = eventId,
            MealLogId = mealLogId,
            UserId = userId,
            BusinessDate = businessDate,
            ProcessedAt = processedAt
        };
    }
}
