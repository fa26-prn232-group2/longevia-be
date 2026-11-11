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
            profile.AgeOn(Longevia.Shared.Time.BusinessTime.ToDate(now)), profile.IsMale, profile.ActivityFactor, now,
            profile.IsOther);
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

    /// <summary>
    /// Tính mục tiêu calo/macro từ TDEE và mục tiêu sức khỏe, lưu ngay.
    /// - Giảm cân: -15% TDEE; tăng cơ: +10%; duy trì/trường thọ: giữ TDEE.
    /// - Protein 1,8 g/kg (giảm cân/tăng cơ) hoặc 1,4 g/kg; fat 25% calo; carb phần còn lại.
    /// </summary>
    public async Task<TargetResponse> AutoAsync(AutoTargetRequest request, CancellationToken ct)
    {
        var userId = user.RequiredId();
        var now = time.GetUtcNow();
        var profile = await profiles.GetAsync(userId, ct);
        var weight = await weights.GetLatestAsync(userId, now, ct)
            ?? throw Longevia.Shared.Errors.AppException.Unprocessable(
                "Bạn chưa ghi nhận cân nặng.", "diet.weight_not_found");

        var goal = NormalizeGoal(string.IsNullOrWhiteSpace(request.Goal) ? profile.Goal : request.Goal);
        var calculation = new EnergyCalculation(weight.WeightKg, weight.MeasuredAt,
            weight.SourceMetricId, weight.RequestedAsOf, profile.HeightCm,
            profile.AgeOn(Longevia.Shared.Time.BusinessTime.ToDate(now)), profile.IsMale,
            profile.ActivityFactor, now, profile.IsOther);
        if (calculation.Bmr <= 0)
        {
            throw Longevia.Shared.Errors.AppException.Unprocessable(
                "Hồ sơ chưa phù hợp để tính nhu cầu năng lượng.", "diet.profile_not_supported");
        }

        var adjustment = goal switch
        {
            "LoseWeight" => 0.85m,
            "BuildMuscle" => 1.10m,
            _ => 1.0m,
        };
        var calories = Math.Clamp(
            Math.Round(calculation.Tdee * adjustment / 10m, MidpointRounding.AwayFromZero) * 10m,
            1200m, 4000m);
        var protein = Math.Clamp(
            Math.Round(weight.WeightKg * (goal is "LoseWeight" or "BuildMuscle" ? 1.8m : 1.4m),
                1, MidpointRounding.AwayFromZero),
            weight.WeightKg, weight.WeightKg * 2.4m);
        var fat = Math.Clamp(
            Math.Round(calories * 0.25m / 9m, 1, MidpointRounding.AwayFromZero),
            calories * 0.20m / 9m, calories * 0.35m / 9m);
        var carbs = Math.Max(0,
            Math.Round((calories - protein * 4m - fat * 9m) / 4m, 1, MidpointRounding.AwayFromZero));

        var target = DailyDietTarget.CreateCalculated(userId,
            new NutritionValues(calories, protein, carbs, fat, 0), calculation);
        return TargetResponse.From(await targets.UpsertAsync(target, ct));
    }

    private static string NormalizeGoal(string? goal) =>
        goal?.Trim().ToLowerInvariant() switch
        {
            "loseweight" or "lose_weight" => "LoseWeight",
            "buildmuscle" or "build_muscle" => "BuildMuscle",
            "longevity" => "Longevity",
            _ => "Maintain",
        };

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
                profile.ActivityFactor, now, profile.IsOther);
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
