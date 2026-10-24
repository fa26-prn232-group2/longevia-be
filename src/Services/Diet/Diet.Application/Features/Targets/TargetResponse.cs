using Diet.Domain.Entities;

namespace Diet.Application.Features.Targets;

/// <summary>Mục tiêu hiện tại; Bmr/Tdee null khi chưa có dữ liệu thể trạng.</summary>
public sealed record TargetResponse(Guid Id, decimal? Bmr, decimal? Tdee, decimal TargetCalories,
    decimal TargetProteinGrams, decimal TargetCarbsGrams, decimal TargetFatGrams, DateTimeOffset UpdatedAt)
{
    internal static TargetResponse From(DailyDietTarget target) => new(target.Id, null, null,
        target.TargetCalories, target.TargetProteinGrams, target.TargetCarbsGrams,
        target.TargetFatGrams, target.UpdatedAt);
}
