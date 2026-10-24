using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Progress.IntegrationTests;

[Collection("Progress foundation")]
public sealed class AuthTests(ProgressFixture fixture)
{
    [Theory]
    [InlineData("no-header")]
    [InlineData("signature")]
    [InlineData("algorithm")]
    [InlineData("unsigned")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("missing-sub")]
    [InlineData("name-id-only")]
    [InlineData("conflicting-sub")]
    [InlineData("duplicate-sub")]
    [InlineData("invalid-sub")]
    [InlineData("empty-sub")]
    [InlineData("unknown-kid")]
    [InlineData("missing-kid")]
    public async Task Authentication_InvalidToken_ReturnsUnauthorized(string scenario)
    {
        using var client = Client(scenario, Guid.NewGuid());
        using var response = await client.GetAsync("/_test/subject");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("previous")]
    public async Task Authentication_ValidCurrentOrPreviousKey_UsesVerifiedSubject(string scenario)
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using var client = Client(scenario, first);
        using var other = Client(scenario, second);
        var one = await client.GetFromJsonAsync<JsonElement>($"/_test/subject?userId={second}");
        var two = await other.GetFromJsonAsync<JsonElement>($"/_test/subject?userId={first}");
        Assert.Equal(first, one.GetProperty("userId").GetGuid());
        Assert.Equal(second, two.GetProperty("userId").GetGuid());
        Assert.True(one.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(new FoundationTimeProvider().GetUtcNow(), one.GetProperty("now").GetDateTimeOffset());
    }

    private HttpClient Client(string scenario, Guid userId)
    {
        var client = fixture.Factory.CreateClient();
        if (scenario != "no-header")
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                TestTokens.Create(fixture, scenario, userId));
        }
        return client;
    }
}
