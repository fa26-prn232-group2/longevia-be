using Progress.Application.Abstractions;
using Progress.Application.Features.Metrics;
using Progress.Domain.Entities;

namespace Progress.UnitTests.Metrics;

public sealed class WeightQueryServiceTests
{
    private sealed class StubBodyMetricRepository : IBodyMetricRepository
    {
        private readonly List<BodyMetricLog> _logs = [];

        public void Add(BodyMetricLog log) => _logs.Add(log);

        public Task<BodyMetricLog?> GetLatestAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(_logs.Where(x => x.UserId == userId).OrderByDescending(x => x.LoggedAt).ThenByDescending(x => x.Id).FirstOrDefault());

        public Task<BodyMetricLog?> GetLatestBeforeDateAsync(Guid userId, DateTimeOffset cutoffUtc, CancellationToken ct) =>
            Task.FromResult(_logs.Where(x => x.UserId == userId && x.LoggedAt <= cutoffUtc)
                .OrderByDescending(x => x.LoggedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault());

        public Task<BodyMetricLog?> GetEarliestAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(_logs.Where(x => x.UserId == userId).OrderBy(x => x.LoggedAt).ThenBy(x => x.Id).FirstOrDefault());

        public Task<(IReadOnlyList<BodyMetricLog> Items, int TotalCount)> GetHistoryAsync(Guid userId, DateOnly? fromDate, DateOnly? toDate, int pageNumber, int pageSize, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<DateOnly>> GetActiveMetricDatesAsync(Guid userId, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    [Fact]
    public async Task GetLatestWeight_EmptyUserId_ThrowsArgumentException()
    {
        var repo = new StubBodyMetricRepository();
        var service = new WeightQueryService(repo, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetLatestWeightAsync(Guid.Empty, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetLatestWeight_NoRecord_ReturnsNotFound()
    {
        var repo = new StubBodyMetricRepository();
        var service = new WeightQueryService(repo, TimeProvider.System);

        var result = await service.GetLatestWeightAsync(Guid.NewGuid(), null, CancellationToken.None);

        Assert.False(result.Found);
        Assert.Null(result.WeightKgX100);
        Assert.Null(result.MeasuredAt);
    }

    [Fact]
    public async Task GetLatestWeight_ValidRecord_ConvertsToInt32X100Accurately()
    {
        var repo = new StubBodyMetricRepository();
        var service = new WeightQueryService(repo, TimeProvider.System);

        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var log = BodyMetricLog.Create(userId, loggedAt, 68.55m, 175.0m);
        repo.Add(log);

        var result = await service.GetLatestWeightAsync(userId, null, CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal(6855, result.WeightKgX100);
        Assert.Equal(loggedAt, result.MeasuredAt);
        Assert.Equal(log.Id, result.SourceMetricId);
    }

    [Fact]
    public async Task GetLatestWeight_MultipleRecords_PicksLatestBeforeAsOf()
    {
        var repo = new StubBodyMetricRepository();
        var service = new WeightQueryService(repo, TimeProvider.System);

        var userId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow.AddDays(-10);

        var log1 = BodyMetricLog.Create(userId, baseTime.AddDays(1), 72.0m, 175.0m);
        var log2 = BodyMetricLog.Create(userId, baseTime.AddDays(3), 71.0m, 175.0m);
        var log3 = BodyMetricLog.Create(userId, baseTime.AddDays(5), 70.0m, 175.0m);

        repo.Add(log1);
        repo.Add(log2);
        repo.Add(log3);

        var asOf = baseTime.AddDays(4);
        var result = await service.GetLatestWeightAsync(userId, asOf, CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal(7100, result.WeightKgX100);
        Assert.Equal(log2.Id, result.SourceMetricId);
    }

    [Fact]
    public async Task GetLatestWeight_TwoUsersIsolation_DoesNotCrossRead()
    {
        var repo = new StubBodyMetricRepository();
        var service = new WeightQueryService(repo, TimeProvider.System);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var logA = BodyMetricLog.Create(userA, DateTimeOffset.UtcNow, 70.0m, 175.0m);
        repo.Add(logA);

        var resultB = await service.GetLatestWeightAsync(userB, null, CancellationToken.None);
        Assert.False(resultB.Found);

        var resultA = await service.GetLatestWeightAsync(userA, null, CancellationToken.None);
        Assert.True(resultA.Found);
        Assert.Equal(7000, resultA.WeightKgX100);
    }

    [Fact]
    public async Task GetLatestWeight_FutureAsOf_ClampedToServerUtcNow()
    {
        var repo = new StubBodyMetricRepository();
        var now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var service = new WeightQueryService(repo, timeProvider);

        var userId = Guid.NewGuid();
        var pastLog = BodyMetricLog.Create(userId, now.AddHours(-2), 70.0m, 175.0m);
        var futureLog = BodyMetricLog.Create(userId, now.AddHours(2), 69.0m, 175.0m);
        repo.Add(pastLog);
        repo.Add(futureLog);

        // Client yêu cầu as_of ở tương lai (+5 hours)
        var futureAsOf = now.AddHours(5);
        var result = await service.GetLatestWeightAsync(userId, futureAsOf, CancellationToken.None);

        // Phải được clamp về `now`, do đó bỏ qua futureLog và lấy pastLog
        Assert.True(result.Found);
        Assert.Equal(7000, result.WeightKgX100);
        Assert.Equal(pastLog.Id, result.SourceMetricId);
    }

    [Fact]
    public async Task GetLatestWeight_SameTimestamp_PicksHigherIdTieBreak()
    {
        var repo = new StubBodyMetricRepository();
        var now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var service = new WeightQueryService(repo, new FixedTimeProvider(now));

        var userId = Guid.NewGuid();
        var timestamp = now.AddDays(-1);

        var log1 = BodyMetricLog.Create(userId, timestamp, 70.0m, 175.0m);
        var log2 = BodyMetricLog.Create(userId, timestamp, 71.5m, 175.0m);

        repo.Add(log1);
        repo.Add(log2);

        var expectedLog = log1.Id.CompareTo(log2.Id) > 0 ? log1 : log2;
        var result = await service.GetLatestWeightAsync(userId, null, CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal(expectedLog.Id, result.SourceMetricId);
        Assert.Equal(checked((int)(expectedLog.WeightKg * 100m)), result.WeightKgX100);
    }
}
