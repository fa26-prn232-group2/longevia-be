using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Longevia.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

public sealed class Es256JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public Es256JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.SigningPrivateKey))
        {
            throw new InvalidOperationException(
                "Biến môi trường 'JWT__SIGNING_PRIVATE_KEY' chưa được cấu hình. " +
                "Identity Service yêu cầu Private Key để ký Access Token.");
        }

        var ecdsa = JwtKeyHelper.ImportPrivateKey(_options.SigningPrivateKey);
        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = _options.KeyId };
        _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);
    }

    public string GenerateAccessToken(User user, IReadOnlyList<string>? roles = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (roles is not null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var handler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = _signingCredentials,
        };

        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken(Guid userId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(_options.RefreshTokenExpiryDays);

        return RefreshToken.Create(Guid.NewGuid(), userId, token, expiresAt, now);
    }
}
