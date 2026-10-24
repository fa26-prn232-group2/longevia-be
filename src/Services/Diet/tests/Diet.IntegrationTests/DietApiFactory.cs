using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace Diet.IntegrationTests;

public sealed class DietApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("diet_db_test").WithUsername("diet_test").WithPassword("diet_test_password").Build();
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public Task InitializeAsync() => _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        _key.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Diet", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "diet-test");
        builder.UseSetting("Jwt:Audience", "diet-clients-test");
        builder.UseSetting("Jwt:KeyId", "diet-test-key");
        builder.UseSetting("Jwt:SigningPublicKey",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(_key.ExportSubjectPublicKeyInfoPem())));
    }

    public HttpClient UserClient(Guid userId, string? subject = null)
    {
        var client = CreateClient();
        var token = new JwtSecurityToken("diet-test", "diet-clients-test",
            [new Claim("sub", subject ?? userId.ToString())], expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new ECDsaSecurityKey(_key) { KeyId = "diet-test-key" },
                SecurityAlgorithms.EcdsaSha256));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
