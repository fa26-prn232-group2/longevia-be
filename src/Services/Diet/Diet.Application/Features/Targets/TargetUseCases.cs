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
            profile.AgeOn(Longevia.Shared.Time.BusinessTime.ToDate(now)), profile.IsMale, profile.ActivityFactor, now);
        if (calculation.Bmr <= 0)
            throw Longevia.Shared.Errors.AppException.Unprocessable(
                "Hồ sơ chưa phù hợp để tính nhu cầu năng lượng.", "diet.profile_not_supported");
        var target = DailyDietTarget.CreateCalculated(userId, new NutritionValues(request.TargetCalories,
            request.TargetProteinGrams, request.TargetCarbsGrams, request.TargetFatGrams, 0), calculation);
        return TargetResponse.From(await targets.UpsertAsync(target, ct));
    }

    public async Task<TargetResponse> UpdateAsync(UpdateTargetRequest request, CancellationToken ct)
    {
        var userId = user.RequiredId();
        var now = time.GetUtcNow();
        var nutrition = new NutritionValues(request.TargetCalories,
            request.TargetProteinGrams, request.TargetCarbsGrams, request.TargetFatGrams, 0);

        // Lưu thủ công vẫn đính kèm BMR/TDEE khi có hồ sơ + cân nặng để người dùng
        // luôn thấy chỉ số tham chiếu; thiếu dữ liệu thì lưu mục tiêu bình thường.
        var calculation = await TryBuildCalculationAsync(userId, now, ct);
        if (calculation is null)
        {
            calculation = (await targets.GetAsync(userId, ct))?.Calculation;
        }

        var target = calculation is null
            ? DailyDietTarget.Create(userId, nutrition, now)
            : DailyDietTarget.CreateCalculated(userId, nutrition, calculation);
        return TargetResponse.From(await targets.UpsertAsync(target, ct));
    }

    private async Task<EnergyCalculation?> TryBuildCalculationAsync(Guid userId, DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            var profileTask = profiles.GetAsync(userId, ct);
            var weightTask = weights.GetLatestAsync(userId, now, ct);
            await Task.WhenAll(profileTask, weightTask);

            var profile = await profileTask;
            var weight = await weightTask;
            if (weight is null)
            {
                return null;
            }

            var calculation = new EnergyCalculation(weight.WeightKg, weight.MeasuredAt,
                weight.SourceMetricId, weight.RequestedAsOf, profile.HeightCm,
                profile.AgeOn(Longevia.Shared.Time.BusinessTime.ToDate(now)), profile.IsMale,
                profile.ActivityFactor, now);
            return calculation.Bmr <= 0 ? null : calculation;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Longevia.Shared.Errors.AppException)
        {
            // Thiếu hồ sơ/cân nặng hoặc provider tạm lỗi: không chặn lưu mục tiêu.
            return null;
        }
    }
}
