using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Water;

public sealed class WaterUseCases(
    IWaterRepository waterRepo,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public const int DefaultDailyTargetMl = 2500;

    public async Task<WaterSummaryResponse> LogAsync(LogWaterRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var log = WaterLog.Create(userId, request.AmountMl, today, now);
        waterRepo.Add(log);
        await uow.SaveChangesAsync(ct);

        var totalConsumed = await waterRepo.GetTotalConsumedMlAsync(userId, today, ct);
        var percent = Math.Round((double)totalConsumed / DefaultDailyTargetMl * 100, 2);

        return new WaterSummaryResponse(
            today,
            totalConsumed,
            DefaultDailyTargetMl,
            percent);
    }

    public async Task<WaterSummaryResponse> GetTodayAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var totalConsumed = await waterRepo.GetTotalConsumedMlAsync(userId, today, ct);
        var percent = Math.Round((double)totalConsumed / DefaultDailyTargetMl * 100, 2);

        return new WaterSummaryResponse(
            today,
            totalConsumed,
            DefaultDailyTargetMl,
            percent);
    }
}
