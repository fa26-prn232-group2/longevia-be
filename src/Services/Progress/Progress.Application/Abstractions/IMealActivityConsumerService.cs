using Longevia.Shared.Events;

namespace Progress.Application.Abstractions;

public enum MealProcessingResult
{
    Created,
    AlreadyProcessed,
    ConflictingDuplicate
}

public interface IMealActivityConsumerService
{
    Task<MealProcessingResult> ProcessMealLoggedAsync(MealLoggedEvent @event, CancellationToken ct);
    Task<MealProcessingResult> ProcessMealRemovedAsync(MealLogRemovedEvent @event, CancellationToken ct);
}
