namespace Diet.Domain.Entities;

public sealed class DailyDietTarget
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal TargetCalories { get; private set; }
    public decimal TargetProteinGrams { get; private set; }
    public decimal TargetCarbsGrams { get; private set; }
    public decimal TargetFatGrams { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private DailyDietTarget() { }

    public static DailyDietTarget Create(Guid userId, NutritionValues target, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(target.Calories);
        if (target.ProteinGrams < 0 || target.CarbsGrams < 0 || target.FatGrams < 0 ||
            Math.Abs(target.ProteinGrams * 4 + target.CarbsGrams * 4 + target.FatGrams * 9
                - target.Calories) > target.Calories * 0.05m)
        {
            throw new ArgumentException("Năng lượng từ macro phải nằm trong ±5% mục tiêu.", nameof(target));
        }

        return new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetCalories = target.Calories,
            TargetProteinGrams = target.ProteinGrams,
            TargetCarbsGrams = target.CarbsGrams,
            TargetFatGrams = target.FatGrams,
            UpdatedAt = now,
        };
    }
}
