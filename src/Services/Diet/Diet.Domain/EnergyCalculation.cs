namespace Diet.Domain;

/// <summary>
/// Nhu cầu năng lượng theo Mifflin-St Jeor. Với giới tính "Other", dùng hệ số trung
/// tính (-78) giữa nam (+5) và nữ (-161); kết quả chỉ là ước lượng và UI ghi rõ điều này.
/// </summary>
public sealed record EnergyCalculation(decimal WeightKg, DateTimeOffset WeightMeasuredAt,
    Guid SourceMetricId, DateTimeOffset WeightRequestedAsOf, decimal HeightCm,
    int AgeYears, bool IsMale, decimal ActivityFactor, DateTimeOffset CalculatedAt,
    bool IsOther = false)
{
    public decimal Bmr { get; private set; } = 10m * WeightKg + 6.25m * HeightCm - 5m * AgeYears
        + (IsMale ? 5m : IsOther ? -78m : -161m);

    public decimal Tdee { get; private set; } = (10m * WeightKg + 6.25m * HeightCm - 5m * AgeYears
        + (IsMale ? 5m : IsOther ? -78m : -161m)) * ActivityFactor;

    public void Validate()
    {
        if (WeightKg is < 25m or > 300m || HeightCm is < 50m or > 250m ||
            AgeYears is < 1 or > 120 || ActivityFactor is < 1m or > 3m || Bmr <= 0 ||
            decimal.Round(WeightKg, 2) != WeightKg || decimal.Round(HeightCm, 2) != HeightCm ||
            decimal.Round(ActivityFactor, 4) != ActivityFactor || SourceMetricId == Guid.Empty ||
            WeightMeasuredAt > WeightRequestedAsOf ||
            WeightRequestedAsOf > CalculatedAt)
        {
            throw new ArgumentException("Dữ liệu tính nhu cầu năng lượng không hợp lệ.");
        }
    }
}
