using Recommendation.Domain.Constants;

namespace Recommendation.UnitTests.Features.WorkoutPlans;

public sealed class WorkoutActivitiesLabelsTests
{
    [Theory]
    [InlineData("walking", "Đi bộ")]
    [InlineData("running", "Chạy bộ")]
    [InlineData("gardening", "Làm vườn")]
    [InlineData("tai_chi", "Thái cực quyền")]
    public void DisplayNameFor_KnownKey_ReturnsVietnameseLabel(string key, string expected)
    {
        Assert.Equal(expected, WorkoutActivities.DisplayNameFor(key));
    }

    [Fact]
    public void DisplayNameFor_UnknownKey_FallsBackToKey()
    {
        Assert.Equal("stretching", WorkoutActivities.DisplayNameFor("stretching"));
        Assert.Equal(string.Empty, WorkoutActivities.DisplayNameFor(null));
    }
}
