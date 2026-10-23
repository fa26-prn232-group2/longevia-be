namespace Identity.Infrastructure.Messaging;

/// <summary>RabbitMQ topology contract consumed by the Worker email queue.</summary>
internal static class IdentityEventTopology
{
    public const string EventsExchange = "longevia.events";
    public const string UserRegisteredRoutingKey = "identity.user.registered";
}
