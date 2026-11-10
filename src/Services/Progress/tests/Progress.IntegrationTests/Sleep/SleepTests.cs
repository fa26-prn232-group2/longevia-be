using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Progress.Application.Features.Sleep;

namespace Progress.IntegrationTests.Sleep;

[Collection("Progress foundation")]
public sealed class SleepTests(ProgressFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Sleep_Log_8HoursOvernight_CalculatesDurationAndWakeDate()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var sleptAt = new FoundationTimeProvider().GetUtcNow().AddHours(-10);
        var wokeAt = new FoundationTimeProvider().GetUtcNow().AddHours(-2);

        var request = new LogSleepRequest(sleptAt, wokeAt, "Good");

        var response = await client.PostAsJsonAsync("/sleep/log", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SleepResponse>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal(8.0, result.DurationHours);
        Assert.Equal("Good", result.SleepQuality);
        Assert.Equal(Progress.Domain.UtcCalendar.Day(wokeAt), result.Date);
    }

    [Fact]
    public async Task Sleep_Log_Overlap_Returns409Conflict()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var sleptAt = new FoundationTimeProvider().GetUtcNow().AddHours(-10);
        var wokeAt = new FoundationTimeProvider().GetUtcNow().AddHours(-2);
        await client.PostAsJsonAsync("/sleep/log", new LogSleepRequest(sleptAt, wokeAt, "Good"));

        // Overlapping: sleptAt rơi vào giữa khoảng giấc ngủ trước
        var overlapRequest = new LogSleepRequest(sleptAt.AddHours(2), wokeAt.AddHours(2), "Fair");
        var response = await client.PostAsJsonAsync("/sleep/log", overlapRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Sleep_Log_ConcurrentOverlappingRequests_OnlyOneSucceeds()
    {
        var userId = Guid.NewGuid();
        var sleptAt = new FoundationTimeProvider().GetUtcNow().AddHours(-10);
        var wokeAt = new FoundationTimeProvider().GetUtcNow().AddHours(-2);

        // Bắn 5 request cùng một khoảng thời gian đồng thời
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var client = CreateClient(userId);
            return await client.PostAsJsonAsync("/sleep/log", new LogSleepRequest(sleptAt, wokeAt, "Good"));
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, createdCount);
        Assert.Equal(4, conflictCount);
    }

    [Fact]
    public async Task Sleep_Log_WokeBeforeSlept_Returns400BadRequest()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var sleptAt = new FoundationTimeProvider().GetUtcNow().AddHours(-2);
        var wokeAt = new FoundationTimeProvider().GetUtcNow().AddHours(-5);

        var response = await client.PostAsJsonAsync("/sleep/log", new LogSleepRequest(sleptAt, wokeAt, "Good"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sleep_TwoUsers_DataIsIsolated()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        using var clientA = CreateClient(userA);
        using var clientB = CreateClient(userB);

        var sleptAt = new FoundationTimeProvider().GetUtcNow().AddHours(-8);
        var wokeAt = new FoundationTimeProvider().GetUtcNow().AddHours(-1);

        // User A logs sleep
        var respA = await clientA.PostAsJsonAsync("/sleep/log", new LogSleepRequest(sleptAt, wokeAt, "Good"));
        Assert.Equal(HttpStatusCode.Created, respA.StatusCode);

        // User B logs same time period - should NOT conflict because users are isolated
        var respB = await clientB.PostAsJsonAsync("/sleep/log", new LogSleepRequest(sleptAt, wokeAt, "Good"));
        Assert.Equal(HttpStatusCode.Created, respB.StatusCode);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(fixture, "valid", userId));
        return client;
    }

    [Fact]
    public async Task Sleep_Today_ReturnsLatestRecordForWakeDate()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var empty = await client.GetAsync("/sleep/today");
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);

        var sleptAt = new FoundationTimeProvider().GetUtcNow().AddHours(-9);
        var wokeAt = new FoundationTimeProvider().GetUtcNow().AddHours(-1);
        var logged = await client.PostAsJsonAsync("/sleep/log", new LogSleepRequest(sleptAt, wokeAt, "Good"));
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);
        var created = await logged.Content.ReadFromJsonAsync<SleepResponse>(JsonOpts);

        var today = await client.GetFromJsonAsync<SleepResponse>("/sleep/today", JsonOpts);
        Assert.NotNull(today);
        Assert.Equal(created!.Id, today.Id);
    }

    [Fact]
    public async Task Sleep_History_ReturnsPagedRecentFirst()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var sleptAt = new FoundationTimeProvider().GetUtcNow().AddHours(-9);
        var wokeAt = new FoundationTimeProvider().GetUtcNow().AddHours(-1);
        var logged = await client.PostAsJsonAsync("/sleep/log", new LogSleepRequest(sleptAt, wokeAt, "Good"));
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);
        var created = await logged.Content.ReadFromJsonAsync<SleepResponse>(JsonOpts);

        var history = await client.GetFromJsonAsync<SleepHistoryPageResponse>(
            "/sleep/history?page=1&pageSize=10", JsonOpts);
        Assert.NotNull(history);
        Assert.True(history.TotalCount >= 1);
        Assert.Contains(history.Items, item => item.Id == created!.Id);

        var empty = await client.GetFromJsonAsync<SleepHistoryPageResponse>(
            "/sleep/history?page=999&pageSize=10", JsonOpts);
        Assert.NotNull(empty);
        Assert.Empty(empty.Items);
    }
}
