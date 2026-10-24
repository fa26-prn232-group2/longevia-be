namespace Diet.Application.Features.Meals;

/// <summary>Tổng hợp snapshot nhật ký theo ngày với mục tiêu hiện tại.</summary>
public sealed record DailySummaryResponse(DateOnly Date, decimal TargetCalories, decimal ConsumedCalories,
    decimal RemainingCalories, MacroSummary Macros, IReadOnlyList<MealSummary> Meals);
