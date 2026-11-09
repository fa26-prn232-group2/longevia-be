using System.Text.Json;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.WorkoutPlans;

public sealed class WorkoutPlanUseCases(IWorkoutPlanRepository plans, IWorkoutPlanProvider provider,
    IUserProfileProvider profiles, IBodyMetricRepository metrics, IUnitOfWork unitOfWork,
    ICurrentUser currentUser, TimeProvider time)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkoutPlanResponse> GenerateAsync(GenerateWorkoutPlanRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(request.WeekStartDate, "yyyy-MM-dd", out var weekStart) ||
            weekStart.DayOfWeek != DayOfWeek.Monday)
            throw AppException.BadRequest("weekStartDate phải là thứ Hai theo ISO yyyy-MM-dd.",
                "progress.invalid_week_start");
        if (request.Days is < 1 or > 7)
            throw AppException.BadRequest("days phải nằm trong 1..7.", "progress.invalid_days");
        if (request.SessionsPerWeek is < 1 or > 7 || request.SessionsPerWeek > request.Days)
            throw AppException.BadRequest("sessionsPerWeek phải trong 1..days.", "progress.invalid_sessions");
        if (request.MinutesPerSession is < 10 or > 120)
            throw AppException.BadRequest("minutesPerSession phải trong 10..120.", "progress.invalid_minutes");

        var userId = currentUser.RequiredId();
        var profile = await profiles.GetAsync(userId, ct);
        var weight = await metrics.GetLatestAsync(userId, ct)
            ?? throw AppException.Unprocessable("Bạn chưa ghi nhận cân nặng.", "progress.weight_not_found");
        var now = time.GetUtcNow();
        var generated = await provider.GenerateAsync(new WorkoutPlanInput(userId, profile, request.Goal,
            weight.WeightKg, request.WeekStartDate, request.Days, request.SessionsPerWeek,
            request.MinutesPerSession, request.ActivityPreference ?? [], request.EquipmentAvailable ?? [],
            request.Limitations ?? []), now.AddSeconds(90), ct);
        if (string.IsNullOrWhiteSpace(generated.Disclaimer?.Text))
            throw new AppException("Kế hoạch tập thiếu disclaimer an toàn.", 502,
                "Dịch vụ gợi ý trả dữ liệu không hợp lệ.", "progress.ai_integration_failed");

        var draft = WorkoutPlan.Create(userId, weekStart, request.Days, request.SessionsPerWeek,
            request.MinutesPerSession, request.Goal, "{}", now);
        var response = WorkoutPlanMapper.From(draft.Id, now, generated);
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var saved = await plans.UpsertAsync(WorkoutPlan.Create(userId, weekStart, request.Days,
            request.SessionsPerWeek, request.MinutesPerSession, request.Goal, json, now), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return response with { Id = saved.Id };
    }

    public async Task<WorkoutPlanResponse> GetAsync(DateOnly weekStart, CancellationToken ct)
    {
        var plan = await plans.GetAsync(currentUser.RequiredId(), weekStart, ct)
            ?? throw new AppException("Chưa có kế hoạch tập cho tuần này.", 404,
                "Không tìm thấy kế hoạch tập.", "progress.workout_plan_not_found");
        return JsonSerializer.Deserialize<WorkoutPlanResponse>(plan.PlanJson, JsonOptions)
            ?? throw new AppException("Kế hoạch tập đã lưu không hợp lệ.", 500,
                "Dữ liệu kế hoạch không đọc được.", "progress.workout_plan_corrupt");
    }
}
