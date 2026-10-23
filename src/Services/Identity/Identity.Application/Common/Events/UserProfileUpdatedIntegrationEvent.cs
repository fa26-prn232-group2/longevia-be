namespace Identity.Application.Common.Events;

public sealed record UserProfileUpdatedIntegrationEvent(
    Guid UserId,
    DateTimeOffset UpdatedAt);
