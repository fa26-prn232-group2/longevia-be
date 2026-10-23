using Identity.Application.Abstractions;
using MassTransit;

namespace Identity.Infrastructure.Messaging;

public sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class =>
        publishEndpoint.Publish(@event, ct);
}
