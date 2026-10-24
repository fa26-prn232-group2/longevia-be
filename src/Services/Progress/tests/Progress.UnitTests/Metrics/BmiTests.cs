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
}
