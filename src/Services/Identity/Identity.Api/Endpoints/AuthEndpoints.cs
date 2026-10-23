using Identity.Application.Features.Auth;
using Identity.Application.Features.Registration;
using Longevia.Shared.Validation;

namespace Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Authentication");

        group.MapPost("/register", async (
            RegisterUserRequest request,
            RegisterUserUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return TypedResults.Created($"/users/{result.Id}", result);
        })
        .AddEndpointFilter<ValidationFilter<RegisterUserRequest>>()
        .WithName("RegisterUser")
        .WithSummary("Đăng ký tài khoản người dùng mới")
        .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequest request,
            LoginUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return TypedResults.Ok(result);
        })
        .AddEndpointFilter<ValidationFilter<LoginRequest>>()
        .WithName("Login")
        .WithSummary("Đăng nhập lấy cặp Access/Refresh Token")
        .AllowAnonymous();

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            RefreshTokenUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return TypedResults.Ok(result);
        })
        .AddEndpointFilter<ValidationFilter<RefreshTokenRequest>>()
        .WithName("RefreshToken")
        .WithSummary("Đổi Access Token mới qua Refresh Token")
        .AllowAnonymous();

        group.MapPost("/logout", async (
            LogoutRequest request,
            LogoutUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(request, ct);
            return TypedResults.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Thu hồi Refresh Token")
        .RequireAuthorization();

        return app;
    }
}
