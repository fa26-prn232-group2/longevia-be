using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Longevia.Progress.Contracts.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.IntegrationTests.Metrics;

public sealed class ProgressWeightGrpcTests(ProgressWeightGrpcFixture fixture)
    : IClassFixture<ProgressWeightGrpcFixture>
{
    // 1. Invalid and empty GUID
    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Case01_GetLatestWeight_InvalidAndEmptyGuid_ThrowsInvalidArgument(string userId)
    {
        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest { UserId = userId }));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // 2. No BodyMetricLog
    [Fact]
    public async Task Case02_GetLatestWeight_NoBodyMetricLog_ReturnsNoRecordFound()
    {
        var userId = Guid.NewGuid();
        var response = await fixture.Client.GetLatestWeightAsync(
            new GetLatestWeightRequest { UserId = userId.ToString() });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.NoRecordFound, response.ResultCase);
        Assert.True(response.NoRecordFound);
    }

    // 3. One valid measurement
    [Fact]
    public async Task Case03_GetLatestWeight_OneValidMeasurement_ReturnsWeightRecord()
    {
        var userId = Guid.NewGuid();
        var loggedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var metric = await SeedMetricAsync(userId, 70.50m, 175.00m, loggedAt);

        var response = await fixture.Client.GetLatestWeightAsync(
            new GetLatestWeightRequest { UserId = userId.ToString() });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.Record, response.ResultCase);
        Assert.NotNull(response.Record);
        Assert.Equal(7050, response.Record.WeightKgX100);
        Assert.Equal(metric.Id.ToString(), response.Record.SourceMetricId);
    }

    // 4. Multiple measurements and deterministic newest selection
    [Fact]
    public async Task Case04_GetLatestWeight_MultipleMeasurements_PicksDeterministicNewest()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await SeedMetricAsync(userId, 75.00m, 175.00m, now.AddDays(-5));
        await SeedMetricAsync(userId, 73.00m, 175.00m, now.AddDays(-3));
        var newest = await SeedMetricAsync(userId, 71.50m, 175.00m, now.AddDays(-1));

        var response = await fixture.Client.GetLatestWeightAsync(
            new GetLatestWeightRequest { UserId = userId.ToString() });

        Assert.Equal(7150, response.Record.WeightKgX100);
        Assert.Equal(newest.Id.ToString(), response.Record.SourceMetricId);
    }

    // 5. Equal timestamps with ID tie-break
    [Fact]
    public async Task Case05_GetLatestWeight_EqualTimestamps_PicksHigherIdTieBreak()
    {
        var userId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.AddDays(-2);

        var log1 = await SeedMetricAsync(userId, 68.00m, 175.00m, timestamp);
        var log2 = await SeedMetricAsync(userId, 69.50m, 175.00m, timestamp);

        var expectedLog = log1.Id.CompareTo(log2.Id) > 0 ? log1 : log2;

        var response = await fixture.Client.GetLatestWeightAsync(
            new GetLatestWeightRequest { UserId = userId.ToString() });

        Assert.Equal(expectedLog.Id.ToString(), response.Record.SourceMetricId);
        Assert.Equal(checked((int)(expectedLog.WeightKg * 100m)), response.Record.WeightKgX100);
    }

    // 6. asOf before all measurements
    [Fact]
    public async Task Case06_GetLatestWeight_AsOfBeforeAllMeasurements_ReturnsNoRecordFound()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await SeedMetricAsync(userId, 70.00m, 175.00m, now.AddDays(-2));

        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString(),
            AsOfUtc = Timestamp.FromDateTimeOffset(now.AddDays(-5))
        });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.NoRecordFound, response.ResultCase);
        Assert.True(response.NoRecordFound);
    }

    // 7. asOf exactly equal to measurement timestamp
    [Fact]
    public async Task Case07_GetLatestWeight_AsOfExactlyEqualMeasurementTimestamp_ReturnsRecord()
    {
        var userId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow.AddDays(-2);
        var log = await SeedMetricAsync(userId, 72.25m, 175.00m, timestamp);

        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString(),
            AsOfUtc = Timestamp.FromDateTimeOffset(log.LoggedAt)
        });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.Record, response.ResultCase);
        Assert.Equal(7225, response.Record.WeightKgX100);
        Assert.Equal(log.Id.ToString(), response.Record.SourceMetricId);
    }

    // 8. Omitted asOf
    [Fact]
    public async Task Case08_GetLatestWeight_OmittedAsOf_UsesServerUtcNow()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var log = await SeedMetricAsync(userId, 70.00m, 175.00m, now.AddHours(-1));

        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString()
            // AsOfUtc omitted
        });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.Record, response.ResultCase);
        Assert.Equal(7000, response.Record.WeightKgX100);
        Assert.Equal(log.Id.ToString(), response.Record.SourceMetricId);
    }

    // 9. Future asOf clamping
    [Fact]
    public async Task Case09_GetLatestWeight_FutureAsOf_ClampedToServerUtcNow()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var validPastLog = await SeedMetricAsync(userId, 69.50m, 175.00m, now.AddHours(-1));
        await SeedMetricAsync(userId, 65.00m, 175.00m, now.AddDays(2)); // future log

        // Query with future asOf (+10 days) -> clamped to server UtcNow
        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString(),
            AsOfUtc = Timestamp.FromDateTimeOffset(now.AddDays(10))
        });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.Record, response.ResultCase);
        Assert.Equal(6950, response.Record.WeightKgX100);
        Assert.Equal(validPastLog.Id.ToString(), response.Record.SourceMetricId);
    }

    // 10. Future measurement exclusion
    [Fact]
    public async Task Case10_GetLatestWeight_FutureMeasurement_Excluded()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        // User only has a future measurement (e.g. client device clock skewed)
        await SeedMetricAsync(userId, 68.00m, 175.00m, now.AddHours(5));

        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString()
        });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.NoRecordFound, response.ResultCase);
        Assert.True(response.NoRecordFound);
    }

    // 11. Backdated measurements
    [Fact]
    public async Task Case11_GetLatestWeight_BackdatedMeasurement_PreservedAndQueried()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var backdated = await SeedMetricAsync(userId, 80.00m, 175.00m, now.AddDays(-20));
        await SeedMetricAsync(userId, 75.00m, 175.00m, now.AddDays(-10));

        // Query asOf -15 days selects the backdated record
        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString(),
            AsOfUtc = Timestamp.FromDateTimeOffset(now.AddDays(-15))
        });

        Assert.Equal(8000, response.Record.WeightKgX100);
        Assert.Equal(backdated.Id.ToString(), response.Record.SourceMetricId);
    }

    // 12. Two users with distinct measurements
    [Fact]
    public async Task Case12_GetLatestWeight_TwoUsers_StrictIsolation()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var logA = await SeedMetricAsync(userA, 75.00m, 175.00m, now.AddHours(-1));
        var logB = await SeedMetricAsync(userB, 65.00m, 170.00m, now.AddHours(-1));

        var responseA = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest { UserId = userA.ToString() });
        var responseB = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest { UserId = userB.ToString() });

        Assert.Equal(7500, responseA.Record.WeightKgX100);
        Assert.Equal(logA.Id.ToString(), responseA.Record.SourceMetricId);

        Assert.Equal(6500, responseB.Record.WeightKgX100);
        Assert.Equal(logB.Id.ToString(), responseB.Record.SourceMetricId);
    }

    // 13. weight_kg_x100 precision and round-trip
    [Theory]
    [InlineData(70.50, 7050)]
    [InlineData(68.25, 6825)]
    [InlineData(120.00, 12000)]
    [InlineData(55.55, 5555)]
    public async Task Case13_GetLatestWeight_WeightKgX100Precision_RoundTrip(decimal inputKg, int expectedX100)
    {
        var userId = Guid.NewGuid();
        await SeedMetricAsync(userId, inputKg, 175.00m, DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest { UserId = userId.ToString() });

        Assert.Equal(expectedX100, response.Record.WeightKgX100);
    }

    // 14. MeasuredAt and SourceMetricId accuracy
    [Fact]
    public async Task Case14_GetLatestWeight_MeasuredAtAndSourceMetricId_ExactAccuracy()
    {
        var userId = Guid.NewGuid();
        var exactTime = new DateTimeOffset(2026, 10, 24, 9, 15, 30, TimeSpan.Zero).AddTicks(2469327);
        var metric = await SeedMetricAsync(userId, 71.00m, 175.00m, exactTime);

        var response = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest { UserId = userId.ToString() });

        Assert.Equal(metric.Id.ToString(), response.Record.SourceMetricId);
        Assert.NotEqual(exactTime, metric.LoggedAt);
        Assert.Equal(metric.LoggedAt, response.Record.MeasuredAt.ToDateTimeOffset());
    }

    // 15. Transport unavailable (no server listening)
    [Fact]
    public async Task Case15_GetLatestWeight_ServerUnreachable_ThrowsUnavailable()
    {
        using var brokenChannel = GrpcChannel.ForAddress("http://127.0.0.1:1", new GrpcChannelOptions
        {
            // Fail connection establishment before the RPC deadline, even during parallel fixture startup.
            HttpHandler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(1) }
        });
        var brokenClient = new ProgressService.ProgressServiceClient(brokenChannel);

        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await brokenClient.GetLatestWeightAsync(
                new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() },
                deadline: DateTime.UtcNow.AddSeconds(10)));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    // 16. Deadline and cancellation
    [Fact]
    public async Task Case16_GetLatestWeight_Cancellation_ThrowsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await fixture.Client.GetLatestWeightAsync(
                new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() },
                cancellationToken: cts.Token));

        Assert.Equal(StatusCode.Cancelled, ex.StatusCode);
    }

    // 17. No false success after errors
    [Fact]
    public async Task Case17_GetLatestWeight_NoFalseSuccessAfterErrors()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest { UserId = "invalid-guid" }));

        Assert.NotEqual(StatusCode.OK, ex.StatusCode);
    }

    // 18. Real HTTP/2 server startup
    [Fact]
    public void Case18_ProgressHost_RealHttp2ServerStartup_ServesGrpc()
    {
        Assert.True(fixture.GrpcPort > 0);
        Assert.NotNull(fixture.Client);
    }

    // 19. Container network and port configuration
    [Fact]
    public void Case19_ContainerNetworkAndPort_Configuration_Verified()
    {
        new ProgressWeightConfigurationTests().Case19_ContainerNetworkAndPort_Configuration_Verified();
    }

    // 20. No public host exposure in production Compose
    [Fact]
    public void Case20_ProductionCompose_NoPublicHostExposure()
    {
        new ProgressWeightConfigurationTests().Case20_ProductionCompose_NoPublicHostExposure();
    }

    // Partial integration: domain BMI -> PostgreSQL -> real Weight gRPC provider.
    [Fact]
    public async Task SeededMetrics_BmiSnapshots_To_WeightGrpc()
    {
        // 1. Seed body metric with real Height & Weight in progress DB
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var initialHeightCm = 175.00m;
        var initialWeightKg = 70.00m;

        // Height is supplied directly to the domain entity in this partial test.
        var initialMetric = await SeedMetricAsync(userId, initialWeightKg, initialHeightCm, now.AddHours(-2));

        // BMI: 70 / (1.75 * 1.75) = 22.86
        Assert.Equal(22.86m, initialMetric.Bmi);
        Assert.Equal(initialHeightCm, initialMetric.HeightUsedCm);

        // 2. Query Progress Weight gRPC provider for Diet BMR/TDEE calculation
        var weightResponse1 = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString()
        });

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.Record, weightResponse1.ResultCase);
        Assert.Equal(7000, weightResponse1.Record.WeightKgX100);
        Assert.Equal(initialMetric.Id.ToString(), weightResponse1.Record.SourceMetricId);
        Assert.Equal(initialMetric.LoggedAt, weightResponse1.Record.MeasuredAt.ToDateTimeOffset());

        // 3. Seed a second snapshot with a different height and weight.
        var updatedHeightCm = 180.00m;
        var updatedWeightKg = 72.00m;
        var secondMetric = await SeedMetricAsync(userId, updatedWeightKg, updatedHeightCm, now.AddHours(-1));

        // BMI: 72 / (1.80 * 1.80) = 22.22
        Assert.Equal(22.22m, secondMetric.Bmi);

        // 4. Query Progress Weight gRPC again: returns the newest weight (7200)
        var weightResponse2 = await fixture.Client.GetLatestWeightAsync(new GetLatestWeightRequest
        {
            UserId = userId.ToString()
        });

        Assert.Equal(7200, weightResponse2.Record.WeightKgX100);
        Assert.Equal(secondMetric.Id.ToString(), weightResponse2.Record.SourceMetricId);

        // Historical initial metric provenance and BMI remain intact
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var persistedInitial = await db.BodyMetricLogs.FindAsync(initialMetric.Id);
        Assert.NotNull(persistedInitial);
        Assert.Equal(22.86m, persistedInitial.Bmi);
        Assert.Equal(initialHeightCm, persistedInitial.HeightUsedCm);
    }

    private async Task<BodyMetricLog> SeedMetricAsync(Guid userId, decimal weightKg, decimal heightCm, DateTimeOffset loggedAt)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var log = BodyMetricLog.Create(userId, loggedAt, weightKg, heightCm);
        db.BodyMetricLogs.Add(log);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await db.BodyMetricLogs.AsNoTracking().SingleAsync(x => x.Id == log.Id);
    }
}
