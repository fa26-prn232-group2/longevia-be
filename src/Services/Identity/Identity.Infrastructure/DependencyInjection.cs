using Identity.Application.Abstractions;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Messaging;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Repositories;
using Identity.Infrastructure.Security;
using Longevia.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Identity")
            ?? "Host=localhost;Port=5432;Database=identity_db;Username=identity_user;Password=change-me";

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, Es256JwtTokenGenerator>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        services.AddMassTransit(busConfig =>
        {
            busConfig.AddEntityFrameworkOutbox<IdentityDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            var rabbitHost = configuration["RABBITMQ_HOST"] ?? configuration["RabbitMQ:Host"];
            if (!string.IsNullOrWhiteSpace(rabbitHost))
            {
                busConfig.UsingRabbitMq((ctx, cfg) =>
                {
                    var portRaw = configuration["RABBITMQ_PORT"] ?? configuration["RabbitMQ:Port"];
                    var port = ushort.TryParse(portRaw, out var p) ? p : (ushort)5672;
                    var user = configuration["RABBITMQ_USER"] ?? configuration["RabbitMQ:User"] ?? "guest";
                    var pass = configuration["RABBITMQ_PASSWORD"] ?? configuration["RabbitMQ:Password"] ?? "guest";

                    cfg.Host(rabbitHost, port, "/", h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });

                    cfg.ConfigureJsonSerializerOptions(options =>
                    {
                        EventJsonOptions.Configure(options);
                        return options;
                    });

                    cfg.Message<UserRegisteredEvent>(message => message.SetEntityName(IdentityEventTopology.EventsExchange));
                    cfg.Publish<UserRegisteredEvent>(publish => publish.ExchangeType = "topic");
                    cfg.Send<UserRegisteredEvent>(send =>
                        send.UseRoutingKeyFormatter(_ => IdentityEventTopology.UserRegisteredRoutingKey));

                    cfg.Message<PasswordResetRequestedEvent>(message => message.SetEntityName(IdentityEventTopology.EventsExchange));
                    cfg.Publish<PasswordResetRequestedEvent>(publish => publish.ExchangeType = "topic");
                    cfg.Send<PasswordResetRequestedEvent>(send =>
                        send.UseRoutingKeyFormatter(_ => IdentityEventTopology.PasswordResetRoutingKey));

                    cfg.Message<UserProfileUpdatedEvent>(message => message.SetEntityName(IdentityEventTopology.EventsExchange));
                    cfg.Publish<UserProfileUpdatedEvent>(publish => publish.ExchangeType = "topic");
                    cfg.Send<UserProfileUpdatedEvent>(send =>
                        send.UseRoutingKeyFormatter(_ => IdentityEventTopology.UserProfileUpdatedRoutingKey));

                    cfg.ConfigureEndpoints(ctx);
                });
            }
            else
            {
                busConfig.UsingInMemory((ctx, cfg) =>
                {
                    cfg.ConfigureEndpoints(ctx);
                });
            }
        });

        return services;
    }
}
