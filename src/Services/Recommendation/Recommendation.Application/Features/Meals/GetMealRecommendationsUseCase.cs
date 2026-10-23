using Recommendation.Domain.Constants;

namespace Recommendation.Application.Features.Meals;

/// <summary>
/// Use case gợi ý bữa ăn (FR-001..FR-007): lọc dị ứng → lọc sở thích → rule
/// 105% calo → chọn tối đa 5 món bằng mô hình **trong tập đã lọc** → gắn lý do
/// và trích dẫn, kèm mẹo trường thọ và khuyến cáo bắt buộc.
/// </summary>
public sealed class GetMealRecommendationsUseCase(
    AllergenGuard allergenGuard,
    DietaryPreferenceFilter dietaryPreferenceFilter,
    CalorieCapRule calorieCapRule,
    MealSelector mealSelector)
{
    private const string EmptyCatalogNote =
        "Bạn chưa gửi danh mục món ăn ứng viên nào nên chưa thể gợi ý bữa ăn.";

    private const string NoSafeCandidateNote =
        "Danh mục bạn gửi không còn món nào an toàn sau khi lọc dị ứng và sở thích ăn uống, "
        + "nên hệ thống không gợi ý thay thế ngoài danh mục.";

    private const string CalorieCapNote =
        "Bạn đã ăn tới hoặc vượt 105% mục tiêu calo hôm nay nên hệ thống không gợi ý bữa ăn chính; "
        + "hãy chọn đồ uống không calo và ưu tiên thư giãn, ngủ đủ giấc.";

    private const string ProviderNote =
        "Dịch vụ AI tạm thời không phản hồi nên chưa thể chọn món; không có gợi ý nào được tạo ra.";

    /// <summary>Chạy toàn bộ pipeline gợi ý bữa ăn cho một lời gọi.</summary>
    public async Task<MealRecommendationResult> ExecuteAsync(
        MealRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CandidateFoods.Count == 0)
        {
            return EmptyResult(EmptyCatalogNote);
        }

        var safe = allergenGuard.Filter(request.CandidateFoods, request.Profile.Allergies);
        var preferred = dietaryPreferenceFilter.Filter(safe, request.Profile.DietaryPreference);

        if (calorieCapRule.IsCapReached(request.ConsumedCalories, request.TargetCalories))
        {
            return await BuildCappedResultAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (preferred.Count == 0)
        {
            return EmptyResult(NoSafeCandidateNote);
        }

        var outcome = await mealSelector.SelectAsync(request, preferred, cancellationToken).ConfigureAwait(false);
        if (outcome.Abstained)
        {
            return EmptyResult(ProviderNote);
        }

        var tip = await mealSelector.BuildTipAsync(request, LongevityTips.Default, cancellationToken)
            .ConfigureAwait(false);

        return new MealRecommendationResult
        {
            Meals = outcome.Meals,
            LongevityTip = tip.Text,
            TipCitations = tip.Citations,
            Disclaimer = DisclaimerText.Value,
        };
    }

    private async Task<MealRecommendationResult> BuildCappedResultAsync(
        MealRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var tip = await mealSelector.BuildTipAsync(request, LongevityTips.Relaxation, cancellationToken)
            .ConfigureAwait(false);

        return new MealRecommendationResult
        {
            Meals = [calorieCapRule.CreateZeroCalorieSuggestion()],
            LongevityTip = tip.Text,
            TipCitations = tip.Citations,
            Disclaimer = DisclaimerText.Value,
            CalorieCapReached = true,
            SafetyNote = CalorieCapNote,
        };
    }

    private static MealRecommendationResult EmptyResult(string safetyNote) => new()
    {
        Disclaimer = DisclaimerText.Value,
        SafetyNote = safetyNote,
    };
}
