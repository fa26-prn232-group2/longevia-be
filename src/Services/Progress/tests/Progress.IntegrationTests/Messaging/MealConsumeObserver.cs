using System.Collections.Concurrent;
using Longevia.Shared.Events;
using MassTransit;

namespace Progress.IntegrationTests.Messaging;

public sealed class MealConsumeObserver : IConsumeObserver
{
    public ConcurrentDictionary<Guid, (string Exchange, string Key, bool Redelivered)> Received { get; } = new();
    public ConcurrentDictionary<Guid, bool> Completed { get; } = new();
    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        if (context.Message is MealLoggedEvent meal && context.ReceiveContext.TryGetPayload<RabbitMqBasicConsumeContext>(out var rabbit))
            Received.AddOrUpdate(meal.EventId, (rabbit.Exchange, rabbit.RoutingKey ?? "", context.ReceiveContext.Redelivered),
                (_, previous) => (rabbit.Exchange, rabbit.RoutingKey ?? "", previous.Redelivered || context.ReceiveContext.Redelivered));
        return Task.CompletedTask;
    }
    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        if (context.Message is MealLoggedEvent meal) Completed[meal.EventId] = true;
        return Task.CompletedTask;
    }
    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class => Task.CompletedTask;
}
