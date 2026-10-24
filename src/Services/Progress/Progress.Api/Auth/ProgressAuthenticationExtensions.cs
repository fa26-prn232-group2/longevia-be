using System.Security.Claims;
using Longevia.Shared.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Progress.Api.Auth;

public static class ProgressAuthenticationExtensions
{
    public static IServiceCollection AddProgressAuthentication(this IServiceCollection services, IConfiguration config)
    {
        // Validate before Shared imports any PEM, including a mistakenly supplied private key.
        var validator = new ProgressJwtOptionsValidator();
        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var result = validator.Validate(null, jwt);
        if (result.Failed)
        {
            throw new OptionsValidationException(JwtOptions.SectionName, typeof(JwtOptions), result.Failures);
        }
        services.AddSingleton<IValidateOptions<JwtOptions>>(validator);
        services.AddOptions<JwtOptions>().ValidateOnStart();
        services.AddJwtAuthentication(config);
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            var resolver = options.TokenValidationParameters.IssuerSigningKeyResolver;
            options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                string.IsNullOrWhiteSpace(kid) ? [] : resolver!(token, securityToken, kid, parameters);
            options.Events.OnTokenValidated = context =>
            {
                var subjects = context.Principal?.FindAll("sub").ToArray() ?? [];
                if (subjects.Length != 1 || !Guid.TryParse(subjects[0].Value, out var userId) || userId == Guid.Empty
                    || context.Principal!.FindAll(ClaimTypes.NameIdentifier).Any(claim => claim.Value != subjects[0].Value))
                {
                    context.Fail("Chủ thể xác thực không hợp lệ.");
                }
                return Task.CompletedTask;
            };
        });
        services.AddAuthorization();
        return services;
    }
}
