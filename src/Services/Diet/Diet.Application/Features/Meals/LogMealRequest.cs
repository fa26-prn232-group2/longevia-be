namespace Diet.Application.Features.Meals;

/// <summary>Nhật ký một lần ghi bữa ăn; UserId lấy từ JWT.</summary>
public sealed record LogMealRequest(DateOnly Date, string MealType, IReadOnlyList<LogMealItemRequest> Items);
