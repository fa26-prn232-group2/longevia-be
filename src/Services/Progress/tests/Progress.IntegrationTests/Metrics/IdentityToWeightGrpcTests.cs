using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Longevia.Progress.Contracts.V1;
using Microsoft.EntityFrameworkCore;
using Progress.Domain.Entities;

namespace Progress.IntegrationTests.Metrics;

[Collection("Actual Identity height")]
public sealed class IdentityToWeightGrpcTests(IdentityHeightFixture fixture)
{
    [Fact]
    public async Task ActualIdentity_JwtMetrics_Postgres_WeightGrpc_PreservesHistoricalSnapshots()
    {
        var user = await fixture.SeedAsync(175m);
        await using var api = fixture.Factory();
        using var http = fixture.Client(api, user);
        var firstTime = new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.Zero).AddTicks(2469327);
        var first = await PostMetricAsync(http, user, 70m, firstTime);
        Assert.Equal(70m, first.WeightKg);
        Assert.Equal(175m, first.HeightUsedCm);
        Assert.Equal(22.86m, first.Bmi);
        Assert.Equal("NormalWeight", first.BmiCategory);

        var grpc = new ProgressWeightGrpcFixture { ExternalConnectionString = fixture.Progress.ConnectionString };
        try
        {
            await grpc.InitializeAsync();
            await AssertWeightAsync(grpc, user, first, null);
            await fixture.UpdateAsync(user, 180m);
            var second = await PostMetricAsync(http, user, 72m, firstTime.AddDays(1));
            Assert.Equal(180m, second.HeightUsedCm);
            Assert.Equal(22.22m, second.Bmi);
            await using var db = fixture.Db();
            var original = await db.BodyMetricLogs.AsNoTracking().SingleAsync(x => x.Id == first.Id);
            Assert.Equal(175m, original.HeightUsedCm);
            Assert.Equal(22.86m, original.Bmi);
            await AssertWeightAsync(grpc, user, second, null);
            await AssertWeightAsync(grpc, user, first, first.LoggedAt);
        }
        finally { await grpc.DisposeAsync(); }
    }

    [Fact]
    public async Task ActualIdentity_TwoJwtUsers_WeightGrpc_SelectsOwnHeightAndWeight()
    {
        var a = await fixture.SeedAsync(175m);
        var b = await fixture.SeedAsync(180m);
        await using var api = fixture.Factory();
        using var httpA = fixture.Client(api, a);
        using var httpB = fixture.Client(api, b);
        var time = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
        // Forged body IDs cannot override the authenticated HTTP subject.
        var metricA = await PostMetricAsync(httpA, b, 70m, time);
        var metricB = await PostMetricAsync(httpB, a, 72m, time);
        Assert.Equal(a, metricA.UserId);
        Assert.Equal(b, metricB.UserId);
        Assert.Equal(175m, metricA.HeightUsedCm);
        Assert.Equal(180m, metricB.HeightUsedCm);
        Assert.Equal(22.86m, metricA.Bmi);
        Assert.Equal(22.22m, metricB.Bmi);
        var grpc = new ProgressWeightGrpcFixture { ExternalConnectionString = fixture.Progress.ConnectionString };
        try
        {
            await grpc.InitializeAsync();
            // Internal RPC selects by requested UserId under ADR 001 Compose trust, without user JWT authorization.
            await AssertWeightAsync(grpc, a, metricA, null);
            await AssertWeightAsync(grpc, b, metricB, null);
        }
        finally { await grpc.DisposeAsync(); }
    }

    private async Task<BodyMetricLog> PostMetricAsync(HttpClient client, Guid bodyUser, decimal weight, DateTimeOffset time)
    {
        using var response = await client.PostAsJsonAsync("/metrics", new { loggedAt = time, weightKg = weight, userId = bodyUser });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        await using var db = fixture.Db();
        return await db.BodyMetricLogs.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private static async Task AssertWeightAsync(ProgressWeightGrpcFixture grpc, Guid user, BodyMetricLog metric, DateTimeOffset? asOf)
    {
        var request = new GetLatestWeightRequest { UserId = user.ToString() };
        if (asOf.HasValue) request.AsOfUtc = Timestamp.FromDateTimeOffset(asOf.Value);
        var response = await grpc.Client.GetLatestWeightAsync(request, deadline: DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.Record, response.ResultCase);
        Assert.Equal(checked((int)(metric.WeightKg * 100m)), response.Record.WeightKgX100);
        Assert.Equal(metric.Id.ToString(), response.Record.SourceMetricId);
        Assert.Equal(metric.LoggedAt, response.Record.MeasuredAt.ToDateTimeOffset());
    }
}
