using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Application.Features.Meals;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Microsoft.Extensions.Options;

namespace Diet.Application.Features.Nutrition;

public sealed class NutrientGapUseCase(IMealRepository meals, IFoodRepository foods, IProfileProvider profiles,
    IWeightProvider weights, ITargetRepository targets, INutrientGapProvider provider, ICurrentUser user,
    TimeProvider time, IOptions<RecommendationBudgetOptions> budget)
{
    public async Task<NutrientGapResponse> ExecuteAsync(int days, CancellationToken ct)
    {
        if (days is < 1 or > 7)
            throw AppException.BadRequest("days phải nằm trong 1..7.", "diet.invalid_days");

        var userId = user.RequiredId();
        var now = time.GetUtcNow();
        var today = Longevia.Shared.Time.BusinessTime.ToDate(now);
        var from = today.AddDays(-(days - 1));
        var target = await targets.GetAsync(userId, ct) ?? throw DietErrors.MissingTarget();
        var profile = await profiles.GetAsync(userId, ct);
        var weight = await weights.GetLatestAsync(userId, now, ct)
            ?? throw AppException.Unprocessable("Bạn chưa ghi nhận cân nặng.", "diet.weight_not_found");
        var logs = await meals.GetRangeAsync(userId, from, today, ct);
        var foodIds = logs.SelectMany(x => x.Items).Select(x => x.FoodId).Distinct().ToArray();
        var foodMap = (await foods.GetByIdsAsync(foodIds, ct)).ToDictionary(x => x.Id);

        var daily = new List<DailyNutrientSummary>();
        var warnings = new List<string>();
        foreach (var group in logs.GroupBy(x => x.Date).OrderBy(x => x.Key))
        {
            var items = group.SelectMany(x => x.Items).ToArray();
            var summary = new DailyNutrientSummary
            {
                Date = group.Key.ToString("yyyy-MM-dd"),
                Calories = checked((int)decimal.Round(items.Sum(x => x.Nutrition.Calories), 0,
                    MidpointRounding.AwayFromZero)),
                Protein = (double)items.Sum(x => x.Nutrition.ProteinGrams),
                Carbs = (double)items.Sum(x => x.Nutrition.CarbsGrams),
                Fat = (double)items.Sum(x => x.Nutrition.FatGrams),
                Fiber = (double)items.Sum(x => x.Nutrition.FiberGrams),
            };

            var micros = new Dictionary<string, (decimal Amount, string Unit)>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (!foodMap.TryGetValue(item.FoodId, out var food) || food.Micronutrients is null ||
                    food.ServingSize <= 0) continue;
                var ratio = item.Quantity / food.ServingSize;
                foreach (var (nutrient, amount) in food.Micronutrients)
                {
                    var unit = UnitFor(nutrient);
                    if (micros.TryGetValue(nutrient, out var current) &&
                        !string.Equals(current.Unit, unit, StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add($"Bỏ qua {nutrient} vì đơn vị không thống nhất.");
                        continue;
                    }
                    micros[nutrient] = (current.Amount + amount * ratio, unit);
                }
            }
            foreach (var (nutrient, value) in micros)
            {
                summary.Micronutrients.Add(new MicronutrientIntake
                {
                    Nutrient = nutrient,
                    Unit = value.Unit,
                    Amount = (double)value.Amount,
                });
            }
            daily.Add(summary);
        }

        if (daily.Count == 0)
            warnings.Add("Chưa có nhật ký bữa ăn trong khoảng ngày đã chọn.");
        if (daily.Count < days)
            warnings.Add($"Chỉ có dữ liệu {daily.Count}/{days} ngày.");

        var request = new NutrientGapRequest
        {
            UserId = userId.ToString("D"),
            Profile = new UserProfile
            {
                Age = profile.AgeOn(today),
                Sex = profile.IsOther ? BiologicalSex.Other
                    : profile.IsMale ? BiologicalSex.Male : BiologicalSex.Female,
                HeightCm = (double)profile.HeightCm,
                WeightKg = (double)weight.WeightKg,
                ActivityLevel = profile.ActivityLevel,
                DietaryPreference = profile.DietaryPreference ?? "None",
            },
            TargetCalories = checked((int)decimal.Round(target.TargetCalories, 0, MidpointRounding.AwayFromZero)),
            TargetProteinGrams = (double)target.TargetProteinGrams,
            TargetCarbsGrams = (double)target.TargetCarbsGrams,
            TargetFatGrams = (double)target.TargetFatGrams,
            TargetFiberGrams = Math.Round((double)target.TargetCalories * 14d / 1000d, 1),
        };
        request.Profile.Allergies.AddRange(profile.Allergies ?? []);
        request.Past7Days.AddRange(daily);

        var deadline = time.GetUtcNow().AddMilliseconds(budget.Value.DeadlineMilliseconds);
        var result = await provider.AnalyzeAsync(request, deadline, ct);
        if (string.IsNullOrWhiteSpace(result.Disclaimer?.Text))
            throw new AppException("Phân tích thiếu disclaimer an toàn.", 502,
                "Dịch vụ gợi ý trả dữ liệu không hợp lệ.", "diet.ai_integration_failed");
        return NutrientGapMapper.From(result, warnings, daily.Count);
    }

    private static string UnitFor(string nutrient) => nutrient switch
    {
        "Vitamin A" or "Vitamin D" or "Vitamin K" or "Vitamin B9" or "Vitamin B12" or "Iodine" or "Selenium" => "µg",
        "Omega-3" or "Fiber" or "Protein" or "Carbohydrate" or "Fat" => "g",
        _ => "mg",
    };
}
