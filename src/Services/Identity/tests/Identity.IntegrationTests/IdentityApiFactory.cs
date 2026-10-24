using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using Longevia.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public ConcurrentQueue<UserRegisteredEvent> RegisteredEvents { get; } = new();
    public ConcurrentQueue<PasswordResetRequestedEvent> PasswordResetEvents { get; } = new();
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("identity_db_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private readonly string _privateKeyB64;
    private readonly string _publicKeyB64;

    public string? RabbitHost { get; set; }
    public int? RabbitPort { get; set; }
    public string? RabbitUser { get; set; }
    public string? RabbitPassword { get; set; }

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
        if (RabbitHost is not null && RabbitPort is not null)
        {
            builder.UseSetting("RABBITMQ_HOST", RabbitHost);
            builder.UseSetting("RABBITMQ_PORT", RabbitPort.Value.ToString());
            builder.UseSetting("RABBITMQ_USER", RabbitUser ?? "guest");
            builder.UseSetting("RABBITMQ_PASSWORD", RabbitPassword ?? "guest");
        }
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddScoped<IEventPublisher>(provider => new CapturingEventPublisher(
                provider.GetRequiredService<IPublishEndpoint>(),
                RegisteredEvents,
                PasswordResetEvents));

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

    private sealed class CapturingEventPublisher(
        IPublishEndpoint publishEndpoint,
        ConcurrentQueue<UserRegisteredEvent> registeredEvents,
        ConcurrentQueue<PasswordResetRequestedEvent> passwordResetEvents) : IEventPublisher
    {
        public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        {
            if (@event is UserRegisteredEvent registeredEvent)
            {
                registeredEvents.Enqueue(registeredEvent);
            }
            else if (@event is PasswordResetRequestedEvent resetEvent)
            {
                passwordResetEvents.Enqueue(resetEvent);
            }

            return publishEndpoint.Publish(@event, ct);
        }
    }
}
