using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Sleep;

public sealed class SleepUseCases(
    ISleepRepository sleepRepo,
    IUnitOfWork uow,
    ICurrentUser currentUser)
{
    public async Task<SleepResponse> LogAsync(LogSleepRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();

        if (request.WokeAt <= request.SleptAt)
        {
            throw ProgressErrors.SleepWakeBeforeSleep();
        }

        if ((request.WokeAt - request.SleptAt).TotalHours > 24)
        {
            throw ProgressErrors.SleepDurationExceeded();
        }

        // Kiểm tra overlap
        var hasOverlap = await sleepRepo.HasOverlapAsync(userId, request.SleptAt, request.WokeAt, ct);
        if (hasOverlap)
        {
            throw ProgressErrors.SleepOverlap();
        }

        var log = SleepLog.Create(userId, request.SleptAt, request.WokeAt, request.SleepQuality);
        sleepRepo.Add(log);
        await uow.SaveChangesAsync(ct);

        return new SleepResponse(
            log.Id,
            log.SleptAt,
            log.WokeAt,
            log.SleepQuality,
            log.DurationHours,
            log.Date);
    }
}
