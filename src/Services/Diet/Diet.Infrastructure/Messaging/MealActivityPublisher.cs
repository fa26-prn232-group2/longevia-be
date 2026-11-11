using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using Longevia.Shared.Events;
using MassTransit;

namespace Diet.Infrastructure.Messaging;

public sealed class MealActivityPublisher(IPublishEndpoint endpoint, TimeProvider time) : IMealActivityPublisher
{
    public Task PublishAsync(MealLog meal, CancellationToken ct) =>
        endpoint.Publish(new MealLoggedEvent(meal.Id, meal.UserId, meal.Id, meal.Date,
            meal.CreatedAt, time.GetUtcNow()), context =>
        {
            context.MessageId = meal.Id;
            context.SetRoutingKey(DietMessagingExtensions.RoutingKey);
        }, ct);

    public Task PublishRemovedAsync(MealLog meal, CancellationToken ct)
    {
        // Một MealLog chỉ bị xoá một lần; EventId mới (khác MealLogId) để consumer
        // phân biệt với sự kiện "logged" và không đụng dedup key cũ.
        var eventId = Guid.NewGuid();
        return endpoint.Publish(
            new MealLogRemovedEvent(eventId, meal.UserId, meal.Id, meal.Date, time.GetUtcNow()),
            context =>
            {
                context.MessageId = eventId;
                context.SetRoutingKey(DietMessagingExtensions.RemovalRoutingKey);
            },
            ct);
    }
}
