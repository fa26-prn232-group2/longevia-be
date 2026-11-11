using FluentValidation;
using Identity.Application.Features.Auth;
using Identity.Application.Features.Password;
using Identity.Application.Features.Registration;
using Identity.Application.Features.Users;
using Identity.Application.Features.Verification;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<RegisterUserUseCase>();
        services.AddScoped<VerifyUserUseCase>();
        services.AddScoped<ResendVerificationUseCase>();
        services.AddScoped<ChangePasswordUseCase>();
        services.AddScoped<ForgotPasswordUseCase>();
        services.AddScoped<ResetPasswordUseCase>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<LogoutUseCase>();
        services.AddScoped<GetCurrentUserUseCase>();
        services.AddScoped<UpdateProfileUseCase>();
        services.AddScoped<ChangeFullNameUseCase>();
        services.AddScoped<RequestEmailChangeUseCase>();
        services.AddScoped<ConfirmEmailChangeUseCase>();
        services.AddScoped<PreferenceUseCases>();

        return services;
    }
}
