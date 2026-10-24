namespace Diet.Domain.Entities;

public sealed class MealLog
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public string MealType { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public List<MealLogItem> Items { get; private set; } = [];

    private MealLog() { }

    public static MealLog Create(Guid userId, DateOnly date, string mealType, DateTimeOffset now)
    {
        if (!MealTypes.All.Contains(mealType))
        {
            throw new ArgumentException("Loại bữa ăn không hợp lệ.", nameof(mealType));
        }

        // PostgreSQL persists microseconds; keep the original and replay event timestamps identical.
        now = now.AddTicks(-(now.Ticks % 10));
        return new() { Id = Guid.NewGuid(), UserId = userId, Date = date, MealType = mealType, CreatedAt = now };
    }

    public void Add(FoodItem food, decimal quantity) => Items.Add(MealLogItem.Create(Id, food, quantity));
}
