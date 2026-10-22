namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Mục tiêu dinh dưỡng của người dùng trong lời gọi. Giá trị <c>0</c> nghĩa là
/// bên gọi không đặt mục tiêu riêng — calculator dùng bảng nhu cầu tham chiếu
/// theo nhóm tuổi và giới (<c>MicronutrientRda</c>).
/// </summary>
public sealed record NutrientTargets
{
    /// <summary>Mục tiêu năng lượng (kcal); <c>0</c> = dùng nhu cầu tham chiếu.</summary>
    public double TargetCalories { get; init; }

    /// <summary>Mục tiêu protein (g); <c>0</c> = dùng nhu cầu tham chiếu.</summary>
    public double TargetProteinGrams { get; init; }

    /// <summary>Mục tiêu carbohydrate (g); <c>0</c> = dùng nhu cầu tham chiếu.</summary>
    public double TargetCarbsGrams { get; init; }

    /// <summary>Mục tiêu chất béo (g); <c>0</c> = dùng nhu cầu tham chiếu.</summary>
    public double TargetFatGrams { get; init; }

    /// <summary>Mục tiêu chất xơ (g); <c>0</c> = dùng nhu cầu tham chiếu.</summary>
    public double TargetFiberGrams { get; init; }
}
