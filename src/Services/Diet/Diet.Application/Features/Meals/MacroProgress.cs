namespace Diet.Application.Features.Meals;

/// <summary>Lượng đã ăn, mục tiêu hiện tại và phần còn lại; remaining có thể âm.</summary>
public sealed record MacroProgress(decimal Consumed, decimal Target, decimal Remaining);
