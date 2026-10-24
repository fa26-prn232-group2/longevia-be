namespace Diet.Domain;

public sealed record NutritionValues(
    decimal Calories, decimal ProteinGrams, decimal CarbsGrams,
    decimal FatGrams, decimal FiberGrams)
{
    public NutritionValues ForQuantity(decimal quantity, decimal servingSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(servingSize);
        var ratio = quantity / servingSize;
        return new(
            decimal.Round(Calories * ratio, 8), decimal.Round(ProteinGrams * ratio, 8),
            decimal.Round(CarbsGrams * ratio, 8), decimal.Round(FatGrams * ratio, 8),
            decimal.Round(FiberGrams * ratio, 8));
    }
}
