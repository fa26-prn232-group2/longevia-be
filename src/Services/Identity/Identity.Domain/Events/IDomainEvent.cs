namespace Identity.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
