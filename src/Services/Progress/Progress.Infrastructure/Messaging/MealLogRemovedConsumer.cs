using Longevia.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Messaging;

public sealed class MealLogRemovedConsumer(
    IMealActivityConsumerService consumerService,
    ILogger<MealLogRemovedConsumer> logger) : IConsumer<MealLogRemovedEvent>
{
    public async Task Consume(ConsumeContext<MealLogRemovedEvent> context)
    {
        var @event = context.Message;

        var result = await consumerService.ProcessMealRemovedAsync(@event, context.CancellationToken);

        switch (result)
        {
            case MealProcessingResult.Created:
                logger.LogInformation(
                    "Processed MealLogRemovedEvent. EventId: {EventId}, MealLogId: {MealLogId}, Date: {Date}",
                    @event.EventId, @event.MealLogId, @event.BusinessDate);
                break;

            case MealProcessingResult.AlreadyProcessed:
                logger.LogInformation(
                    "MealLogRemovedEvent already processed. EventId: {EventId}, MealLogId: {MealLogId}",
                    @event.EventId, @event.MealLogId);
                break;

            case MealProcessingResult.ConflictingDuplicate:
                logger.LogError(
                    "MealLogRemovedEvent rejected due to integrity conflict. EventId: {EventId}, MealLogId: {MealLogId}",
                    @event.EventId, @event.MealLogId);
                throw new InvalidOperationException(
                    $"Conflicting MealLogRemovedEvent for MealLogId {@event.MealLogId}.");
        }
    }
}
