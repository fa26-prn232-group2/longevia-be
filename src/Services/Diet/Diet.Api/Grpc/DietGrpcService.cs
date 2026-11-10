using Diet.Application.Abstractions;
using Grpc.Core;
using Longevia.Diet.Contracts.V1;

namespace Diet.Api.Grpc;

public sealed class DietGrpcService(IMealRepository meals, ITargetRepository targets) : DietService.DietServiceBase
{
    public override async Task<NutritionAdherenceResponse> GetNutritionAdherence(
        NutritionAdherenceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id không hợp lệ."));
        if (!DateOnly.TryParseExact(request.FromDate, "yyyy-MM-dd", out var from) ||
            !DateOnly.TryParseExact(request.ToDate, "yyyy-MM-dd", out var to) || from > to)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Khoảng ngày không hợp lệ."));

        var target = await targets.GetAsync(userId, context.CancellationToken);
        if (target is null || target.TargetCalories <= 0)
            return new NutritionAdherenceResponse();

        var logs = await meals.GetRangeAsync(userId, from, to, context.CancellationToken);
        var days = logs.GroupBy(x => x.Date).Select(x => new
        {
            Date = x.Key,
            Calories = x.SelectMany(i => i.Items).Sum(i => i.Nutrition.Calories),
        }).ToArray();
        var onTarget = days.Count(x =>
            Math.Abs(x.Calories - target.TargetCalories) <= target.TargetCalories * 0.10m);
        return new NutritionAdherenceResponse
        {
            AdherenceRate = days.Length == 0 ? 0 : (double)onTarget / days.Length,
            DaysWithTarget = days.Length,
            DaysOnTarget = onTarget,
        };
    }

    public override async Task<WeeklyNutritionSummaryResponse> GetWeeklyNutritionSummary(
        WeeklyNutritionSummaryRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id không hợp lệ."));
        if (!DateOnly.TryParseExact(request.WeekStartDate, "yyyy-MM-dd", out var weekStart) ||
            weekStart.DayOfWeek != DayOfWeek.Monday)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "week_start_date phải là thứ Hai theo ISO yyyy-MM-dd."));

        var weekEnd = weekStart.AddDays(6);
        var logs = await meals.GetRangeAsync(userId, weekStart, weekEnd, context.CancellationToken);
        var response = new WeeklyNutritionSummaryResponse();
        foreach (var group in logs.GroupBy(x => x.Date).OrderBy(x => x.Key))
        {
            var items = group.SelectMany(x => x.Items).ToArray();
            response.Days.Add(new DailyNutritionPoint
            {
                Date = group.Key.ToString("yyyy-MM-dd"),
                Calories = checked((int)decimal.Round(items.Sum(x => x.Nutrition.Calories), 0,
                    MidpointRounding.AwayFromZero)),
                ProteinGrams = (double)items.Sum(x => x.Nutrition.ProteinGrams),
                CarbsGrams = (double)items.Sum(x => x.Nutrition.CarbsGrams),
                FatGrams = (double)items.Sum(x => x.Nutrition.FatGrams),
                FiberGrams = (double)items.Sum(x => x.Nutrition.FiberGrams),
            });
        }

        response.DaysWithData = response.Days.Count;
        if (response.Days.Count > 0)
        {
            response.AverageCalories = checked((int)Math.Round(response.Days.Average(x => x.Calories)));
            response.AverageProteinGrams = response.Days.Average(x => x.ProteinGrams);
            response.AverageCarbsGrams = response.Days.Average(x => x.CarbsGrams);
            response.AverageFatGrams = response.Days.Average(x => x.FatGrams);
            response.AverageFiberGrams = response.Days.Average(x => x.FiberGrams);
        }
        return response;
    }
}
