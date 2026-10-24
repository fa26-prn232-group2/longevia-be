using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Longevia.Shared.Auth;

namespace Diet.Application.Features.Meals;

public sealed class DailySummaryUseCase(IMealRepository meals, ITargetRepository targets,
    ICurrentUser user, TimeProvider time)
{
    public async Task<DailySummaryResponse> ExecuteAsync(DateOnly? date, CancellationToken ct)
    {
        var userId = user.RequiredId();
        var day = date ?? DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var target = await targets.GetAsync(userId, ct) ?? throw DietErrors.MissingTarget();
        var logs = await meals.GetDailyAsync(userId, day, ct);
        var nutrients = logs.SelectMany(x => x.Items).Select(x => x.Nutrition).ToList();
        var calories = nutrients.Sum(x => x.Calories);
        var macros = new MacroSummary(
            Progress(nutrients.Sum(x => x.ProteinGrams), target.TargetProteinGrams),
            Progress(nutrients.Sum(x => x.CarbsGrams), target.TargetCarbsGrams),
            Progress(nutrients.Sum(x => x.FatGrams), target.TargetFatGrams));
        var summaries = logs.GroupBy(x => x.MealType)
            .OrderBy(x => Array.IndexOf(MealTypes.All.ToArray(), x.Key))
            .Select(g => new MealSummary(g.Key, g.Sum(x => x.Items.Sum(i => i.Nutrition.Calories)),
                g.Sum(x => x.Items.Count))).ToList();
        return new(day, target.TargetCalories, calories, target.TargetCalories - calories, macros, summaries);
    }

    private static MacroProgress Progress(decimal consumed, decimal target) => new(consumed, target, target - consumed);
}
