namespace Diet.Application.Features.Meals;

/// <summary>
/// Thay thế toàn bộ món của một loại bữa trong ngày; ngày và loại bữa nằm ở query
/// string (`date`, `mealType`).
/// </summary>
public sealed record ReplaceMealRequest(IReadOnlyList<LogMealItemRequest> Items);
