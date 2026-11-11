using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common.Dtos;
using Identity.Application.Features.Auth;
using Identity.Application.Features.Registration;
using Identity.Application.Features.Users;

namespace Identity.IntegrationTests;

public sealed class EmailChangeIntegrationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task FullFlow_ChangesLoginEmail_AndRevokesOldSessions()
    {
        var email = $"change-{Guid.NewGuid():N}@example.com";
        var newEmail = $"new-{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        await RegisterAndVerify(email, password);
        using var client = await Login(email, password);

        var request = await client.PostAsJsonAsync("/users/me/email-change",
            new RequestEmailChangeRequest(newEmail, password));
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);

        var changeEvent = factory.EmailChangeRequestedEvents.Last(e =>
            e.CurrentEmail == email && e.NewEmail == newEmail);
        Assert.Equal("Tên Người Dùng", changeEvent.FullName);

        var confirm = await factory.CreateClient().PostAsJsonAsync("/auth/confirm-email-change",
            new ConfirmEmailChangeRequest(changeEvent.Token));
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var oldLogin = await factory.CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await factory.CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest(newEmail, password));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        // Token dùng một lần.
        var replay = await factory.CreateClient().PostAsJsonAsync("/auth/confirm-email-change",
            new ConfirmEmailChangeRequest(changeEvent.Token));
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Contains("identity.invalid_email_change_token", await replay.Content.ReadAsStringAsync());

        var changedEvent = factory.EmailChangedEvents.Last(e => e.NewEmail == newEmail);
        Assert.Equal(email, changedEvent.OldEmail);
    }

    [Fact]
    public async Task Request_WithWrongPassword_SameEmailOrTakenEmail_IsRejected()
    {
        var email = $"guard-{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        await RegisterAndVerify(email, password);
        using var client = await Login(email, password);

        var wrongPassword = await client.PostAsJsonAsync("/users/me/email-change",
            new RequestEmailChangeRequest($"next-{Guid.NewGuid():N}@example.com", "WrongPassword123!"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Contains("identity.invalid_current_password",
            await wrongPassword.Content.ReadAsStringAsync());

        var sameEmail = await client.PostAsJsonAsync("/users/me/email-change",
            new RequestEmailChangeRequest(email, password));
        Assert.Equal(HttpStatusCode.BadRequest, sameEmail.StatusCode);
        Assert.Contains("identity.email_unchanged", await sameEmail.Content.ReadAsStringAsync());

        var takenEmail = $"taken-{Guid.NewGuid():N}@example.com";
        await RegisterAndVerify(takenEmail, password);
        var taken = await client.PostAsJsonAsync("/users/me/email-change",
            new RequestEmailChangeRequest(takenEmail, password));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Contains("identity.email_taken", await taken.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Request_TwiceWithinCooldown_Returns429()
    {
        var email = $"cooldown-{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        await RegisterAndVerify(email, password);
        using var client = await Login(email, password);

        var first = await client.PostAsJsonAsync("/users/me/email-change",
            new RequestEmailChangeRequest($"one-{Guid.NewGuid():N}@example.com", password));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var second = await client.PostAsJsonAsync("/users/me/email-change",
            new RequestEmailChangeRequest($"two-{Guid.NewGuid():N}@example.com", password));
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Contains("identity.email_change_too_frequent",
            await second.Content.ReadAsStringAsync());
    }

    private async Task RegisterAndVerify(string email, string password)
    {
        await factory.CreateClient().PostAsJsonAsync("/auth/register",
            new RegisterUserRequest(email, password, "Tên Người Dùng"));
        var registered = factory.RegisteredEvents.Last(e => e.Email == email);
        var verify = await factory.CreateClient().GetAsync(
            "/auth/verify?token=" + Uri.EscapeDataString(registered.VerificationToken));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
    }

    private async Task<HttpClient> Login(string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", tokens!.AccessToken);
        return client;
    }
}
