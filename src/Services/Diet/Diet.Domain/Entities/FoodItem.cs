namespace Diet.Domain.Entities;

public sealed class FoodItem
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Name { get; private set; } = "";
    public string Category { get; private set; } = "";
    public decimal ServingSize { get; private set; }
    public string ServingUnit { get; private set; } = "";
    public NutritionValues Nutrition { get; private set; } = new(0, 0, 0, 0, 0);

    public string[]? Allergens { get; private set; }

    private FoodItem() { }

    public static FoodItem Create(Guid? userId, string name, string category,
        decimal servingSize, string servingUnit, NutritionValues nutrition, IReadOnlyList<string>? allergens = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(servingSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (nutrition.Calories < 0 || nutrition.ProteinGrams < 0 || nutrition.CarbsGrams < 0 ||
            nutrition.FatGrams < 0 || nutrition.FiberGrams < 0)
        {
            throw new ArgumentException("Dinh dưỡng không được âm.", nameof(nutrition));
        }
        if (allergens is not null && (allergens.Count > 32 ||
            allergens.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 50)))
        {
            throw new ArgumentException("Invalid allergen tags.", nameof(allergens));
        }
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Category = category.Trim(),
            ServingSize = servingSize,
            ServingUnit = servingUnit.Trim(),
            Nutrition = nutrition,
            Allergens = allergens?.Select(x => x.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
    }
}
