using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Progress.IntegrationTests;

public static class TestTokens
{
    public static string Create(ProgressFixture fixture, string scenario, Guid userId)
    {
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var key = scenario == "signature" ? otherKey : scenario == "previous" ? fixture.PreviousKey : fixture.CurrentKey;
        var kid = scenario switch { "unknown-kid" => "unknown", "missing-kid" => null, "previous" => "previous", _ => "current" };
        var claims = scenario == "name-id-only" ? [new Claim(ClaimTypes.NameIdentifier, userId.ToString())]
            : scenario == "missing-sub" ? Array.Empty<Claim>()
            : new[] { new Claim("sub", scenario == "invalid-sub" ? "invalid" : scenario == "empty-sub" ? Guid.Empty.ToString() : userId.ToString()) };
        if (scenario == "conflicting-sub")
        {
            claims = [.. claims, new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())];
        }
        if (scenario == "duplicate-sub")
        {
            claims = [.. claims, new Claim("sub", Guid.NewGuid().ToString())];
        }
        SigningCredentials? credentials = scenario == "unsigned" ? null : new SigningCredentials(
            new ECDsaSecurityKey(key) { KeyId = kid }, SecurityAlgorithms.EcdsaSha256);
        if (scenario == "algorithm")
        {
            credentials = new SigningCredentials(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32))
            { KeyId = "current" }, SecurityAlgorithms.HmacSha256);
        }
        var token = new JwtSecurityToken(scenario == "issuer" ? "wrong" : "progress-tests",
            scenario == "audience" ? "wrong" : "progress-test-clients", claims,
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: scenario == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
