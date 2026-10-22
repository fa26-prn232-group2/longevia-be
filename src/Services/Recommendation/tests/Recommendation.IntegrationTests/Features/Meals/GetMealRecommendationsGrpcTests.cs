using Longevia.Recommendation.Contracts.V1;

namespace Recommendation.IntegrationTests.Features.Meals;

/// <summary>
/// T051 — gọi server thật qua <c>Grpc.Net.Client</c>: không món nào chứa thành
/// phần dị ứng, không quá 5 món, mọi <c>food_id</c> thuộc danh mục, calo lấy
/// đúng từ danh mục, luôn có disclaimer và cờ chạm trần 105%.
/// </summary>
public sealed class GetMealRecommendationsGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>
{
    private const string PeanutDishId = "f-peanut";
    private const string PeanutDishName = "Chè đậu phộng";

    [Fact]
    public async Task NeverReturnsMealContainingAllergen()
    {
        var request = BuildRequest(consumedCalories: 800);
        var catalog = request.CandidateFoods.ToDictionary(food => food.FoodId, StringComparer.Ordinal);

        var response = await fixture.Client.GetMealRecommendationsAsync(request);

        Assert.InRange(response.Meals.Count, 1, 5);
        Assert.All(response.Meals, meal =>
        {
            Assert.Contains(meal.FoodId, catalog.Keys);
            Assert.DoesNotContain("đậu phộng", meal.Name, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(catalog[meal.FoodId].Calories, meal.EstimatedCalories);
        });
        Assert.DoesNotContain(response.Meals, meal => string.Equals(meal.FoodId, PeanutDishId, StringComparison.Ordinal));

        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
        Assert.False(string.IsNullOrWhiteSpace(response.LongevityTip));

        Assert.DoesNotContain(
            fixture.CapturedPrompts,
            prompt => prompt.UserPrompt.Contains(PeanutDishName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetsCalorieCapFlagAndSkipsMainMealsAboveCap()
    {
        var request = BuildRequest(consumedCalories: 2200);

        var response = await fixture.Client.GetMealRecommendationsAsync(request);

        Assert.True(response.CalorieCapReached);
        Assert.InRange(response.Meals.Count, 0, 5);
        Assert.All(response.Meals, meal => Assert.Equal(string.Empty, meal.FoodId));
        Assert.All(response.Meals, meal => Assert.Equal(0, meal.EstimatedCalories));
        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
    }

    private static MealRecommendationRequest BuildRequest(int consumedCalories)
    {
        var request = new MealRecommendationRequest
        {
            UserId = "user-integration",
            MealType = MealType.Lunch,
            Goal = UserGoal.Longevity,
            Profile = new UserProfile
            {
                Age = 32,
                Sex = BiologicalSex.Female,
                HeightCm = 165,
                WeightKg = 58,
                ActivityLevel = "ModeratelyActive",
                DietaryPreference = "None",
            },
            ConsumedCalories = consumedCalories,
            TargetCalories = 2000,
            TargetProteinRemaining = 30,
            TargetCarbsRemaining = 60,
            TargetFatRemaining = 15,
            Locale = "vi-VN",
        };

        request.Profile.Allergies.Add("đậu phộng");

        request.CandidateFoods.AddRange(
        [
            new CandidateFood
            {
                FoodId = PeanutDishId,
                Name = PeanutDishName,
                ServingSize = "1",
                ServingUnit = "chén",
                Calories = 320,
                ProteinGrams = 9,
                CarbsGrams = 48,
                FatGrams = 11,
                FiberGrams = 4,
                Allergens = { "đậu phộng" },
            },
            new CandidateFood
            {
                FoodId = "f-rice",
                Name = "Cơm gạo lứt",
                ServingSize = "1",
                ServingUnit = "chén",
                Calories = 230,
                ProteinGrams = 5,
                CarbsGrams = 45,
                FatGrams = 2,
                FiberGrams = 4,
            },
            new CandidateFood
            {
                FoodId = "f-tofu",
                Name = "Đậu hũ sốt cà chua",
                ServingSize = "1",
                ServingUnit = "đĩa",
                Calories = 180,
                ProteinGrams = 14,
                CarbsGrams = 8,
                FatGrams = 9,
                FiberGrams = 2,
            },
            new CandidateFood
            {
                FoodId = "f-salmon",
                Name = "Cá hồi áp chảo",
                ServingSize = "120",
                ServingUnit = "g",
                Calories = 250,
                ProteinGrams = 26,
                CarbsGrams = 0,
                FatGrams = 15,
                FiberGrams = 0,
            },
            new CandidateFood
            {
                FoodId = "f-yogurt",
                Name = "Sữa chua Hy Lạp",
                ServingSize = "1",
                ServingUnit = "hộp",
                Calories = 120,
                ProteinGrams = 10,
                CarbsGrams = 6,
                FatGrams = 4,
                FiberGrams = 0,
                Allergens = { "sữa" },
            },
        ]);

        return request;
    }
}
