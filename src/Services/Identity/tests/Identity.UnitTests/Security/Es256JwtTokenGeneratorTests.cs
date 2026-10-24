using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Identity.Domain.Entities;
using Identity.Infrastructure.Security;
using Longevia.Shared.Auth;
using Microsoft.Extensions.Options;

namespace Identity.UnitTests.Security;

public sealed class Es256JwtTokenGeneratorTests
{
    private readonly Es256JwtTokenGenerator _sut;
    private readonly JwtOptions _options;

    public Es256JwtTokenGeneratorTests()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePem = ecdsa.ExportPkcs8PrivateKeyPem();
        var publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();

        _options = new JwtOptions
        {
            Issuer = "longevia-test",
            Audience = "longevia-test-clients",
            KeyId = "test-key-01",
            SigningPrivateKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(privatePem)),
            SigningPublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicPem)),
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7,
        };

        _sut = new Es256JwtTokenGenerator(Options.Create(_options));
    }

    [Fact]
    public void GenerateAccessToken_ShouldProduceValidJwtWithES256AndKid()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "alex.minh@example.com",
            "hashed_pass",
            "Alex Minh",
            DateTimeOffset.UtcNow,
            "hash",
            DateTimeOffset.UtcNow.AddHours(24));

        var tokenString = _sut.GenerateAccessToken(user);

        Assert.False(string.IsNullOrWhiteSpace(tokenString));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        Assert.Equal("ES256", token.Header.Alg);
        Assert.Equal(_options.KeyId, token.Header.Kid);
        Assert.Equal(_options.Issuer, token.Issuer);
        Assert.Equal(_options.Audience, token.Audiences.First());
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal(user.Email, token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldProduceActiveToken()
    {
        var userId = Guid.NewGuid();

        var token = _sut.GenerateRefreshToken(userId);

        Assert.NotNull(token);
        Assert.Equal(userId, token.UserId);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.True(token.IsActive(DateTimeOffset.UtcNow));
        Assert.False(token.IsRevoked);
    }
}
