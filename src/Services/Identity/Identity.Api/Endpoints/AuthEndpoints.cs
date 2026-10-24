using Identity.Api.Extensions;
using Identity.Application.Features.Auth;
using Identity.Application.Features.Password;
using Identity.Application.Features.Registration;
using Identity.Application.Features.Verification;

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
        .AddIdentityValidation<RegisterUserRequest>()
        .WithName("RegisterUser")
        .WithSummary("Đăng ký tài khoản người dùng mới")
        .AllowAnonymous();

        group.MapGet("/verify", async (
            [AsParameters] VerifyUserRequest request,
            VerifyUserUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(request, ct);
            return TypedResults.NoContent();
        })
        .AddIdentityValidation<VerifyUserRequest>()
        .WithName("VerifyUser")
        .WithSummary("Xác minh địa chỉ email và kích hoạt tài khoản")
        .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequest request,
            LoginUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return TypedResults.Ok(result);
        })
        .AddIdentityValidation<LoginRequest>()
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
        .AddIdentityValidation<RefreshTokenRequest>()
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

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            ChangePasswordUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(request, ct);
            return TypedResults.NoContent();
        })
        .AddIdentityValidation<ChangePasswordRequest>()
        .WithName("ChangePassword")
        .WithSummary("Đổi mật khẩu của tài khoản hiện tại")
        .RequireAuthorization();

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            ForgotPasswordUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(request, ct);
            return TypedResults.Accepted((string?)null);
        })
        .AddIdentityValidation<ForgotPasswordRequest>()
        .WithName("ForgotPassword")
        .WithSummary("Yêu cầu gửi email đặt lại mật khẩu")
        .AllowAnonymous();

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            ResetPasswordUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(request, ct);
            return TypedResults.NoContent();
        })
        .AddIdentityValidation<ResetPasswordRequest>()
        .WithName("ResetPassword")
        .WithSummary("Đặt lại mật khẩu bằng token email")
        .AllowAnonymous();

        return app;
    }
}
