using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Progress.Application.Features.Water;

namespace Progress.IntegrationTests.Water;

[Collection("Progress foundation")]
public sealed class WaterTests(ProgressFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Water_Log_350Plus650_Totals1000MlAndCalculatesPercent()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        // 1. Initial today should be 0 consumed
        var initialResp = await client.GetAsync("/water/today");
        Assert.Equal(HttpStatusCode.OK, initialResp.StatusCode);
        var initial = await initialResp.Content.ReadFromJsonAsync<WaterSummaryResponse>(JsonOpts);
        Assert.NotNull(initial);
        Assert.Equal(0, initial.ConsumedMl);
        Assert.Equal(2500, initial.TargetMl);
        Assert.Equal(0, initial.PercentComplete);

        // 2. Log 350 ml
        var log1Resp = await client.PostAsJsonAsync("/water/log", new LogWaterRequest(350));
        Assert.Equal(HttpStatusCode.OK, log1Resp.StatusCode);
        var log1 = await log1Resp.Content.ReadFromJsonAsync<WaterSummaryResponse>(JsonOpts);
        Assert.NotNull(log1);
        Assert.Equal(350, log1.ConsumedMl);
        Assert.Equal(14.0, log1.PercentComplete); // 350 / 2500 * 100 = 14%

        // 3. Log 650 ml
        var log2Resp = await client.PostAsJsonAsync("/water/log", new LogWaterRequest(650));
        Assert.Equal(HttpStatusCode.OK, log2Resp.StatusCode);
        var log2 = await log2Resp.Content.ReadFromJsonAsync<WaterSummaryResponse>(JsonOpts);
        Assert.NotNull(log2);
        Assert.Equal(1000, log2.ConsumedMl);
        Assert.Equal(40.0, log2.PercentComplete); // 1000 / 2500 * 100 = 40%

        // 4. GET /water/today matches total
        var todayResp = await client.GetAsync("/water/today");
        Assert.Equal(HttpStatusCode.OK, todayResp.StatusCode);
        var today = await todayResp.Content.ReadFromJsonAsync<WaterSummaryResponse>(JsonOpts);
        Assert.NotNull(today);
        Assert.Equal(1000, today.ConsumedMl);
        Assert.Equal(40.0, today.PercentComplete);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [InlineData(5001)]
    public async Task Water_Log_InvalidAmount_Returns400BadRequest(int invalidAmount)
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var response = await client.PostAsJsonAsync("/water/log", new LogWaterRequest(invalidAmount));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Water_TwoUsers_DataIsIsolated()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        using var clientA = CreateClient(userA);
        using var clientB = CreateClient(userB);

        await clientA.PostAsJsonAsync("/water/log", new LogWaterRequest(500));
        await clientB.PostAsJsonAsync("/water/log", new LogWaterRequest(800));

        var summaryA = await clientA.GetFromJsonAsync<WaterSummaryResponse>("/water/today", JsonOpts);
        var summaryB = await clientB.GetFromJsonAsync<WaterSummaryResponse>("/water/today", JsonOpts);

        Assert.NotNull(summaryA);
        Assert.NotNull(summaryB);
        Assert.Equal(500, summaryA.ConsumedMl);
        Assert.Equal(800, summaryB.ConsumedMl);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(fixture, "valid", userId));
        return client;
    }
}
