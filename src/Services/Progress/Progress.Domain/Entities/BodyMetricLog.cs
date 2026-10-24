using Progress.Domain.Constants;

namespace Progress.Domain.Entities;

public sealed class BodyMetricLog
{
    private BodyMetricLog() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset LoggedAt { get; private set; }
    public decimal WeightKg { get; private set; }
    public decimal HeightUsedCm { get; private set; }
    public decimal Bmi { get; private set; }
    public string BmiCategory { get; private set; } = null!;

    public decimal? BodyFatPercent { get; private set; }
    public decimal? WaistCircumferenceCm { get; private set; }
    public int? SystolicBloodPressure { get; private set; }
    public int? DiastolicBloodPressure { get; private set; }
    public int? RestingHeartRate { get; private set; }

    public static BodyMetricLog Create(
        Guid userId,
        DateTimeOffset loggedAt,
        decimal weightKg,
        decimal heightUsedCm,
        decimal? bodyFatPercent = null,
        decimal? waistCircumferenceCm = null,
        int? systolicBloodPressure = null,
        int? diastolicBloodPressure = null,
        int? restingHeartRate = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId không được để trống.", nameof(userId));
        }

        if (weightKg < 25.0m || weightKg > 300.0m)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKg), "Cân nặng phải từ 25.0 đến 300.0 kg.");
        }

        if (heightUsedCm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightUsedCm), "Chiều cao hồ sơ phải lớn hơn 0 để tính BMI.");
        }

        if (bodyFatPercent.HasValue && (bodyFatPercent.Value <= 0 || bodyFatPercent.Value > 100))
        {
            throw new ArgumentOutOfRangeException(nameof(bodyFatPercent), "Tỷ lệ mỡ cơ thể phải lớn hơn 0 và nhỏ hơn hoặc bằng 100%.");
        }

        if (waistCircumferenceCm.HasValue && (waistCircumferenceCm.Value <= 0 || waistCircumferenceCm.Value > 300))
        {
            throw new ArgumentOutOfRangeException(nameof(waistCircumferenceCm), "Vòng eo phải lớn hơn 0 và nhỏ hơn hoặc bằng 300 cm.");
        }

        var hasSystolic = systolicBloodPressure.HasValue;
        var hasDiastolic = diastolicBloodPressure.HasValue;
        if (hasSystolic != hasDiastolic)
        {
            throw new ArgumentException("Huyết áp tâm thu và tâm trương phải được ghi nhận cùng lúc.");
        }

        if (hasSystolic && hasDiastolic)
        {
            if (systolicBloodPressure!.Value < 50 || systolicBloodPressure.Value > 250)
            {
                throw new ArgumentOutOfRangeException(nameof(systolicBloodPressure), "Huyết áp tâm thu phải từ 50 đến 250 mmHg.");
            }

            if (diastolicBloodPressure!.Value < 30 || diastolicBloodPressure.Value > 150)
            {
                throw new ArgumentOutOfRangeException(nameof(diastolicBloodPressure), "Huyết áp tâm trương phải từ 30 đến 150 mmHg.");
            }

            if (systolicBloodPressure.Value <= diastolicBloodPressure.Value)
            {
                throw new ArgumentException("Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.");
            }
        }

        if (restingHeartRate.HasValue && (restingHeartRate.Value < 25 || restingHeartRate.Value > 250))
        {
            throw new ArgumentOutOfRangeException(nameof(restingHeartRate), "Nhịp tim lúc nghỉ phải từ 25 đến 250 bpm.");
        }

        // Tính toán BMI và phân loại: BMI = WeightKg / (HeightM * HeightM)
        var heightM = heightUsedCm / 100m;
        var rawBmi = weightKg / (heightM * heightM);
        var bmi = Math.Round(rawBmi, 2);
        // Q04: Phân loại theo giá trị chưa làm tròn (rawBmi), hiển thị/lưu tròn 2 chữ số (bmi)
        var category = Constants.BmiCategory.FromBmi(rawBmi);

        return new BodyMetricLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LoggedAt = loggedAt,
            WeightKg = weightKg,
            HeightUsedCm = heightUsedCm,
            Bmi = bmi,
            BmiCategory = category,
            BodyFatPercent = bodyFatPercent,
            WaistCircumferenceCm = waistCircumferenceCm,
            SystolicBloodPressure = systolicBloodPressure,
            DiastolicBloodPressure = diastolicBloodPressure,
            RestingHeartRate = restingHeartRate
        };
    }
}
