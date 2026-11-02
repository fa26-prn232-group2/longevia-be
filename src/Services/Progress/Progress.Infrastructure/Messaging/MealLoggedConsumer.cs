using Longevia.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Messaging;

public sealed class MealLoggedConsumer(
    IMealActivityConsumerService consumerService,
    ILogger<MealLoggedConsumer> logger) : IConsumer<MealLoggedEvent>
{
    public async Task Consume(ConsumeContext<MealLoggedEvent> context)
    {
        var @event = context.Message;

        var result = await consumerService.ProcessMealLoggedAsync(@event, context.CancellationToken);

        switch (result)
        {
            case MealProcessingResult.Created:
                logger.LogInformation(
                    "Processed MealLoggedEvent successfully. EventId: {EventId}, MealLogId: {MealLogId}, Date: {Date}",
                    @event.EventId, @event.MealLogId, @event.BusinessDate);
                break;

            case MealProcessingResult.AlreadyProcessed:
                logger.LogInformation(
                    "MealLoggedEvent already processed (idempotent duplicate). EventId: {EventId}, MealLogId: {MealLogId}",
                    @event.EventId, @event.MealLogId);
                break;

            case MealProcessingResult.ConflictingDuplicate:
                logger.LogError(
                    "MealLoggedEvent rejected due to data integrity conflict with existing meal projection. EventId: {EventId}, MealLogId: {MealLogId}",
                    @event.EventId, @event.MealLogId);
                throw new InvalidOperationException($"Conflicting duplicate MealLoggedEvent detected for MealLogId {@event.MealLogId}.");
        }
    }
}
