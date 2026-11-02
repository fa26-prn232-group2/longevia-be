using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Progress.Application.Abstractions;
using Progress.Application.Features.Metrics;

namespace Progress.IntegrationTests.Metrics;

[Collection("Progress foundation")]
public sealed class MetricsTests(ProgressFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private sealed class StubBodyProfileProvider(ProfileHeightResult result) : IBodyProfileProvider
    {
        public Task<ProfileHeightResult> GetUserHeightCmAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(result);
    }

    [Fact]
    public async Task Metrics_Log_70Kg175Cm_Returns201WithAccurateSnapshotBmi()
    {
        var userId = Guid.NewGuid();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IBodyProfileProvider>(_ =>
                    new StubBodyProfileProvider(new ProfileHeightResult(ProfileHeightStatus.Success, 175.0m))));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", userId));

        var request = new LogBodyMetricRequest(
            new FoundationTimeProvider().GetUtcNow(),
            70.0m,
            BodyFatPercent: 18.5m,
            WaistCircumferenceCm: 80.0m,
            SystolicBloodPressure: 120,
            DiastolicBloodPressure: 80,
            RestingHeartRate: 65);

        var response = await client.PostAsJsonAsync("/metrics", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BodyMetricResponse>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal(70.0m, result.WeightKg);
        Assert.Equal(22.86m, result.Bmi);
        Assert.Equal("NormalWeight", result.BmiCategory);
        Assert.Equal(18.5m, result.BodyFatPercent);
        Assert.Equal(80.0m, result.WaistCircumferenceCm);
        Assert.Equal(120, result.SystolicBloodPressure);
        Assert.Equal(80, result.DiastolicBloodPressure);
        Assert.Equal(65, result.RestingHeartRate);
    }

    [Fact]
    public async Task Metrics_Log_WhenHeightNotRecorded_Returns422Unprocessable()
    {
        var userId = Guid.NewGuid();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IBodyProfileProvider>(_ =>
                    new StubBodyProfileProvider(new ProfileHeightResult(ProfileHeightStatus.HeightNotRecorded))));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", userId));

        var request = new LogBodyMetricRequest(
            new FoundationTimeProvider().GetUtcNow(),
            70.0m,
            null, null, null, null, null);

        var response = await client.PostAsJsonAsync("/metrics", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_Log_WhenProfileNotFound_Returns404NotFound()
    {
        var userId = Guid.NewGuid();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IBodyProfileProvider>(_ =>
                    new StubBodyProfileProvider(new ProfileHeightResult(ProfileHeightStatus.ProfileNotFound))));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", userId));

        var request = new LogBodyMetricRequest(
            new FoundationTimeProvider().GetUtcNow(),
            70.0m,
            null, null, null, null, null);

        var response = await client.PostAsJsonAsync("/metrics", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_Log_WhenIdentityUnavailable_Returns503ServiceUnavailable()
    {
        var userId = Guid.NewGuid();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IBodyProfileProvider>(_ =>
                    new StubBodyProfileProvider(new ProfileHeightResult(ProfileHeightStatus.ServiceUnavailable))));
            });
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", userId));

        var request = new LogBodyMetricRequest(
            new FoundationTimeProvider().GetUtcNow(),
            70.0m,
            null, null, null, null, null);

        var response = await client.PostAsJsonAsync("/metrics", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_History_PaginationAndUserIsolation()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IBodyProfileProvider>(_ =>
                    new StubBodyProfileProvider(new ProfileHeightResult(ProfileHeightStatus.Success, 170.0m))));
            });
        });

        using var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", userA));

        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(fixture, "valid", userB));

        // Log 3 bản ghi cho User A
        var baseTime = new FoundationTimeProvider().GetUtcNow().AddDays(-5);
        for (int i = 0; i < 3; i++)
        {
            await clientA.PostAsJsonAsync("/metrics", new LogBodyMetricRequest(
                baseTime.AddDays(i), 70.0m + i, null, null, null, null, null));
        }

        // Log 1 bản ghi cho User B
        await clientB.PostAsJsonAsync("/metrics", new LogBodyMetricRequest(
            baseTime, 80.0m, null, null, null, null, null));

        // User A query history with pageSize = 2
        var historyA = await clientA.GetFromJsonAsync<MetricsHistoryResponse>("/metrics/history?pageNumber=1&pageSize=2", JsonOpts);
        Assert.NotNull(historyA);
        Assert.Equal(3, historyA.TotalCount);
        Assert.Equal(2, historyA.TotalPages);
        Assert.Equal(2, historyA.Items.Count);
        // Order by LoggedAt descending: bản ghi ngày 2 (+2) phải đứng đầu
        Assert.Equal(72.0m, historyA.Items[0].WeightKg);

        // User B query history
        var historyB = await clientB.GetFromJsonAsync<MetricsHistoryResponse>("/metrics/history", JsonOpts);
        Assert.NotNull(historyB);
        Assert.Equal(1, historyB.TotalCount);
        Assert.Equal(80.0m, historyB.Items[0].WeightKg);
    }
}
