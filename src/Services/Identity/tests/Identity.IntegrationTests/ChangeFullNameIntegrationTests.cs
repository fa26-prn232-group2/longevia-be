using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common.Dtos;
using Identity.Application.Features.Auth;
using Identity.Application.Features.Registration;
using Identity.Application.Features.Users;

namespace Identity.IntegrationTests;

public sealed class ChangeFullNameIntegrationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task AuthenticatedUser_CanChangeDisplayName()
    {
        var email = $"name-{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        await factory.CreateClient().PostAsJsonAsync("/auth/register",
            new RegisterUserRequest(email, password, "Old Name"));

        var registered = factory.RegisteredEvents.Last(e => e.Email == email);
        await factory.CreateClient().GetAsync(
            "/auth/verify?token=" + Uri.EscapeDataString(registered.VerificationToken));

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var patch = await client.PatchAsJsonAsync("/users/me", new ChangeFullNameRequest("  Tên Mới  "));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Tên Mới", updated.FullName);

        var me = await client.GetFromJsonAsync<UserResponse>("/users/me");
        Assert.Equal("Tên Mới", me!.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankName_Returns422ValidationFailed(string fullName)
    {
        using var client = await CreateAuthenticatedClient();

        var patch = await client.PatchAsJsonAsync("/users/me", new ChangeFullNameRequest(fullName));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, patch.StatusCode);
        Assert.Contains("identity.validation_failed", await patch.Content.ReadAsStringAsync());
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var email = $"name-{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        await factory.CreateClient().PostAsJsonAsync("/auth/register",
            new RegisterUserRequest(email, password, "Old Name"));
        var registered = factory.RegisteredEvents.Last(e => e.Email == email);
        await factory.CreateClient().GetAsync(
            "/auth/verify?token=" + Uri.EscapeDataString(registered.VerificationToken));

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", tokens!.AccessToken);
        return client;
    }
}
