using Recommendation.Application.Features.WorkoutPlans;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T058 — FR-031: bảng ánh xạ đóng limitation → hoạt động bị loại; token không
/// khớp bị bỏ qua kèm <c>unsupported_limitation</c>; không suy diễn y tế ngoài
/// danh sách caller gửi.
/// </summary>
public sealed class WorkoutLimitationRulesTests
{
    private readonly WorkoutLimitationRules _rules = new();

    [Fact]
    public void JointPainExcludesHighImpactActivities()
    {
        var result = _rules.Apply(["đau khớp gối"]);

        Assert.Contains("running", result.ExcludedActivityKeys);
        Assert.Contains("dancing", result.ExcludedActivityKeys);
        Assert.Contains("strength", result.ExcludedActivityKeys);
        Assert.Contains("gardening", result.ExcludedActivityKeys);
        Assert.DoesNotContain("walking", result.ExcludedActivityKeys);
        Assert.DoesNotContain("cycling", result.ExcludedActivityKeys);
        Assert.DoesNotContain("swimming", result.ExcludedActivityKeys);
        Assert.Empty(result.UnsupportedLimitations);
    }

    [Theory]
    [InlineData("Đau Khớp Gối")]
    [InlineData("  đau khớp gối  ")]
    [InlineData("đau khớp")]
    [InlineData("đau đầu gối")]
    [InlineData("knee pain")]
    [InlineData("joint injury")]
    public void MatchingIsNormalizedAndCaseInsensitive(string limitation)
    {
        var result = _rules.Apply([limitation]);

        Assert.Contains("running", result.ExcludedActivityKeys);
        Assert.Empty(result.UnsupportedLimitations);
        Assert.Single(result.AppliedLimitations);
    }

    [Fact]
    public void UnknownLimitationIsReportedWithoutInferringAnything()
    {
        var result = _rules.Apply(["bệnh tim bẩm sinh"]);

        Assert.Empty(result.ExcludedActivityKeys);
        Assert.Single(result.UnsupportedLimitations);
        Assert.Equal("bệnh tim bẩm sinh", result.UnsupportedLimitations[0]);
        Assert.Empty(result.AppliedLimitations);
    }

    [Fact]
    public void KnownAndUnknownLimitationsAreSeparated()
    {
        var result = _rules.Apply(["đau khớp gối", "bệnh tim"]);

        Assert.Contains("running", result.ExcludedActivityKeys);
        Assert.Single(result.UnsupportedLimitations);
        Assert.Single(result.AppliedLimitations);
    }

    [Fact]
    public void EmptyLimitationsProduceNoRuleAndNoWarning()
    {
        var result = _rules.Apply([]);

        Assert.Empty(result.ExcludedActivityKeys);
        Assert.Empty(result.UnsupportedLimitations);
        Assert.Empty(result.AppliedLimitations);
    }

    [Fact]
    public void NullLimitationsAreTreatedAsEmpty()
    {
        var result = _rules.Apply(null);

        Assert.Empty(result.ExcludedActivityKeys);
        Assert.Empty(result.UnsupportedLimitations);
    }

    [Fact]
    public void RuleKeysAreStableAndClosed()
    {
        Assert.Equal(
            new[] { "running", "dancing", "strength", "gardening" },
            _rules.ActivityKeysFor(WorkoutLimitationRules.JointPainRule));
        Assert.Empty(_rules.ActivityKeysFor("unknown_rule"));
    }
}
