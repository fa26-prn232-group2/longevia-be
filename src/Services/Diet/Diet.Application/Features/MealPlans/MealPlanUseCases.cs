using System.Text.Json;
using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Application.Features.Meals;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;

namespace Diet.Application.Features.MealPlans;

public sealed class MealPlanUseCases(IMealPlanRepository plans, IMealPlanProvider provider,
    IFoodRepository foods, IProfileProvider profiles, IWeightProvider weights, ITargetRepository targets,
    DailySummaryUseCase summaries, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider time,
    Microsoft.Extensions.Options.IOptions<RecommendationBudgetOptions> budget)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MealPlanResponse> GenerateAsync(GenerateMealPlanRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(request.WeekStartDate, "yyyy-MM-dd", out var weekStart) ||
            weekStart.DayOfWeek != DayOfWeek.Monday)
            throw AppException.BadRequest("weekStartDate phải là thứ Hai theo ISO yyyy-MM-dd.",
                "diet.invalid_week_start");
        if (request.Days is < 1 or > 7)
            throw AppException.BadRequest("days phải nằm trong 1..7.", "diet.invalid_days");
        if (request.MealsPerDay is < 1 or > 4)
            throw AppException.BadRequest("mealsPerDay phải nằm trong 1..4.", "diet.invalid_meals_per_day");

        var userId = user.RequiredId();
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var profile = await profiles.GetAsync(userId, ct);
        if (profile.DietaryPreference is null || profile.Allergies is null)
            throw AppException.ServiceUnavailable("Hồ sơ chưa cung cấp chế độ ăn và dị ứng.",
                "diet.profile_incomplete");
        var weight = await weights.GetLatestAsync(userId, now, ct)
            ?? throw AppException.Unprocessable("Bạn chưa ghi nhận cân nặng.", "diet.weight_not_found");
        var target = await targets.GetAsync(userId, ct) ?? throw DietErrors.MissingTarget();
        var candidates = await foods.GetRecommendationCandidatesAsync(userId, profile.Allergies, ct);
        if (candidates.Count == 0)
            throw AppException.Unprocessable("Chưa có món đủ thông tin dị ứng để lập kế hoạch.",
                "diet.no_safe_candidate");

        var consumedCalories = 0;
        string? consumedDate = null;
        var weekEnd = weekStart.AddDays(request.Days - 1);
        if (today >= weekStart && today <= weekEnd)
        {
            var summary = await summaries.ExecuteAsync(today, ct);
            consumedCalories = checked((int)decimal.Round(summary.ConsumedCalories, 0,
                MidpointRounding.AwayFromZero));
            consumedDate = today.ToString("yyyy-MM-dd");
        }

        var input = new MealPlanInput(userId,
            new MealProfile(profile.AgeOn(today), profile.IsMale, profile.HeightCm, weight.WeightKg,
                profile.ActivityLevel, profile.DietaryPreference, profile.Allergies),
            checked((int)decimal.Round(target.TargetCalories, 0, MidpointRounding.AwayFromZero)),
            (double)target.TargetProteinGrams, (double)target.TargetCarbsGrams, (double)target.TargetFatGrams,
            Math.Round((double)target.TargetCalories * 14d / 1000d, 1),
            request.WeekStartDate, request.Days, request.MealsPerDay, candidates,
            request.ExcludedFoodIds ?? [], consumedCalories, consumedDate);

        var deadline = now.AddMilliseconds(budget.Value.DeadlineMilliseconds);
        var generated = await provider.GenerateAsync(input, deadline, ct);
        if (string.IsNullOrWhiteSpace(generated.Disclaimer?.Text))
            throw new AppException("Kế hoạch thiếu disclaimer an toàn.", 502,
                "Dịch vụ gợi ý trả dữ liệu không hợp lệ.", "diet.ai_integration_failed");

        var draft = MealPlan.Create(userId, weekStart, request.Days, request.MealsPerDay, "{}", now);
        var response = MealPlanMapper.From(draft.Id, now, request.MealsPerDay, generated);
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var saved = await plans.UpsertAsync(
            MealPlan.Create(userId, weekStart, request.Days, request.MealsPerDay, json, now), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return response with { Id = saved.Id };
    }

    public async Task<MealPlanResponse> GetAsync(DateOnly weekStart, CancellationToken ct)
    {
        var plan = await plans.GetAsync(user.RequiredId(), weekStart, ct)
            ?? throw new AppException("Chưa có kế hoạch cho tuần này.", 404,
                "Không tìm thấy kế hoạch ăn uống.", "diet.meal_plan_not_found");
        return JsonSerializer.Deserialize<MealPlanResponse>(plan.PlanJson, JsonOptions)
            ?? throw new AppException("Kế hoạch đã lưu không hợp lệ.", 500,
                "Dữ liệu kế hoạch không đọc được.", "diet.meal_plan_corrupt");
    }
}
