namespace Identity.Domain.Events;

public sealed record UserProfileUpdatedDomainEvent(
    Guid UserId,
    DateTimeOffset OccurredOn) : IDomainEvent;
