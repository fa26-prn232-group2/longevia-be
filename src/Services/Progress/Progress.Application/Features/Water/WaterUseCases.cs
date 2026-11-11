using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Water;

public sealed class WaterUseCases(
    IWaterRepository waterRepo,
    IBodyMetricRepository metrics,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public const int DefaultDailyTargetMl = 2500;

    /// <summary>Nhu cầu nước tham khảo 35 ml cho mỗi kg cân nặng.</summary>
    private const decimal MillilitersPerKilogram = 35m;

    private const int MinDailyTargetMl = 1500;
    private const int MaxDailyTargetMl = 4000;

    public async Task<WaterSummaryResponse> LogAsync(LogWaterRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        var now = time.GetUtcNow();
        var today = Progress.Domain.UtcCalendar.Day(now);

        var log = WaterLog.Create(userId, request.AmountMl, today, now);
        waterRepo.Add(log);
        await uow.SaveChangesAsync(ct);

        // Trả đúng log vừa tạo: khi đồng hồ hệ thống bị cố định (test) hoặc hai lần
        // ghi trùng mili-giây, việc tra "log mới nhất" theo thời gian là không xác định.
        return await BuildSummaryAsync(userId, today, ct, log.Id);
    }

    public async Task<WaterSummaryResponse> GetTodayAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        var today = Progress.Domain.UtcCalendar.Day(time.GetUtcNow());
        return await BuildSummaryAsync(userId, today, ct);
    }

    /// <summary>Hoàn tác một lần ghi nước (chỉ log của chính người dùng hiện tại).</summary>
    public async Task<WaterSummaryResponse> UndoAsync(Guid logId, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();
        var deleted = await waterRepo.DeleteAsync(logId, userId, ct);
        if (!deleted)
        {
            throw ProgressErrors.WaterLogNotFound();
        }

        await uow.SaveChangesAsync(ct);
        var today = Progress.Domain.UtcCalendar.Day(time.GetUtcNow());
        return await BuildSummaryAsync(userId, today, ct);
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
        var targetMl = await ResolveTargetMlAsync(userId, ct);
        var totals = await waterRepo.GetDailyTotalsAsync(userId, from, today, ct);
        var byDate = totals.ToDictionary(item => item.Date, item => item.AmountMl);

        var items = new List<WaterHistoryItemResponse>(days);
        for (var date = from; date <= today; date = date.AddDays(1))
        {
            var consumed = byDate.GetValueOrDefault(date);
            items.Add(new WaterHistoryItemResponse(
                date,
                consumed,
                targetMl,
                Percent(consumed, targetMl),
                consumed >= targetMl));
        }

        return new WaterHistoryResponse(days, targetMl, items);
    }

    private async Task<WaterSummaryResponse> BuildSummaryAsync(Guid userId, DateOnly date,
        CancellationToken ct, Guid? latestLogId = null)
    {
        var targetMl = await ResolveTargetMlAsync(userId, ct);
        var consumedMl = await waterRepo.GetTotalConsumedMlAsync(userId, date, ct);
        if (latestLogId is null)
        {
            var latest = await waterRepo.GetLatestAsync(userId, date, ct);
            latestLogId = latest?.Id;
        }

        return new WaterSummaryResponse(
            date,
            consumedMl,
            targetMl,
            Percent(consumedMl, targetMl),
            latestLogId);
    }

    /// <summary>
    /// Mục tiêu nước theo cân nặng gần nhất (35 ml/kg, làm tròn 50 ml, kẹp 1.500–4.000 ml).
    /// Người dùng chưa ghi cân nặng dùng mức mặc định 2.500 ml.
    /// </summary>
    private async Task<int> ResolveTargetMlAsync(Guid userId, CancellationToken ct)
    {
        var latest = await metrics.GetLatestBeforeDateAsync(userId, time.GetUtcNow(), ct);
        if (latest is null || latest.WeightKg <= 0)
        {
            return DefaultDailyTargetMl;
        }

        var raw = latest.WeightKg * MillilitersPerKilogram;
        var rounded = Math.Round(raw / 50m, MidpointRounding.AwayFromZero) * 50m;
        return (int)Math.Clamp(rounded, MinDailyTargetMl, MaxDailyTargetMl);
    }

    private static double Percent(int consumedMl, int targetMl) =>
        targetMl <= 0 ? 0 : Math.Round((double)consumedMl / targetMl * 100, 2);
}
