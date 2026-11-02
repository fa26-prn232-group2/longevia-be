using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;

namespace Diet.Application.Features.Targets;

public sealed class TargetUseCases(ITargetRepository targets, ICurrentUser user, TimeProvider time,
    IProfileProvider profiles, IWeightProvider weights)
{
    public async Task<TargetResponse> GetAsync(CancellationToken ct) =>
        TargetResponse.From(await targets.GetAsync(user.RequiredId(), ct) ?? throw DietErrors.MissingTarget());

    public async Task<TargetResponse> CalculateAsync(UpdateTargetRequest request, CancellationToken ct)
    {
        var userId = user.RequiredId();
        var now = time.GetUtcNow();
        var profile = await profiles.GetAsync(userId, ct);
        var weight = await weights.GetLatestAsync(userId, now, ct)
            ?? throw Longevia.Shared.Errors.AppException.Unprocessable(
                "Bạn chưa ghi nhận cân nặng.", "diet.weight_not_found");
        var calculation = new EnergyCalculation(weight.WeightKg, weight.MeasuredAt,
            weight.SourceMetricId, weight.RequestedAsOf, profile.HeightCm,
            profile.AgeOn(DateOnly.FromDateTime(now.UtcDateTime)), profile.IsMale, profile.ActivityFactor, now);
        if (calculation.Bmr <= 0)
            throw Longevia.Shared.Errors.AppException.Unprocessable(
                "Hồ sơ chưa phù hợp để tính nhu cầu năng lượng.", "diet.profile_not_supported");
        var target = DailyDietTarget.CreateCalculated(userId, new NutritionValues(request.TargetCalories,
            request.TargetProteinGrams, request.TargetCarbsGrams, request.TargetFatGrams, 0), calculation);
        return TargetResponse.From(await targets.UpsertAsync(target, ct));
    }

    public async Task<TargetResponse> UpdateAsync(UpdateTargetRequest request, CancellationToken ct)
    {
        var target = DailyDietTarget.Create(user.RequiredId(), new NutritionValues(request.TargetCalories,
            request.TargetProteinGrams, request.TargetCarbsGrams, request.TargetFatGrams, 0), time.GetUtcNow());
        return TargetResponse.From(await targets.UpsertAsync(target, ct));
    }
}
