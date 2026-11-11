using Diet.Domain.Entities;

namespace Diet.Application.Abstractions;

public interface IMealActivityPublisher
{
    Task PublishAsync(MealLog meal, CancellationToken ct);
    Task PublishRemovedAsync(MealLog meal, CancellationToken ct);
}
