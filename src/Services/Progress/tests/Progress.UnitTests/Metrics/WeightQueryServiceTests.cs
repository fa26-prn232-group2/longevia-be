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
        var log2 = BodyMetricLog.Create(userId, baseTime.AddDays(3), 71.0m, 175.0m); // asOf rơi vào ngày 4
        var log3 = BodyMetricLog.Create(userId, baseTime.AddDays(5), 70.0m, 175.0m);

        repo.Add(log1);
        repo.Add(log2);
        repo.Add(log3);

        // Query as-of ngày 4 (sau log2 nhưng trước log3)
        var asOf = baseTime.AddDays(4);
        var result = await service.GetLatestWeightAsync(userId, asOf, CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal(7100, result.WeightKgX100); // Phải lấy log2 (71.0 kg)
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
}
