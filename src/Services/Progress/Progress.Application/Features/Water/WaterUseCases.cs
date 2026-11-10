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
        var today = Progress.Domain.UtcCalendar.Day(now);

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
        var today = Progress.Domain.UtcCalendar.Day(now);

        var totalConsumed = await waterRepo.GetTotalConsumedMlAsync(userId, today, ct);
        var percent = Math.Round((double)totalConsumed / DefaultDailyTargetMl * 100, 2);

        return new WaterSummaryResponse(
            today,
            totalConsumed,
            DefaultDailyTargetMl,
            percent);
    }

    /// <summary>Lịch sử uống nước theo từng ngày nghiệp vụ, cũ → mới.</summary>
    public async Task<WaterHistoryResponse> GetHistoryAsync(int days, CancellationToken ct)
    {
        if (days is < 1 or > 90)
        {
            throw Longevia.Shared.Errors.AppException.BadRequest(
                "days phải nằm trong 1..90.", "progress.invalid_days");
        }

        var userId = currentUser.RequiredId();
        var today = Progress.Domain.UtcCalendar.Day(time.GetUtcNow());
        var from = today.AddDays(-(days - 1));
        var totals = await waterRepo.GetDailyTotalsAsync(userId, from, today, ct);
        var byDate = totals.ToDictionary(item => item.Date, item => item.AmountMl);

        var items = new List<WaterHistoryItemResponse>(days);
        for (var date = from; date <= today; date = date.AddDays(1))
        {
            var consumed = byDate.GetValueOrDefault(date);
            items.Add(new WaterHistoryItemResponse(
                date,
                consumed,
                DefaultDailyTargetMl,
                Math.Round((double)consumed / DefaultDailyTargetMl * 100, 2),
                consumed >= DefaultDailyTargetMl));
        }

        return new WaterHistoryResponse(days, DefaultDailyTargetMl, items);
    }
}
