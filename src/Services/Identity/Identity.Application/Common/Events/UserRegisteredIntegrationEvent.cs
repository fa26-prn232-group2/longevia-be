namespace Identity.Application.Common.Events;

public sealed record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email,
    string FullName,
    string VerificationToken,
    DateTimeOffset CreatedAt);
