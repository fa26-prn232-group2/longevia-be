using Microsoft.Extensions.DependencyInjection;
using Progress.Application.Features.Metrics;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.IntegrationTests.Metrics;

[Collection("Progress foundation")]
public sealed class WeightProviderIntegrationTests(ProgressFixture fixture)
{
    [Fact]
    public async Task WeightProvider_RealPostgreSql_QueriesDeterministicLatestBeforeAsOf()
    {
        var factory = fixture.Factory;
        using var scope = factory.Services.CreateScope();
        var weightService = scope.ServiceProvider.GetRequiredService<IWeightQueryService>();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();

        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Log 3 bản ghi
        var log1 = BodyMetricLog.Create(userId, now.AddDays(-7), 75.0m, 175.0m);
        var log2 = BodyMetricLog.Create(userId, now.AddDays(-3), 73.5m, 175.0m);
        var log3 = BodyMetricLog.Create(userId, now.AddDays(-1), 72.0m, 175.0m);

        db.BodyMetricLogs.AddRange(log1, log2, log3);
        await db.SaveChangesAsync();

        // 1. Query với asOf = 2 ngày trước (nằm giữa log2 và log3)
        var resultAsOf = await weightService.GetLatestWeightAsync(userId, now.AddDays(-2), CancellationToken.None);
        Assert.True(resultAsOf.Found);
        Assert.Equal(7350, resultAsOf.WeightKgX100);
        Assert.Equal(log2.Id, resultAsOf.SourceMetricId);

        // 2. Query asOf trước tất cả (8 ngày trước) => NotFound
        var resultBefore = await weightService.GetLatestWeightAsync(userId, now.AddDays(-8), CancellationToken.None);
        Assert.False(resultBefore.Found);

        // 3. Query mới nhất không truyền asOf (mặc định lấy now) => log3 (72.0kg)
        var resultLatest = await weightService.GetLatestWeightAsync(userId, null, CancellationToken.None);
        Assert.True(resultLatest.Found);
        Assert.Equal(7200, resultLatest.WeightKgX100);
        Assert.Equal(log3.Id, resultLatest.SourceMetricId);
    }
}
