using Longevia.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progress.Application.Abstractions;
using Progress.Infrastructure.Services;

namespace Progress.Infrastructure.Messaging;

public static class ProgressMessagingExtensions
{
    public static IServiceCollection AddProgressMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IMealActivityConsumerService, MealActivityConsumerService>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<MealLoggedConsumer>();

            if (config.GetValue<bool>("Messaging:UseInMemory"))
            {
                bus.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
                return;
            }

            var rabbitHost = config["RabbitMQ:Host"]
                ?? config["RABBITMQ_HOST"]
                ?? throw new InvalidOperationException("RabbitMQ:Host is required.");
            var portText = config["RabbitMQ:Port"] ?? config["RABBITMQ_PORT"] ?? "5672";
            if (!int.TryParse(portText, out var rabbitPort))
                throw new InvalidOperationException("RabbitMQ:Port must be a valid port.");
            var rabbitUser = config["RabbitMQ:User"] ?? config["RABBITMQ_USER"];
            var rabbitPassword = config["RabbitMQ:Password"] ?? config["RABBITMQ_PASSWORD"];
            if (string.IsNullOrWhiteSpace(rabbitHost) || string.IsNullOrWhiteSpace(rabbitUser)
                || string.IsNullOrWhiteSpace(rabbitPassword) || rabbitPort is < 1 or > 65535)
                throw new InvalidOperationException("Explicit valid RabbitMQ host, port, user and password are required.");

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri($"rabbitmq://{rabbitHost}:{rabbitPort}/"), h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPassword);
                });

                cfg.ConfigureJsonSerializerOptions(options =>
                {
                    EventJsonOptions.Configure(options);
                    return options;
                });

                cfg.Message<MealLoggedEvent>(m => m.SetEntityName(ProgressMessagingTopology.EventsExchange));
                cfg.Publish<MealLoggedEvent>(p => p.ExchangeType = "topic");

                cfg.ReceiveEndpoint(ProgressMessagingTopology.MealActivityQueue, endpoint =>
                {
                    endpoint.PrefetchCount = ProgressMessagingTopology.PrefetchCount;
                    endpoint.ConfigureConsumeTopology = false;

                    endpoint.Bind<MealLoggedEvent>(b =>
                    {
                        b.ExchangeType = "topic";
                        b.RoutingKey = ProgressMessagingTopology.MealActivityRoutingKey;
                    });

                    endpoint.UseMessageRetry(r => r.Intervals(ProgressMessagingTopology.RetryIntervals));
                    endpoint.BindDeadLetterQueue(
                        ProgressMessagingTopology.DeadLetterQueue,
                        ProgressMessagingTopology.DeadLetterExchange);

                    endpoint.ConfigureConsumer<MealLoggedConsumer>(context);
                });
            });
        });

        return services;
    }
}
