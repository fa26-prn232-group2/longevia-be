using Recommendation.Application.Features.Meals;

namespace Recommendation.UnitTests.Features.Meals;

/// <summary>
/// T048 — rule chạm trần 105% calo (FR-004): đạt hoặc vượt 105% mục tiêu thì
/// không gợi ý bữa ăn chính.
/// </summary>
public sealed class CalorieCapRuleTests
{
    private readonly CalorieCapRule _rule = new();

    [Theory]
    [InlineData(2100, 2000)]
    [InlineData(2500, 2000)]
    [InlineData(1050, 1000)]
    public void BlocksMainMealWhenConsumedReachesCap(int consumedCalories, int targetCalories)
    {
        Assert.True(_rule.IsCapReached(consumedCalories, targetCalories));
    }

    [Theory]
    [InlineData(2099, 2000)]
    [InlineData(2000, 2000)]
    [InlineData(0, 2000)]
    [InlineData(1049, 1000)]
    public void AllowsMainMealWhenConsumedIsBelowCap(int consumedCalories, int targetCalories)
    {
        Assert.False(_rule.IsCapReached(consumedCalories, targetCalories));
    }

    [Fact]
    public void AllowsMainMealWhenTargetCaloriesIsNotProvided()
    {
        Assert.False(_rule.IsCapReached(2000, 0));
    }

    [Fact]
    public void ZeroCalorieSuggestionHasNoCaloriesAndNoCatalogIdentifier()
    {
        var suggestion = _rule.CreateZeroCalorieSuggestion();

        Assert.Equal(0, suggestion.EstimatedCalories);
        Assert.Equal(string.Empty, suggestion.FoodId);
        Assert.Empty(suggestion.Citations);
        Assert.False(string.IsNullOrWhiteSpace(suggestion.Rationale));
    }
}
