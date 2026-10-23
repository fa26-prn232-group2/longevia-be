using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common.Dtos;
using Identity.Application.Features.Auth;
using Identity.Application.Features.Registration;
using Identity.Application.Features.Users;
using Identity.Domain.Constants;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.IntegrationTests;

public sealed class AuthAndUserIntegrationTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Startup_AppliesMigrationsIdempotently_AndCreatesExpectedSchema()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Contains("20261021212343_InitialIdentityDb", appliedMigrations);
        Assert.Contains("20261023193116_ReconcileMassTransitOutboxModel", appliedMigrations);

        await db.Database.MigrateAsync();

        var appliedAfterRepeat = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(appliedMigrations, appliedAfterRepeat);

        var tables = await db.Database.SqlQueryRaw<string>(
                """SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'public'""")
            .ToListAsync();
        Assert.Contains("users", tables);
        Assert.Contains("user_profiles", tables);
        Assert.Contains("refresh_tokens", tables);
        Assert.Contains("InboxState", tables);
        Assert.Contains("OutboxMessage", tables);
        Assert.Contains("OutboxState", tables);

        var indexDefinitions = await db.Database.SqlQueryRaw<string>(
                """SELECT indexdef AS "Value" FROM pg_indexes WHERE schemaname = 'public' AND indexname IN ('IX_users_Email', 'IX_user_profiles_UserId', 'IX_refresh_tokens_Token')""")
            .ToListAsync();
        Assert.Equal(3, indexDefinitions.Count);
        Assert.All(indexDefinitions, index => Assert.Contains("UNIQUE INDEX", index.ToUpperInvariant()));

        var outboxIndexes = await db.Database.SqlQueryRaw<string>(
                """SELECT indexname AS "Value" FROM pg_indexes WHERE schemaname = 'public' AND indexname IN ('IX_OutboxState_Created', 'IX_OutboxMessage_EnqueueTime', 'IX_OutboxMessage_ExpirationTime')""")
            .ToListAsync();
        Assert.Equal(3, outboxIndexes.Count);

        var legacyBusNameColumn = await db.Database.SqlQueryRaw<string>(
                """SELECT column_name AS "Value" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'OutboxState' AND column_name = 'BusName'""")
            .ToListAsync();
        Assert.Empty(legacyBusNameColumn);
    }

    [Fact]
    public async Task FullUserLifecycle_Register_Login_GetMe_UpdateProfile_Refresh_Logout()
    {
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";
        var fullName = "Nguyen Van A";

        // 1. Register
        var registerRequest = new RegisterUserRequest(email, password, fullName);
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var createdUser = await registerResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(createdUser);
        Assert.Equal(email, createdUser.Email);
        Assert.Equal(fullName, createdUser.FullName);

        // 2. Duplicate Register (Conflict)
        var duplicateResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        // 3. A newly registered account cannot log in before email verification.
        var pendingLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, pendingLogin.StatusCode);

        // The verification endpoint is implemented in a later Identity phase.
        // Activate through the domain model here so this test can cover the remaining lifecycle.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
            user.Verify(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        // 4. Login after verification
        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var tokenResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(tokenResult);
        Assert.False(string.IsNullOrWhiteSpace(tokenResult.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokenResult.RefreshToken));

        // 5. Invalid Password Login
        var badLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "WrongPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        // 6. Get Current User (/users/me)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
        var meResponse = await _client.GetAsync("/users/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meUser = await meResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(meUser);
        Assert.Equal(createdUser.Id, meUser.Id);

        // 7. Update Profile (/users/me/profile)
        var updateProfileRequest = new UpdateProfileRequest(
            new DateOnly(1995, 6, 15),
            BiologicalSex.Male,
            178,
            ActivityLevel.ModeratelyActive,
            DietaryPreference.Mediterranean,
            ["Seafood", "Peanuts"]);

        var profileResponse = await _client.PutAsJsonAsync("/users/me/profile", updateProfileRequest);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var updatedProfile = await profileResponse.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(updatedProfile);
        Assert.Equal(178, updatedProfile.HeightCm);
        Assert.Equal(BiologicalSex.Male, updatedProfile.BiologicalSex);
        Assert.Contains("Seafood", updatedProfile.Allergies);

        // 8. Refresh Token
        var refreshRequest = new RefreshTokenRequest(tokenResult.RefreshToken);
        var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(newTokens);
        Assert.NotEqual(tokenResult.RefreshToken, newTokens.RefreshToken);

        // 9. Logout
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);
        var logoutResponse = await _client.PostAsJsonAsync("/auth/logout", new LogoutRequest(newTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
    }
}
