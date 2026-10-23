using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common.Dtos;
using Identity.Application.Features.Auth;
using Identity.Application.Features.Registration;
using Identity.Application.Features.Users;
using Identity.Domain.Constants;

namespace Identity.IntegrationTests;

public sealed class AuthAndUserIntegrationTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

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

        // 3. Login
        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var tokenResult = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(tokenResult);
        Assert.False(string.IsNullOrWhiteSpace(tokenResult.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokenResult.RefreshToken));

        // 4. Invalid Password Login
        var badLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "WrongPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        // 5. Get Current User (/users/me)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
        var meResponse = await _client.GetAsync("/users/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meUser = await meResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(meUser);
        Assert.Equal(createdUser.Id, meUser.Id);

        // 6. Update Profile (/users/me/profile)
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

        // 7. Refresh Token
        var refreshRequest = new RefreshTokenRequest(tokenResult.RefreshToken);
        var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(newTokens);
        Assert.NotEqual(tokenResult.RefreshToken, newTokens.RefreshToken);

        // 8. Logout
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);
        var logoutResponse = await _client.PostAsJsonAsync("/auth/logout", new LogoutRequest(newTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
    }
}
