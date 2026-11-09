using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Sleep;

public sealed class SleepUseCases(
    ISleepRepository sleepRepo,
    IUnitOfWork unitOfWork,
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

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        if (!await sleepRepo.DeleteAsync(id, userId, ct))
            throw Longevia.Shared.Errors.AppException.NotFound("Không tìm thấy bản ghi giấc ngủ.",
                "progress.sleep_not_found");
        await unitOfWork.SaveChangesAsync(ct);
    }
}
