using Progress.Domain.Constants;
using Progress.Domain.Entities;

namespace Progress.UnitTests.Metrics;

public sealed class BmiTests
{
    [Theory]
    [InlineData(70.0, 175.0, 22.86, BmiCategory.NormalWeight)]
    [InlineData(50.0, 170.0, 17.30, BmiCategory.Underweight)]
    [InlineData(85.0, 175.0, 27.76, BmiCategory.Overweight)]
    [InlineData(100.0, 170.0, 34.60, BmiCategory.Obese)]
    public void Create_CalculatesBmiAndCategoryAccurately(
        double weightKg, double heightCm, double expectedBmi, string expectedCategory)
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow;

        var log = BodyMetricLog.Create(
            userId, loggedAt, (decimal)weightKg, (decimal)heightCm);

        Assert.Equal((decimal)expectedBmi, log.Bmi);
        Assert.Equal(expectedCategory, log.BmiCategory);
        Assert.Equal((decimal)heightCm, log.HeightUsedCm);
    }

    [Theory]
    [InlineData(24.9)]
    [InlineData(300.1)]
    public void Create_WeightOutOfBounds_ThrowsArgumentOutOfRangeException(double invalidWeight)
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BodyMetricLog.Create(userId, loggedAt, (decimal)invalidWeight, 170.0m));
    }

    [Fact]
    public void Create_BloodPressure_SingleFieldGiven_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            BodyMetricLog.Create(userId, loggedAt, 70.0m, 175.0m, systolicBloodPressure: 120, diastolicBloodPressure: null));

        Assert.Throws<ArgumentException>(() =>
            BodyMetricLog.Create(userId, loggedAt, 70.0m, 175.0m, systolicBloodPressure: null, diastolicBloodPressure: 80));
    }

    [Fact]
    public void Create_BloodPressure_SystolicNotGreaterThanDiastolic_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            BodyMetricLog.Create(userId, loggedAt, 70.0m, 175.0m, systolicBloodPressure: 110, diastolicBloodPressure: 110));

        Assert.Throws<ArgumentException>(() =>
            BodyMetricLog.Create(userId, loggedAt, 70.0m, 175.0m, systolicBloodPressure: 100, diastolicBloodPressure: 120));
    }

    [Fact]
    public void Create_ImmutableSnapshot_DoesNotChangeWithExternalValues()
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow;

        var log = BodyMetricLog.Create(userId, loggedAt, 70.0m, 175.0m);
        Assert.Equal(22.86m, log.Bmi);
        Assert.Equal(175.0m, log.HeightUsedCm);

        // Giả sử sau này người dùng đổi chiều cao thành 180cm trong Identity, bản ghi snapshot cũ vẫn giữ nguyên 175cm và 22.86 BMI
        Assert.Equal(22.86m, log.Bmi);
        Assert.Equal(175.0m, log.HeightUsedCm);
    }

    [Fact]
    public void Create_ClassifiesUsingUnroundedBmi_WhenRoundingCrossesThreshold()
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow;

        // Dùng Height = 200 cm (2m), HeightM^2 = 4m2:
        // Cần rawBmi = 24.996 => Weight = 24.996 * 4 = 99.984 kg
        // rawBmi = 99.984 / 4 = 24.996 < 25.0 (NormalWeight)
        // Math.Round(24.996, 2) = 25.00
        var testWeight = 99.984m;
        var testHeight = 200.0m;

        var log = BodyMetricLog.Create(userId, loggedAt, testWeight, testHeight);

        Assert.Equal(25.00m, log.Bmi);
        Assert.Equal(BmiCategory.NormalWeight, log.BmiCategory);
    }

    [Theory]
    // Ngưỡng 18.5 (Underweight / NormalWeight)
    [InlineData(73.984, 200.0, 18.50, BmiCategory.Underweight)]  // raw = 18.496 < 18.5, round = 18.50
    [InlineData(74.000, 200.0, 18.50, BmiCategory.NormalWeight)] // raw = 18.500 == 18.5, round = 18.50
    [InlineData(74.016, 200.0, 18.50, BmiCategory.NormalWeight)] // raw = 18.504 > 18.5, round = 18.50
    // Ngưỡng 25.0 (NormalWeight / Overweight)
    [InlineData(99.984, 200.0, 25.00, BmiCategory.NormalWeight)] // raw = 24.996 < 25.0, round = 25.00
    [InlineData(100.000, 200.0, 25.00, BmiCategory.Overweight)]  // raw = 25.000 == 25.0, round = 25.00
    [InlineData(100.016, 200.0, 25.00, BmiCategory.Overweight)]  // raw = 25.004 > 25.0, round = 25.00
    // Ngưỡng 30.0 (Overweight / Obese)
    [InlineData(119.984, 200.0, 30.00, BmiCategory.Overweight)]  // raw = 29.996 < 30.0, round = 30.00
    [InlineData(120.000, 200.0, 30.00, BmiCategory.Obese)]       // raw = 30.000 == 30.0, round = 30.00
    [InlineData(120.016, 200.0, 30.00, BmiCategory.Obese)]       // raw = 30.004 > 30.0, round = 30.00
    public void Create_BmiThresholdBoundaries_ClassifiesStrictlyByRawBmi(
        double weightKg, double heightCm, double expectedDisplayBmi, string expectedCategory)
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow;

        var log = BodyMetricLog.Create(userId, loggedAt, (decimal)weightKg, (decimal)heightCm);

        Assert.Equal((decimal)expectedDisplayBmi, log.Bmi);
        Assert.Equal(expectedCategory, log.BmiCategory);
    }
}
