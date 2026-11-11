using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Features.Water;
using Progress.Domain.Entities;

namespace Progress.UnitTests.Water;

public sealed class WaterTargetTests
{
    private static readonly DateOnly Today = Progress.Domain.UtcCalendar.Day(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Target_UsesLatestWeightAtThirtyFiveMlPerKg()
    {
        var userId = Guid.NewGuid();
        var water = new FakeWaterRepository();
        var useCases = CreateUseCases(userId, water, weightKg: 60m);

        var summary = await useCases.GetTodayAsync(CancellationToken.None);

        Assert.Equal(2100, summary.TargetMl);
    }

    [Fact]
    public async Task Target_IsClampedToSafeRange()
    {
        var lightUser = Guid.NewGuid();
        var heavyUser = Guid.NewGuid();

        var light = await CreateUseCases(lightUser, new FakeWaterRepository(), weightKg: 30m)
            .GetTodayAsync(CancellationToken.None);
        var heavy = await CreateUseCases(heavyUser, new FakeWaterRepository(), weightKg: 150m)
            .GetTodayAsync(CancellationToken.None);

        Assert.Equal(1500, light.TargetMl);
        Assert.Equal(4000, heavy.TargetMl);
    }

    [Fact]
    public async Task Target_FallsBackToDefaultWhenNoWeightRecorded()
    {
        var useCases = CreateUseCases(Guid.NewGuid(), new FakeWaterRepository(), weightKg: null);

        var summary = await useCases.GetTodayAsync(CancellationToken.None);

        Assert.Equal(WaterUseCases.DefaultDailyTargetMl, summary.TargetMl);
    }

    [Fact]
    public async Task Undo_DeletesOwnLogAndReturnsFreshSummary()
    {
        var userId = Guid.NewGuid();
        var water = new FakeWaterRepository();
        var useCases = CreateUseCases(userId, water, weightKg: 60m);

        await useCases.LogAsync(new LogWaterRequest(300), CancellationToken.None);
        await useCases.LogAsync(new LogWaterRequest(200), CancellationToken.None);

        var summary = await useCases.GetTodayAsync(CancellationToken.None);
        Assert.NotNull(summary.LatestLogId);
        Assert.Equal(500, summary.ConsumedMl);

        var afterUndo = await useCases.UndoAsync(summary.LatestLogId!.Value, CancellationToken.None);

        Assert.Equal(300, afterUndo.ConsumedMl);
        Assert.NotNull(afterUndo.LatestLogId);
    }

    [Fact]
    public async Task Undo_UnknownLog_ThrowsNotFound()
    {
        var useCases = CreateUseCases(Guid.NewGuid(), new FakeWaterRepository(), weightKg: null);

        var error = await Assert.ThrowsAsync<Longevia.Shared.Errors.AppException>(
            () => useCases.UndoAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(404, error.Status);
        Assert.Equal("progress.water_log_not_found", error.Code);
    }

    private static WaterUseCases CreateUseCases(Guid userId, FakeWaterRepository water, decimal? weightKg)
    {
        var metrics = new FakeMetricRepository(weightKg is null
            ? null
            : BodyMetricLog.Create(userId, DateTimeOffset.UtcNow.AddHours(-1), weightKg.Value, 170m));
        return new WaterUseCases(water, metrics, new FakeUnitOfWork(), new FakeCurrentUser(userId),
            TimeProvider.System);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public string? Email => "tester@example.com";
        public bool IsAuthenticated => true;
        public bool InRole(string role) => false;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeMetricRepository(BodyMetricLog? latest) : IBodyMetricRepository
    {
        public void Add(BodyMetricLog log) { }
        public Task<BodyMetricLog?> GetLatestAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(latest);
        public Task<BodyMetricLog?> GetLatestBeforeDateAsync(Guid userId, DateTimeOffset cutoffUtc,
            CancellationToken ct) => Task.FromResult(latest);
        public Task<BodyMetricLog?> GetEarliestAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(latest);
        public Task<(IReadOnlyList<BodyMetricLog> Items, int TotalCount)> GetHistoryAsync(
            Guid userId, DateOnly? fromDate, DateOnly? toDate, int pageNumber, int pageSize,
            CancellationToken ct) => Task.FromResult<(IReadOnlyList<BodyMetricLog>, int)>(([], 0));
        public Task<IReadOnlyList<DateOnly>> GetActiveMetricDatesAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DateOnly>>([]);
        public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct) =>
            Task.FromResult(false);
    }

    private sealed class FakeWaterRepository : IWaterRepository
    {
        private readonly List<WaterLog> _logs = [];

        public void Add(WaterLog log) => _logs.Add(log);

        public Task<int> GetTotalConsumedMlAsync(Guid userId, DateOnly date, CancellationToken ct) =>
            Task.FromResult(_logs.Where(x => x.UserId == userId && x.Date == date).Sum(x => x.AmountMl));

        public Task<IReadOnlyList<DailyWaterTotal>> GetDailyTotalsAsync(Guid userId, DateOnly from,
            DateOnly to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DailyWaterTotal>>(_logs
                .Where(x => x.UserId == userId && x.Date >= from && x.Date <= to)
                .GroupBy(x => x.Date)
                .OrderBy(x => x.Key)
                .Select(x => new DailyWaterTotal(x.Key, x.Sum(log => log.AmountMl)))
                .ToList());

        public Task<WaterLog?> GetLatestAsync(Guid userId, DateOnly date, CancellationToken ct) =>
            Task.FromResult(_logs
                .Where(x => x.UserId == userId && x.Date == date)
                .OrderByDescending(x => x.LoggedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault());

        public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
        {
            var log = _logs.FirstOrDefault(x => x.Id == id && x.UserId == userId);
            if (log is null) return Task.FromResult(false);
            _logs.Remove(log);
            return Task.FromResult(true);
        }
    }
}
