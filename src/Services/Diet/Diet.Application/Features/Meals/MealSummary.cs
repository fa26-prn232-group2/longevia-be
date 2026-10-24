namespace Diet.Application.Features.Meals;

/// <summary>Tổng các lần ghi cùng loại bữa ăn trong ngày.</summary>
public sealed record MealSummary(string MealType, decimal TotalCalories, int ItemsCount);
