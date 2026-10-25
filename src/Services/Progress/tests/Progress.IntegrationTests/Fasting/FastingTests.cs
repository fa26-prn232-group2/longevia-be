using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Progress.Application.Features.Fasting;

namespace Progress.IntegrationTests.Fasting;

[Collection("Progress foundation")]
public sealed class FastingTests(ProgressFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Fasting_Lifecycle_StartCurrentEnd_975MinutesPasses()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var startedAt = new FoundationTimeProvider().GetUtcNow().AddMinutes(-975);
        var startRequest = new StartFastingRequest("16:8", startedAt, 16);

        // 1. Start fasting
        using var startResponse = await client.PostAsJsonAsync("/fasting/start", startRequest);
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var started = await startResponse.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(started);
        Assert.Equal("16:8", started.Protocol);
        Assert.Equal("InProgress", started.Status);

        // 2. Get current
        using var currentResponse = await client.GetAsync("/fasting/current");
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        var current = await currentResponse.Content.ReadFromJsonAsync<CurrentFastingResponse>(JsonOpts);
        Assert.NotNull(current);
        Assert.Equal(started.SessionId, current.SessionId);
        Assert.True(current.ElapsedHours >= 16.25);
        Assert.Equal(0, current.RemainingHours);

        // 3. End fasting (975 phút = 16.25 giờ)
        var endedAt = startedAt.AddMinutes(975);
        var endRequest = new EndFastingRequest(started.SessionId, endedAt);
        using var endResponse = await client.PostAsJsonAsync("/fasting/end", endRequest);
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
        var ended = await endResponse.Content.ReadFromJsonAsync<EndFastingResponse>(JsonOpts);
        Assert.NotNull(ended);
        Assert.Equal(16.25, ended.ActualHours);
        Assert.True(ended.IsGoalReached);
        Assert.Equal("Completed", ended.Status);

        // 4. Current should now be null
        using var afterEndCurrent = await client.GetAsync("/fasting/current");
        Assert.Equal(HttpStatusCode.OK, afterEndCurrent.StatusCode);
        var content = await afterEndCurrent.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrWhiteSpace(content) || content == "null");
    }

    [Fact]
    public async Task Fasting_Start_WhenActiveExists_Returns409Conflict()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var startedAt = new FoundationTimeProvider().GetUtcNow().AddHours(-2);
        var startRequest = new StartFastingRequest("18:6", startedAt, 18);

        using var firstResponse = await client.PostAsJsonAsync("/fasting/start", startRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Second start while active
        using var secondResponse = await client.PostAsJsonAsync("/fasting/start", startRequest);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Fasting_Concurrent20Starts_OnlyOneSucceeds()
    {
        var userId = Guid.NewGuid();
        var startedAt = new FoundationTimeProvider().GetUtcNow().AddHours(-1);

        // Bắn 20 request start đồng thời
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var client = CreateClient(userId);
            return await client.PostAsJsonAsync("/fasting/start", new StartFastingRequest("16:8", startedAt, 16));
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, createdCount);
        Assert.Equal(19, conflictCount);
    }

    [Fact]
    public async Task Fasting_Ownership_CannotEndOtherUserSession_Returns404NotFound()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = CreateClient(userA);
        using var clientB = CreateClient(userB);

        var startA = await clientA.PostAsJsonAsync("/fasting/start",
            new StartFastingRequest("16:8", new FoundationTimeProvider().GetUtcNow().AddHours(-2), 16));
        var sessionA = await startA.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(sessionA);

        // User B cố tình kết thúc session của User A
        var endB = await clientB.PostAsJsonAsync("/fasting/end",
            new EndFastingRequest(sessionA.SessionId, new FoundationTimeProvider().GetUtcNow()));

        // Ownership hiding: trả về 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, endB.StatusCode);
    }

    [Fact]
    public async Task Fasting_End_EndedBeforeStarted_Returns400BadRequest()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var startedAt = new FoundationTimeProvider().GetUtcNow().AddHours(-2);
        var startResp = await client.PostAsJsonAsync("/fasting/start", new StartFastingRequest("16:8", startedAt, 16));
        var session = await startResp.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(session);

        var endResp = await client.PostAsJsonAsync("/fasting/end",
            new EndFastingRequest(session.SessionId, startedAt.AddMinutes(-30)));

        Assert.Equal(HttpStatusCode.BadRequest, endResp.StatusCode);
    }

    [Fact]
    public async Task Fasting_End_RetryWithSameEndedAt_Returns200OK()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var startedAt = new FoundationTimeProvider().GetUtcNow().AddHours(-16);
        var startResp = await client.PostAsJsonAsync("/fasting/start", new StartFastingRequest("16:8", startedAt, 16));
        var session = await startResp.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(session);

        var endedAt = startedAt.AddHours(16);
        var endReq = new EndFastingRequest(session.SessionId, endedAt);

        // First end
        var endResp1 = await client.PostAsJsonAsync("/fasting/end", endReq);
        Assert.Equal(HttpStatusCode.OK, endResp1.StatusCode);
        var ended1 = await endResp1.Content.ReadFromJsonAsync<EndFastingResponse>(JsonOpts);
        Assert.NotNull(ended1);
        Assert.Equal("Completed", ended1.Status);

        // Replay retry with same endedAt
        var endResp2 = await client.PostAsJsonAsync("/fasting/end", endReq);
        Assert.Equal(HttpStatusCode.OK, endResp2.StatusCode);
        var ended2 = await endResp2.Content.ReadFromJsonAsync<EndFastingResponse>(JsonOpts);
        Assert.NotNull(ended2);
        Assert.Equal(ended1.ActualHours, ended2.ActualHours);
        Assert.Equal(ended1.IsGoalReached, ended2.IsGoalReached);
    }

    [Fact]
    public async Task Fasting_End_RetryWithDifferentEndedAt_Returns409Conflict()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var startedAt = new FoundationTimeProvider().GetUtcNow().AddHours(-18);
        var startResp = await client.PostAsJsonAsync("/fasting/start", new StartFastingRequest("16:8", startedAt, 16));
        var session = await startResp.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(session);

        var endedAt1 = startedAt.AddHours(16);
        var endResp1 = await client.PostAsJsonAsync("/fasting/end", new EndFastingRequest(session.SessionId, endedAt1));
        Assert.Equal(HttpStatusCode.OK, endResp1.StatusCode);

        // Retry with a DIFFERENT endedAt (18 hours instead of 16)
        var endedAt2 = startedAt.AddHours(18);
        var endResp2 = await client.PostAsJsonAsync("/fasting/end", new EndFastingRequest(session.SessionId, endedAt2));
        Assert.Equal(HttpStatusCode.Conflict, endResp2.StatusCode);
    }

    [Fact]
    public async Task Fasting_End_ConcurrentDifferentEndedAt_OnlyOneSucceeds()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var startedAt = new FoundationTimeProvider().GetUtcNow().AddHours(-18);
        var startResp = await client.PostAsJsonAsync("/fasting/start", new StartFastingRequest("16:8", startedAt, 16));
        var session = await startResp.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(session);

        var endedAtA = startedAt.AddHours(16);
        var endedAtB = startedAt.AddHours(17);

        var taskA = Task.Run(async () =>
        {
            using var c = CreateClient(userId);
            return await c.PostAsJsonAsync("/fasting/end", new EndFastingRequest(session.SessionId, endedAtA));
        });
        var taskB = Task.Run(async () =>
        {
            using var c = CreateClient(userId);
            return await c.PostAsJsonAsync("/fasting/end", new EndFastingRequest(session.SessionId, endedAtB));
        });

        var results = await Task.WhenAll(taskA, taskB);

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictCount);
    }

    [Fact]
    public async Task Fasting_End_ConcurrentIdenticalEndedAt_BothSucceedWith200OK()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(userId);

        var startedAt = new FoundationTimeProvider().GetUtcNow().AddHours(-16);
        var startResp = await client.PostAsJsonAsync("/fasting/start", new StartFastingRequest("16:8", startedAt, 16));
        var session = await startResp.Content.ReadFromJsonAsync<FastingSessionResponse>(JsonOpts);
        Assert.NotNull(session);

        var endedAt = startedAt.AddHours(16);

        var taskA = Task.Run(async () =>
        {
            using var c = CreateClient(userId);
            return await c.PostAsJsonAsync("/fasting/end", new EndFastingRequest(session.SessionId, endedAt));
        });
        var taskB = Task.Run(async () =>
        {
            using var c = CreateClient(userId);
            return await c.PostAsJsonAsync("/fasting/end", new EndFastingRequest(session.SessionId, endedAt));
        });

        var results = await Task.WhenAll(taskA, taskB);

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(fixture, "valid", userId));
        return client;
    }
}
