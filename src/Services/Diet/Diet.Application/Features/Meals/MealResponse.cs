using Diet.Domain.Entities;

namespace Diet.Application.Features.Meals;

/// <summary>Nhật ký bữa ăn đã được lưu nguyên tử.</summary>
public sealed record MealResponse(Guid Id, DateOnly Date, string MealType,
    decimal TotalCalories, IReadOnlyList<MealItemResponse> Items)
{
    internal static MealResponse From(MealLog meal) => new(meal.Id, meal.Date, meal.MealType,
        meal.Items.Sum(x => x.Nutrition.Calories), meal.Items.Select(MealItemResponse.From).ToList());
}
