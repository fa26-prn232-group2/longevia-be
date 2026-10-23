using System.Security.Cryptography;
using System.Text;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("identity_db_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private readonly string _privateKeyB64;
    private readonly string _publicKeyB64;

    public IdentityApiFactory()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _privateKeyB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ecdsa.ExportPkcs8PrivateKeyPem()));
        _publicKeyB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ecdsa.ExportSubjectPublicKeyInfoPem()));
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Identity", _postgresContainer.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "longevia-test");
        builder.UseSetting("Jwt:Audience", "longevia-clients-test");
        builder.UseSetting("Jwt:KeyId", "test-kid-01");
        builder.UseSetting("Jwt:SigningPrivateKey", _privateKeyB64);
        builder.UseSetting("Jwt:SigningPublicKey", _publicKeyB64);
        builder.UseSetting("Jwt:AccessTokenExpiryMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenExpiryDays", "7");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<IdentityDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<IdentityDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });
        });
    }
}
