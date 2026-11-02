using Diet.Application.Abstractions;
using Diet.Infrastructure.Persistence;
using Longevia.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diet.Infrastructure.Messaging;

public static class DietMessagingExtensions
{
    public const string Exchange = "longevia.events";
    public const string RoutingKey = "diet.meal.logged";

    public static IServiceCollection AddDietMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IMealActivityPublisher, MealActivityPublisher>();
        services.AddScoped<MealActivityReplay>();
        services.AddScoped<MealActivitySnapshotExporter>();
        services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<DietDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
            });
            if (config.GetValue<bool>("Messaging:UseInMemory"))
            {
                bus.UsingInMemory((context, transport) => transport.ConfigureEndpoints(context));
                return;
            }
            var host = config["RabbitMQ:Host"] ?? throw new InvalidOperationException("Thiếu RabbitMQ:Host.");
            bus.UsingRabbitMq((context, transport) =>
            {
                transport.Host(host, config.GetValue<ushort>("RabbitMQ:Port", 5672), "/", credentials =>
                {
                    credentials.Username(config["RabbitMQ:User"] ?? "guest");
                    credentials.Password(config["RabbitMQ:Password"] ?? "guest");
                });
                transport.ConfigureJsonSerializerOptions(options =>
                {
                    EventJsonOptions.Configure(options);
                    return options;
                });
                transport.Message<MealLoggedEvent>(message => message.SetEntityName(Exchange));
                transport.Publish<MealLoggedEvent>(publish => publish.ExchangeType = "topic");
                transport.Send<MealLoggedEvent>(send => send.UseRoutingKeyFormatter(_ => RoutingKey));
                transport.ConfigureEndpoints(context);
            });
        });
        services.AddOptions<MassTransitHostOptions>().Configure(options => options.WaitUntilStarted = true);
        services.AddHostedService<MealActivityReplayService>();
        return services;
    }
}
