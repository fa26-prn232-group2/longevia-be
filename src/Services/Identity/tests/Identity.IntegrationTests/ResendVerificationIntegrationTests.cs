using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Identity.Application.Features.Registration;
using Identity.Domain.Constants;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.IntegrationTests;

public sealed class ResendVerificationIntegrationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Resend_ForPendingUser_IssuesNewTokenAndReturns202()
    {
        var email = $"resend-{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        var register = await _client.PostAsJsonAsync("/auth/register",
            new RegisterUserRequest(email, password, "Resend User"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var first = factory.RegisteredEvents.Last(e => e.Email == email);

        var resend = await _client.PostAsJsonAsync("/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);

        var second = factory.RegisteredEvents.Last(e => e.Email == email);
        Assert.NotEqual(first.VerificationToken, second.VerificationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.NotNull(user.VerificationTokenHash);
    }

    [Fact]
    public async Task Resend_ForActiveUser_DoesNotSendAnotherEmail()
    {
        var email = $"active-{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        await _client.PostAsJsonAsync("/auth/register",
            new RegisterUserRequest(email, password, "Active User"));

        var registered = factory.RegisteredEvents.Last(e => e.Email == email);
        var verify = await _client.GetAsync(
            "/auth/verify?token=" + Uri.EscapeDataString(registered.VerificationToken));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);

        var before = factory.RegisteredEvents.Count(e => e.Email == email);
        var resend = await _client.PostAsJsonAsync("/auth/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.Equal(before, factory.RegisteredEvents.Count(e => e.Email == email));
    }

    [Fact]
    public async Task Resend_UnknownEmail_StillReturns202WithoutSending()
    {
        var email = $"missing-{Guid.NewGuid():N}@example.com";
        var before = factory.RegisteredEvents.Count;

        var resend = await _client.PostAsJsonAsync("/auth/resend-verification", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.Equal(before, factory.RegisteredEvents.Count);
    }
}
