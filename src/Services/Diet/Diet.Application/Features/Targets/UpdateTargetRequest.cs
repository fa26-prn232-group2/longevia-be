namespace Diet.Application.Features.Targets;

/// <summary>Mục tiêu calo/macro do người dùng nhập, chưa có BMR/TDEE tự động.</summary>
public sealed record UpdateTargetRequest(decimal TargetCalories, decimal TargetProteinGrams,
    decimal TargetCarbsGrams, decimal TargetFatGrams);
