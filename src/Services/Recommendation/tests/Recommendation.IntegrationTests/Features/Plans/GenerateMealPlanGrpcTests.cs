using Grpc.Core;
using Longevia.Recommendation.Contracts.V1;
using Recommendation.IntegrationTests.Features.Meals;

namespace Recommendation.IntegrationTests.Features.Plans;

/// <summary>
/// T031 — gọi server thật qua <c>Grpc.Net.Client</c> cho <c>GenerateMealPlan</c>:
/// đủ ngày/bữa, allergen, food_id, dinh dưỡng, rule 105%, fallback, corpus rỗng,
/// lỗi nghiệp vụ, metadata và safety. Fixture của lớp này không nạp corpus nên
/// mọi response đều đi nhánh <c>corpus_not_loaded</c>.
/// </summary>
public sealed class GenerateMealPlanGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>
{
    private const string PeanutDishId = "f-peanut";
    private const string PeanutDishName = "Chè đậu phộng";
    private const string WeekStart = "2026-10-26";
    private const string CappedDate = "2026-10-28";

    [Fact]
    public async Task ReturnsSevenDaysWithFourMealsPerDay()
    {
        var request = BuildRequest();

        var response = await fixture.Client.GenerateMealPlanAsync(request);

        Assert.Equal(7, response.Days.Count);
        Assert.Equal(WeekStart, response.WeekStartDate);
        Assert.Equal(7, response.DaysRequested);
        Assert.All(response.Days, day => Assert.Equal(4, day.Meals.Count));
        Assert.All(response.Days, day => Assert.All(day.Meals, meal => Assert.InRange(meal.Foods.Count, 1, 5)));
    }

    [Fact]
    public async Task NeverReturnsAllergenOrUnknownFood()
    {
        var request = BuildRequest();
        var catalog = request.CandidateFoods
            .Where(food => food.FoodId != PeanutDishId)
            .ToDictionary(food => food.FoodId, StringComparer.Ordinal);

        var response = await fixture.Client.GenerateMealPlanAsync(request);

        Assert.All(response.Days.SelectMany(day => day.Meals).SelectMany(meal => meal.Foods), food =>
        {
            Assert.Contains(food.FoodId, catalog.Keys);
            Assert.DoesNotContain("đậu phộng", string.Join(' ', food.Allergens), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(catalog[food.FoodId].Calories, food.Calories);
            Assert.Equal(catalog[food.FoodId].ProteinGrams, food.ProteinGrams, 3);
            Assert.Equal(catalog[food.FoodId].CarbsGrams, food.CarbsGrams, 3);
            Assert.Equal(catalog[food.FoodId].FatGrams, food.FatGrams, 3);
        });

        // Negative control T071: món chứa allergen không được xuất hiện cả trong
        // prompt gửi AI, không chỉ bị loại khỏi response.
        Assert.DoesNotContain(
            fixture.CapturedPrompts,
            prompt => prompt.UserPrompt.Contains(PeanutDishName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task KeepsEachDayWithinTenPercentOfTheDailyBudget()
    {
        var response = await fixture.Client.GenerateMealPlanAsync(BuildRequest());

        Assert.All(response.Days.Where(day => !day.CalorieCapReached), day =>
        {
            AssertTolerance(day.TargetCalories, day.TotalCalories);
            AssertTolerance(day.TargetProteinGrams, day.TotalProteinGrams);
            AssertTolerance(day.TargetCarbsGrams, day.TotalCarbsGrams);
            AssertTolerance(day.TargetFatGrams, day.TotalFatGrams);
        });
    }

    [Fact]
    public async Task CapsDayAtOneHundredFivePercent()
    {
        var request = BuildRequest(consumedCalories: 2100, consumedDate: CappedDate);

        var response = await fixture.Client.GenerateMealPlanAsync(request);

        var capped = Assert.Single(response.Days, day => day.CalorieCapReached);
        Assert.Equal(CappedDate, capped.Date);
        Assert.Empty(capped.Meals);
        Assert.Equal(0, capped.TotalCalories);
        Assert.InRange(capped.Suggestions.Count, 0, 3);
        Assert.All(response.Days.SelectMany(day => day.Meals).SelectMany(meal => meal.Foods),
            food => Assert.False(string.IsNullOrWhiteSpace(food.FoodId)));
        Assert.Contains(response.Warnings, warning => warning.Code == "suggestion_source_missing");
    }

    [Fact]
    public async Task FallsBackWhenProviderAbstains()
    {
        fixture.AbstainNextMealPlan();

        var response = await fixture.Client.GenerateMealPlanAsync(BuildRequest());

        Assert.True(response.UsedFallback);
        Assert.Contains(response.Warnings, warning => warning.Code == "ai_fallback_used");
        Assert.Equal(7, response.Days.Count);
        Assert.All(response.Days, day => Assert.NotEmpty(day.Meals));
    }

    [Fact]
    public async Task ReturnsEmptyCitationsAndCorpusWarningWhenCorpusIsEmpty()
    {
        var response = await fixture.Client.GenerateMealPlanAsync(BuildRequest());

        Assert.Empty(response.Citations);
        Assert.Contains(response.Warnings, warning => warning.Code == "corpus_not_loaded");
    }

    [Fact]
    public async Task RejectsNegativeFiberWithInvalidTargets()
    {
        var request = BuildRequest();
        request.TargetFiberGrams = -1;

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await fixture.Client.GenerateMealPlanAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Equal("recommendation.invalid_targets", exception.Trailers.GetValue("code"));
    }

    [Fact]
    public async Task RejectsNegativeConsumedCalories()
    {
        var request = BuildRequest(consumedCalories: -1, consumedDate: CappedDate);

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await fixture.Client.GenerateMealPlanAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Equal("recommendation.invalid_consumed_calories", exception.Trailers.GetValue("code"));
    }

    [Fact]
    public async Task RejectsCandidateWithoutName()
    {
        var request = BuildRequest();
        request.CandidateFoods[0].Name = string.Empty;

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await fixture.Client.GenerateMealPlanAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Equal("recommendation.invalid_candidate_food", exception.Trailers.GetValue("code"));
    }

    [Fact]
    public async Task AddsUnsupportedLocaleWarning()
    {
        var request = BuildRequest();
        request.Locale = "en-US";

        var response = await fixture.Client.GenerateMealPlanAsync(request);

        Assert.Contains(response.Warnings, warning => warning.Code == "unsupported_locale");
        Assert.Equal(7, response.Days.Count);
    }

    [Fact]
    public async Task ReturnsWeeklyTargetMetadata()
    {
        var response = await fixture.Client.GenerateMealPlanAsync(BuildRequest());

        Assert.Equal(14_000, response.WeeklyTargetCalories);
        Assert.Equal(700, response.WeeklyTargetProteinGrams, 3);
        Assert.Equal(1_750, response.WeeklyTargetCarbsGrams, 3);
        Assert.Equal(469, response.WeeklyTargetFatGrams, 3);
        Assert.Equal(210, response.WeeklyTargetFiberGrams, 3);
        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
    }

    [Fact]
    public async Task RationaleAndDisclaimerContainNoMedicalAdvice()
    {
        var response = await fixture.Client.GenerateMealPlanAsync(BuildRequest());

        var text = string.Join(
            ' ',
            response.Days
                .SelectMany(day => day.Meals)
                .Select(meal => meal.Rationale)
                .Append(response.Disclaimer.Text));

        Assert.DoesNotContain("chẩn đoán", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("điều trị", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liều thuốc", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kê đơn", text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertTolerance(double target, double actual)
    {
        if (target <= 0)
        {
            return;
        }

        var deviation = Math.Abs(actual - target) / target;
        Assert.True(
            deviation <= 0.10,
            $"Giá trị {actual} lệch {deviation:P1} so với mục tiêu {target}.");
    }

    private static GenerateMealPlanRequest BuildRequest(
        int consumedCalories = 0,
        string? consumedDate = null)
    {
        var request = new GenerateMealPlanRequest
        {
            Profile = new UserProfile
            {
                Age = 35,
                Sex = BiologicalSex.Female,
                HeightCm = 165,
                WeightKg = 60,
                ActivityLevel = "ModeratelyActive",
                DietaryPreference = "None",
            },
            TargetCalories = 2000,
            TargetProteinGrams = 100,
            TargetCarbsGrams = 250,
            TargetFatGrams = 67,
            TargetFiberGrams = 30,
            WeekStartDate = WeekStart,
            Days = 7,
            MealsPerDay = 4,
            ConsumedCalories = consumedCalories,
            ConsumedDate = consumedDate ?? string.Empty,
            Locale = "vi-VN",
        };

        request.Profile.Allergies.Add("đậu phộng");

        request.CandidateFoods.AddRange(
        [
            // Bốn món khớp đúng ngân sách từng bữa để danh mục đủ đa dạng và
            // phép kiểm tra ±10% có nghiệm; các món còn lại tạo dư địa chọn lựa.
            Food("f-meal-breakfast", "Bữa sáng cân bằng", 500, 25, 62.5, 16.75),
            Food("f-meal-lunch", "Bữa trưa cân bằng", 700, 35, 87.5, 23.45),
            Food("f-meal-dinner", "Bữa tối cân bằng", 600, 30, 75, 20.1),
            Food("f-meal-snack", "Bữa phụ cân bằng", 200, 10, 25, 6.7),
            Food("f-water", "Nước lọc", 0, 0, 0, 0),
            Food(PeanutDishId, "Chè đậu phộng", 320, 9, 48, 11, allergens: ["đậu phộng"]),
            Food("f-rice", "Cơm gạo lứt", 230, 5, 45, 2),
            Food("f-tofu", "Đậu hũ sốt cà", 180, 14, 8, 9),
            Food("f-salmon", "Cá hồi áp chảo", 250, 26, 0, 15),
            Food("f-chicken", "Ức gà luộc", 165, 31, 0, 4),
            Food("f-yogurt", "Sữa chua Hy Lạp", 120, 10, 6, 4),
            Food("f-oat", "Yến mạch", 150, 5, 27, 3),
            Food("f-egg", "Trứng luộc", 78, 6, 1, 5),
            Food("f-broccoli", "Bông cải xanh", 55, 4, 11, 1),
            Food("f-avocado", "Bơ", 160, 2, 9, 15),
            Food("f-banana", "Chuối", 105, 1, 27, 0),
            Food("f-beef", "Thịt bò nạc", 250, 26, 0, 15),
            Food("f-nuts", "Hạt óc chó", 185, 4, 4, 18),
            Food("f-bread", "Bánh mì nguyên cám", 120, 4, 22, 2),
            Food("f-tuna", "Cá ngừ", 130, 28, 0, 1),
            Food("f-milk", "Sữa tươi", 100, 8, 12, 2),
            Food("f-potato", "Khoai lang", 160, 4, 37, 0),
        ]);

        return request;
    }

    private static CandidateFood Food(
        string foodId,
        string name,
        int calories,
        double protein,
        double carbs,
        double fat,
        IReadOnlyList<string>? allergens = null)
    {
        var food = new CandidateFood
        {
            FoodId = foodId,
            Name = name,
            ServingSize = "1",
            ServingUnit = "phần",
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = 5,
        };

        if (allergens is not null)
        {
            food.Allergens.AddRange(allergens);
        }

        return food;
    }
}
