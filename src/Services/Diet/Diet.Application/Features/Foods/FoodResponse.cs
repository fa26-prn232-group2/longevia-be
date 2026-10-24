using Diet.Domain.Entities;

namespace Diet.Application.Features.Foods;

/// <summary>Thông tin dinh dưỡng theo ServingSize/ServingUnit của món.</summary>
public sealed record FoodResponse(Guid Id, string Name, string Category,
    decimal ServingSize, string ServingUnit, decimal Calories, decimal ProteinGrams,
    decimal CarbsGrams, decimal FatGrams, decimal FiberGrams, bool IsCustom)
{
    internal static FoodResponse From(FoodItem food) => new(food.Id, food.Name, food.Category,
        food.ServingSize, food.ServingUnit, food.Nutrition.Calories, food.Nutrition.ProteinGrams,
        food.Nutrition.CarbsGrams, food.Nutrition.FatGrams, food.Nutrition.FiberGrams, food.UserId.HasValue);
}
