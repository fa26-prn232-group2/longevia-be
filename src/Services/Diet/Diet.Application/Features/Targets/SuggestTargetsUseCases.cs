using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Microsoft.Extensions.Options;

namespace Diet.Application.Features.Targets;

/// <summary>
/// Gợi ý mục tiêu dinh dưỡng từ hồ sơ, cân nặng và mô tả của người dùng. Không
/// ghi DB: trả về để UI điền form, người dùng bấm Lưu mới persist qua PUT /targets/me.
/// </summary>
public sealed class SuggestTargetsUseCases(
    ITargetSuggestionProvider provider,
    IProfileProvider profiles,
    IWeightProvider weights,
    ITargetRepository targets,
    ICurrentUser user,
    TimeProvider time,
    IOptions<RecommendationBudgetOptions> budget)
{
    public async Task<SuggestedTargetsResponse> SuggestAsync(SuggestTargetsRequest request,
        CancellationToken ct)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length > 500)
        {
            throw AppException.BadRequest("description tối đa 500 ký tự.", "diet.invalid_description");
        }

        var userId = user.RequiredId();
        var now = time.GetUtcNow();
        var profile = await profiles.GetAsync(userId, ct);
        // Goal của hồ sơ là mặc định; request được phép override cho từng lần gợi ý.
        var goal = NormalizeGoal(string.IsNullOrWhiteSpace(request.Goal) ? profile.Goal : request.Goal);
        var weight = await weights.GetLatestAsync(userId, now, ct)
            ?? throw AppException.Unprocessable("Bạn chưa ghi nhận cân nặng.",
                "diet.weight_not_found");
        var current = await targets.GetAsync(userId, ct);

        var input = new TargetSuggestionInput(
            profile,
            weight.WeightKg,
            goal,
            description,
            current?.TargetCalories is { } calories ? (int)calories : 0);

        var result = await provider.SuggestAsync(input,
            now.AddMilliseconds(budget.Value.DeadlineMilliseconds), ct);

        return new SuggestedTargetsResponse(
            result.Calories,
            result.ProteinGrams,
            result.CarbsGrams,
            result.FatGrams,
            result.Rationale,
            result.Source,
            result.SafetyNote);
    }

    private static string NormalizeGoal(string? goal) =>
        goal?.Trim().ToLowerInvariant() switch
        {
            "loseweight" or "lose_weight" => "LOSE_WEIGHT",
            "buildmuscle" or "build_muscle" => "BUILD_MUSCLE",
            "longevity" => "LONGEVITY",
            _ => "MAINTAIN",
        };
}
