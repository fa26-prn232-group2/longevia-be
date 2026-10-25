using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Longevia.Identity.Contracts.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Progress.Application.Abstractions;
using Progress.Application.Features.Metrics;

namespace Progress.IntegrationTests.Metrics;

public sealed class HeightGrpcTests(IdentityHeightFixture fixture) : IClassFixture<IdentityHeightFixture>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static object Metric(Guid? forgedUser = null) => new
    { loggedAt = "2026-10-24T23:59:59Z", weightKg = 70m, userId = forgedUser };

    [Fact]
    public async Task ActualIdentity_ProfileHeightToJwtMetrics_PersistsImmutableBmiSnapshots()
    {
        var user = await fixture.SeedAsync(175m);
        await using var factory = fixture.Factory();
        using var client = fixture.Client(factory, user);
        using var first = await client.PostAsJsonAsync("/metrics", Metric());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<BodyMetricResponse>(Json))!;
        Assert.Equal(22.86m, firstBody.Bmi); Assert.Equal("NormalWeight", firstBody.BmiCategory);
        await fixture.UpdateAsync(user, 180m);
        using var second = await client.PostAsJsonAsync("/metrics", Metric());
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondBody = (await second.Content.ReadFromJsonAsync<BodyMetricResponse>(Json))!;
        Assert.Equal(21.60m, secondBody.Bmi);
        await using var db = fixture.Db();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
        var logs = await db.BodyMetricLogs.Where(x => x.UserId == user).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Equal(175m, logs.Single(x => x.Id == firstBody.Id).HeightUsedCm);
        Assert.Equal(22.86m, logs.Single(x => x.Id == firstBody.Id).Bmi);
        Assert.Equal("NormalWeight", logs.Single(x => x.Id == firstBody.Id).BmiCategory);
        Assert.Equal(180m, logs.Single(x => x.Id == secondBody.Id).HeightUsedCm);
        var history = (await client.GetFromJsonAsync<MetricsHistoryResponse>("/metrics/history", Json))!;
        Assert.Equal(22.86m, history.Items.Single(x => x.Id == firstBody.Id).Bmi);
        Assert.Equal(21.60m, history.Items.Single(x => x.Id == secondBody.Id).Bmi);
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IBodyProfileProvider>().GetUserHeightCmAsync(user, default);
        Assert.Equal("OK", result.GrpcStatus); Assert.Equal(180m, result.HeightCm);
    }

    [Fact]
    public async Task ActualIdentity_DecimalHeightAndUsers_UseAuthenticatedSubjectNotBodyUserId()
    {
        var a = await fixture.SeedAsync(175.25m); var b = await fixture.SeedAsync(180m);
        using var channel = GrpcChannel.ForAddress(fixture.Address);
        // No credentials: documents the actual provider's unauthenticated service-caller trust gap.
        var reply = await new IdentityService.IdentityServiceClient(channel).GetUserHeightAsync(
            new GetUserHeightRequest { UserId = a.ToString() }, deadline: DateTime.UtcNow.AddSeconds(2));
        Assert.True(reply.HasHeightCmX100); Assert.Equal(17525, reply.HeightCmX100);
        await using var factory = fixture.Factory();
        using var clientA = fixture.Client(factory, a); using var clientB = fixture.Client(factory, b);
        using var responseA = await clientA.PostAsJsonAsync("/metrics", Metric(b));
        using var responseB = await clientB.PostAsJsonAsync("/metrics", Metric(a));
        Assert.Equal(HttpStatusCode.Created, responseA.StatusCode); Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);
        await using var db = fixture.Db();
        var logA = await db.BodyMetricLogs.SingleAsync(x => x.UserId == a);
        var logB = await db.BodyMetricLogs.SingleAsync(x => x.UserId == b);
        Assert.Equal(175.25m, logA.HeightUsedCm);
        Assert.Equal(Math.Round(70m / (1.7525m * 1.7525m), 2), logA.Bmi);
        Assert.Equal(180m, logB.HeightUsedCm); Assert.Equal(21.60m, logB.Bmi);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualIdentity_MissingUserOrProfile_Returns404WithoutPartialMetric(bool existingUser)
    {
        var user = existingUser ? await fixture.SeedAsync() : Guid.NewGuid();
        await using var factory = fixture.Factory(); using var client = fixture.Client(factory, user);
        using var response = await client.PostAsJsonAsync("/metrics", Metric());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("progress.profile_not_found", await response.Content.ReadAsStringAsync());
        await AssertEmptyAsync(user);
    }

    [Fact]
    public async Task ActualIdentity_StoppedEndpoint_FailsBoundedlyWithoutPersisting()
    {
        var user = await fixture.SeedAsync(175m);
        await fixture.StopIdentityAsync();
        try
        {
            await using var factory = fixture.Factory(); using var client = fixture.Client(factory, user);
            var timer = Stopwatch.StartNew();
            using var response = await client.PostAsJsonAsync("/metrics", Metric());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(6));
            using var scope = factory.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IBodyProfileProvider>().GetUserHeightCmAsync(user, default);
            Assert.Equal("Unavailable", result.GrpcStatus);
            await AssertEmptyAsync(user);
        }
        finally { await fixture.StartIdentityAsync(); }
    }

    [Theory]
    [InlineData(null, 422, "OK")]
    [InlineData(StatusCode.InvalidArgument, 502, "InvalidArgument")]
    [InlineData(StatusCode.Internal, 502, "Internal")]
    [InlineData(StatusCode.Unauthenticated, 502, "Unauthenticated")]
    public async Task ControlledOptionalOrFault_PreservesStatusAndSafeHttpMapping(StatusCode? fault, int expectedHttp, string expectedGrpc)
    {
        await using var server = await ControlledHeightServer.StartAsync((_, _) => fault.HasValue
            ? throw new RpcException(new Status(fault.Value, "internal-secret-url-profile-details"))
            : Task.FromResult(new GetUserHeightResponse()));
        var user = Guid.NewGuid();
        await using var factory = fixture.Factory(server.Address); using var client = fixture.Client(factory, user);
        using var response = await client.PostAsJsonAsync("/metrics", Metric());
        Assert.Equal(expectedHttp, (int)response.StatusCode);
        Assert.DoesNotContain("internal-secret", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IBodyProfileProvider>().GetUserHeightCmAsync(user, default);
        Assert.Equal(expectedGrpc, result.GrpcStatus);
        Assert.Equal(fault.HasValue ? ProfileHeightStatus.IntegrationFailure : ProfileHeightStatus.HeightNotRecorded, result.Status);
        await AssertEmptyAsync(user);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4999)]
    [InlineData(25001)]
    public async Task ControlledInvalidPresentHeight_Is502NotMissingHeight(int scaledHeight)
    {
        await using var server = await ControlledHeightServer.StartAsync((_, _) => Task.FromResult(new GetUserHeightResponse { HeightCmX100 = scaledHeight }));
        var user = Guid.NewGuid(); await using var factory = fixture.Factory(server.Address);
        using var client = fixture.Client(factory, user); using var response = await client.PostAsJsonAsync("/metrics", Metric());
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode); await AssertEmptyAsync(user);
    }

    [Fact]
    public async Task ControlledDelay_ActualDeadline_Is503WithDeadlineDiagnosticAndNoPersistence()
    {
        await using var server = await ControlledHeightServer.StartAsync(async (_, call) =>
        { await Task.Delay(TimeSpan.FromSeconds(20), call.CancellationToken); return new GetUserHeightResponse { HeightCmX100 = 17500 }; });
        var user = Guid.NewGuid(); await using var factory = fixture.Factory(server.Address, 150);
        using var client = fixture.Client(factory, user); var timer = Stopwatch.StartNew();
        using var response = await client.PostAsJsonAsync("/metrics", Metric());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.True(timer.Elapsed < TimeSpan.FromSeconds(4));
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IBodyProfileProvider>().GetUserHeightCmAsync(user, default);
        Assert.Equal("DeadlineExceeded", result.GrpcStatus); await AssertEmptyAsync(user);
    }

    [Fact]
    public async Task CallerCancellation_StopsOutboundHttp2CallAndDoesNotPersist()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await ControlledHeightServer.StartAsync(async (_, call) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(20000, call.CancellationToken); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
            return new GetUserHeightResponse { HeightCmX100 = 17500 };
        });
        var user = Guid.NewGuid(); await using var factory = fixture.Factory(server.Address, 5000);
        using var client = fixture.Client(factory, user); using var ct = new CancellationTokenSource();
        var request = client.PostAsJsonAsync("/metrics", Metric(), ct.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3)); await AssertEmptyAsync(user);
    }

    [Theory]
    [InlineData("", "2000")]
    [InlineData("ftp://identity:50051", "2000")]
    [InlineData("http://identity:50051", "0")]
    public async Task Host_InvalidGrpcConfiguration_FailsStartup(string address, string deadline)
    {
        await using var factory = new ProgressApiFactory(fixture.Progress, new Dictionary<string, string?>
        { ["IdentityGrpc:Address"] = address, ["IdentityGrpc:DeadlineMilliseconds"] = deadline });
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
    }

    private async Task AssertEmptyAsync(Guid user)
    {
        await using var db = fixture.Db(); Assert.False(await db.BodyMetricLogs.AnyAsync(x => x.UserId == user));
    }
}
