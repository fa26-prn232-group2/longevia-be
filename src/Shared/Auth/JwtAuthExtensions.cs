using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Longevia.Shared.Auth;

public static class JwtAuthExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        services.Configure<JwtOptions>(jwtSection);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningPublicKey))
        {
            // If public key is not configured yet (e.g. initial setup), we still register auth
            // but log/throw when authentication is attempted.
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();
            return services;
        }

        var currentEcdsa = JwtKeyHelper.ImportPublicKey(jwtOptions.SigningPublicKey);
        var currentKey = new ECDsaSecurityKey(currentEcdsa) { KeyId = jwtOptions.KeyId };

        ECDsaSecurityKey? previousKey = null;
        if (!string.IsNullOrWhiteSpace(jwtOptions.PreviousPublicKey) &&
            !string.IsNullOrWhiteSpace(jwtOptions.PreviousKeyId))
        {
            var prevEcdsa = JwtKeyHelper.ImportPublicKey(jwtOptions.PreviousPublicKey);
            previousKey = new ECDsaSecurityKey(prevEcdsa) { KeyId = jwtOptions.PreviousKeyId };
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    IssuerSigningKeyResolver = (_, _, kid, _) =>
                    {
                        var keys = new List<SecurityKey>();
                        if (!string.IsNullOrWhiteSpace(kid))
                        {
                            if (kid == currentKey.KeyId)
                            {
                                keys.Add(currentKey);
                            }
                            else if (previousKey is not null && kid == previousKey.KeyId)
                            {
                                keys.Add(previousKey);
                            }
                        }
                        else
                        {
                            keys.Add(currentKey);
                        }
                        return keys;
                    },
                };
            });

        return services;
    }
}
