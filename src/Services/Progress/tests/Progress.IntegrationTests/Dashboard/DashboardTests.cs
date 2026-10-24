using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Progress.Application.Abstractions;
using Progress.Application.Features.Dashboard;
using Progress.Application.Features.Fasting;
using Progress.Application.Features.Metrics;
using Progress.Application.Features.Sleep;
using Progress.Application.Features.Water;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace Progress.IntegrationTests.Dashboard;

[Collection("Progress foundation")]
public sealed class DashboardTests(ProgressFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private sealed class StubBodyProfileProvider(ProfileHeightResult result) : IBodyProfileProvider
    {
        public Task<ProfileHeightResult> GetUserHeightCmAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(result);
    }

    [Fact]
    public async Task Dashboard_Summary_AggregatesAllMetricsAndStreakCorrectly()
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

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // 1. Log metrics: Initial (72kg, 14 days ago), Baseline (70kg, 8 days ago), Current (68.5kg, today)
        // Delta so với tuần trước (baseline 70kg) => 68.5 - 70.0 = -1.5 kg
        await client.PostAsJsonAsync("/metrics", new LogBodyMetricRequest(now.AddDays(-14), 72.0m, null, null, null, null, null));
        await client.PostAsJsonAsync("/metrics", new LogBodyMetricRequest(now.AddDays(-8), 70.0m, null, null, null, null, null));
        await client.PostAsJsonAsync("/metrics", new LogBodyMetricRequest(now, 68.5m, null, null, null, null, null));

        // 2. Add Meal activity contribution (Diet.Meal) for yesterday to create streak
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
            db.ActivityContributions.Add(ActivityContribution.Create(
                userId, "Diet.Meal", Guid.NewGuid().ToString(), today.AddDays(-1), now.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        // 3. Log water (water should NOT create streak independently per PRD)
        await client.PostAsJsonAsync("/water/log", new LogWaterRequest(1000));

        // 4. Log sleep (8 hours)
        await client.PostAsJsonAsync("/sleep/log", new LogSleepRequest(now.AddHours(-10), now.AddHours(-2), "Good"));

        // 5. Fasting session completed this week
        var startFasting = await client.PostAsJsonAsync("/fasting/start", new StartFastingRequest("16:8", now.AddHours(-18), 16));
        var fastingResult = await startFasting.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(fastingResult);
        await client.PostAsJsonAsync("/fasting/end", new EndFastingRequest(fastingResult.SessionId, now.AddHours(-1)));

        // 6. Query Dashboard Summary
        var summaryResp = await client.GetAsync("/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, summaryResp.StatusCode);

        var summary = await summaryResp.Content.ReadFromJsonAsync<DashboardSummaryResponse>(JsonOpts);
        Assert.NotNull(summary);

        Assert.Equal(68.5m, summary.CurrentWeightKg);
        Assert.Equal(72.0m, summary.InitialWeightKg);
        Assert.Equal(-1.5m, summary.WeightDeltaKg);
        Assert.Equal(22.37m, summary.CurrentBmi); // 68.5 / (1.75^2) = 22.367 => 22.37
        Assert.Equal(2, summary.CurrentStreakDays); // Yesterday (Meal) + Today (Metric) = 2
        Assert.True(summary.BestStreakDays >= 2);
        Assert.Equal(1, summary.FastingThisWeekCompleted);
        Assert.Equal(8.0, summary.AverageSleepHours);
    }
}
