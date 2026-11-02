using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;

namespace Diet.Application.Features.Meals;

public sealed class MealRecommendationsUseCase(IProfileProvider profiles, IWeightProvider weights,
    IMealRecommendationProvider recommendations, IFoodRepository foods, DailySummaryUseCase summaries,
    ICurrentUser user, TimeProvider time)
{
    public async Task<MealRecommendationsResponse> ExecuteAsync(string mealType, string goal, CancellationToken ct)
    {
        var meal = Array.IndexOf(MealTypes.All.ToArray(), mealType) + 1;
        var goalValue = goal switch { "LoseWeight" => 1, "Maintain" => 2, "BuildMuscle" => 3, "Longevity" => 4, _ => 0 };
        if (meal == 0 || goalValue == 0)
            throw AppException.BadRequest("mealType hoặc goal không hợp lệ.", "diet.invalid_recommendation_query");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromMilliseconds(2800));
        var deadline = time.GetUtcNow().AddMilliseconds(2800);
        try { return await ResolveAsync(meal, goalValue, deadline, budget.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AppException("Gợi ý bữa ăn quá thời gian chờ.", 504, "Dịch vụ phụ thuộc quá hạn.", "diet.ai_timeout");
        }
    }

    private async Task<MealRecommendationsResponse> ResolveAsync(int meal, int goal, DateTimeOffset deadline, CancellationToken ct)
    {
        var userId = user.RequiredId();
        var now = time.GetUtcNow();
        var summary = await summaries.ExecuteAsync(DateOnly.FromDateTime(now.UtcDateTime), ct);
        var profile = await profiles.GetAsync(userId, ct);
        if (profile.DietaryPreference is null || profile.Allergies is null)
            throw AppException.ServiceUnavailable("Hồ sơ chưa cung cấp thông tin chế độ ăn và dị ứng.", "diet.profile_incomplete");
        var weight = await weights.GetLatestAsync(userId, now, ct)
            ?? throw AppException.Unprocessable("Bạn chưa ghi nhận cân nặng.", "diet.weight_not_found");
        var candidates = await foods.GetRecommendationCandidatesAsync(userId, profile.Allergies, ct);
        if (candidates.Count == 0 && summary.ConsumedCalories < summary.TargetCalories * 1.05m)
            throw AppException.Unprocessable("Chưa có món đủ thông tin dị ứng để gợi ý.", "diet.no_safe_candidate");
        var result = await recommendations.GetAsync(new(userId, meal, goal,
            new(profile.AgeOn(DateOnly.FromDateTime(now.UtcDateTime)), profile.IsMale, profile.HeightCm,
                weight.WeightKg, profile.ActivityLevel, profile.DietaryPreference, profile.Allergies),
            checked((int)decimal.Round(summary.ConsumedCalories, 0, MidpointRounding.AwayFromZero)),
            checked((int)decimal.Round(summary.TargetCalories, 0, MidpointRounding.AwayFromZero)),
            summary.Macros.Protein.Remaining, summary.Macros.Carbs.Remaining, summary.Macros.Fat.Remaining, candidates), deadline, ct);
        return new(summary.RemainingCalories, result.Meals.Select(MealSuggestionResponse.From).ToArray(), result.LongevityTip,
            result.TipCitations.Select(MealCitationResponse.From).ToArray(),
            result.Disclaimer, result.CalorieCapReached, result.SafetyNote);
    }
}
