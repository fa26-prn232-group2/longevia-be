using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Sleep;

public sealed class SleepUseCases(
    ISleepRepository sleepRepo,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time)
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

    /// <summary>Giấc ngủ đêm qua (ngày thức dậy = hôm nay), mới nhất; null nếu chưa ghi.</summary>
    public async Task<SleepResponse?> GetTodayAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        var today = Progress.Domain.UtcCalendar.Day(time.GetUtcNow());
        var logs = await sleepRepo.GetSleepLogsInDateRangeAsync(userId, today, today, ct);
        var log = logs.FirstOrDefault();
        return log is null
            ? null
            : new SleepResponse(log.Id, log.SleptAt, log.WokeAt, log.SleepQuality,
                log.DurationHours, log.Date);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        if (!await sleepRepo.DeleteAsync(id, userId, ct))
            throw Longevia.Shared.Errors.AppException.NotFound("Không tìm thấy bản ghi giấc ngủ.",
                "progress.sleep_not_found");
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Lịch sử giấc ngủ, mới nhất trước, có phân trang.</summary>
    public async Task<SleepHistoryPageResponse> GetHistoryAsync(int page, int pageSize,
        CancellationToken ct)
    {
        if (page < 1)
        {
            throw Longevia.Shared.Errors.AppException.BadRequest("page phải >= 1.",
                "progress.invalid_page");
        }

        var size = Math.Clamp(pageSize, 1, 50);
        var userId = currentUser.RequiredId();
        var skip = checked((page - 1) * size);

        var items = await sleepRepo.GetPageAsync(userId, skip, size, ct);
        var total = await sleepRepo.CountAsync(userId, ct);

        return new SleepHistoryPageResponse(
            page,
            size,
            total,
            items.Select(log => new SleepResponse(
                log.Id,
                log.SleptAt,
                log.WokeAt,
                log.SleepQuality,
                log.DurationHours,
                log.Date)).ToArray());
    }
}
