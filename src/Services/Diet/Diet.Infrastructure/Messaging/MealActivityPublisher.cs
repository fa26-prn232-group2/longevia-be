using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using Longevia.Shared.Events;
using MassTransit;

namespace Diet.Infrastructure.Messaging;

public sealed class MealActivityPublisher(IPublishEndpoint endpoint, TimeProvider time) : IMealActivityPublisher
{
    public Task PublishAsync(MealLog meal, CancellationToken ct) =>
        endpoint.Publish(new MealLoggedEvent(meal.Id, meal.UserId, meal.Id, meal.Date,
            meal.CreatedAt, time.GetUtcNow()), context => context.MessageId = meal.Id, ct);
}
