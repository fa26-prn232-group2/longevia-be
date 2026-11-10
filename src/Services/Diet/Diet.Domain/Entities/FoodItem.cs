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

    /// <summary>
    /// Giá trị vi chất theo 100 đơn vị serving, ví dụ "Vitamin D" → µg.
    /// Null/rỗng nghĩa là chưa có dữ liệu, không phải 0.
    /// </summary>
    public IReadOnlyDictionary<string, decimal>? Micronutrients { get; private set; }

    public bool IsDeleted { get; private set; }

    private FoodItem() { }

    public static FoodItem Create(Guid? userId, string name, string category,
        decimal servingSize, string servingUnit, NutritionValues nutrition, IReadOnlyList<string>? allergens = null,
        IReadOnlyDictionary<string, decimal>? micronutrients = null)
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
        if (micronutrients is not null && (micronutrients.Count > 64 ||
            micronutrients.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 60 || x.Value < 0)))
        {
            throw new ArgumentException("Invalid micronutrient values.", nameof(micronutrients));
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
            Micronutrients = micronutrients?.ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    public void Update(string name, string category, decimal servingSize, string servingUnit,
        NutritionValues nutrition, IReadOnlyList<string>? allergens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(servingSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Category = category.Trim();
        ServingSize = servingSize;
        ServingUnit = servingUnit.Trim();
        Nutrition = nutrition;
        Allergens = allergens?.Select(x => x.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
    }

    public void SoftDelete() => IsDeleted = true;
}
