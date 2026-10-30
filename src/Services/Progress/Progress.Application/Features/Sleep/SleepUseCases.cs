using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Sleep;

public sealed class SleepUseCases(
    ISleepRepository sleepRepo,
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

        var log = await sleepRepo.LogWithConcurrencyCheckAsync(userId, request.SleptAt, request.WokeAt, request.SleepQuality, ct);

        return new SleepResponse(
            log.Id,
            log.SleptAt,
            log.WokeAt,
            log.SleepQuality,
            log.DurationHours,
            log.Date);
    }
}
