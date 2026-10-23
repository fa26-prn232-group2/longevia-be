namespace Identity.Domain.Events;

public sealed record UserRegisteredDomainEvent(
    Guid UserId,
    string Email,
    string FullName,
    string VerificationToken,
    DateTimeOffset OccurredOn) : IDomainEvent;
