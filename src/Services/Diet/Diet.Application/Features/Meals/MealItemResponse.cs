using Diet.Domain.Entities;

namespace Diet.Application.Features.Meals;

/// <summary>Snapshot dinh dưỡng của lượng thực tế đã ăn.</summary>
public sealed record MealItemResponse(Guid FoodId, string Name, string ServingUnit, decimal Quantity,
    decimal Calories, decimal ProteinGrams, decimal CarbsGrams, decimal FatGrams, decimal FiberGrams)
{
    internal static MealItemResponse From(MealLogItem item) => new(item.FoodId, item.Name, item.ServingUnit,
        item.Quantity, item.Nutrition.Calories, item.Nutrition.ProteinGrams, item.Nutrition.CarbsGrams,
        item.Nutrition.FatGrams, item.Nutrition.FiberGrams);
}
