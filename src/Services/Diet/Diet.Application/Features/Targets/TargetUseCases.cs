using Diet.Application.Abstractions;
using Diet.Application.Common;
using Diet.Domain;
using Diet.Domain.Entities;
using Longevia.Shared.Auth;

namespace Diet.Application.Features.Targets;

public sealed class TargetUseCases(ITargetRepository targets, ICurrentUser user, TimeProvider time)
{
    public async Task<TargetResponse> GetAsync(CancellationToken ct) =>
        TargetResponse.From(await targets.GetAsync(user.RequiredId(), ct) ?? throw DietErrors.MissingTarget());

    public async Task<TargetResponse> UpdateAsync(UpdateTargetRequest request, CancellationToken ct)
    {
        var target = DailyDietTarget.Create(user.RequiredId(), new NutritionValues(request.TargetCalories,
            request.TargetProteinGrams, request.TargetCarbsGrams, request.TargetFatGrams, 0), time.GetUtcNow());
        return TargetResponse.From(await targets.UpsertAsync(target, ct));
    }
}
