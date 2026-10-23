namespace Recommendation.Domain.Models;

/// <summary>
/// Một khoảng thiếu hụt vi chất so với nhu cầu khuyến nghị (RDA) của hồ sơ người dùng.
/// Giá trị được tính deterministic bằng code, không giao cho AI.
/// </summary>
public sealed record NutrientGap
{
    /// <summary>Tên vi chất, ví dụ <c>Vitamin D</c> hoặc <c>Omega-3</c>.</summary>
    public required string Nutrient { get; init; }

    /// <summary>Đơn vị đo: mg | g | IU.</summary>
    public required string Unit { get; init; }

    /// <summary>Nhu cầu theo nhóm tuổi và giới.</summary>
    public required double Target { get; init; }

    /// <summary>Lượng trung bình thực tế trong 7 ngày.</summary>
    public required double AverageIntake { get; init; }

    /// <summary>Mức thiếu hụt (<see cref="Target"/> trừ <see cref="AverageIntake"/>), không âm.</summary>
    public required double Deficit { get; init; }

    /// <summary>Mức độ: mild (&lt;25%) | moderate (25–50%) | severe (&gt;50%).</summary>
    public required string Severity { get; init; }
}
