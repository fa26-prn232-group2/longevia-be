namespace Longevia.Worker.Messaging;

/// <summary>
/// Single source of truth for the MassTransit/RabbitMQ names declared in
/// <c>Program.cs</c> (data-model §1.3, event-driven-design §Topology):
/// topic exchange <c>longevia.events</c>, queue <c>worker.email</c>,
/// routing keys <c>identity.user.*</c>, DLX/DLQ, prefetch 10,
/// retry 3x (2s, 5s, 10s).
/// </summary>
public static class WorkerTopology
{
    /// <summary>Business topic exchange shared by all async events.</summary>
    public const string EventsExchange = "longevia.events";

    /// <summary>Durable queue feeding the email consumers.</summary>
    public const string EmailQueue = "worker.email";

    /// <summary>Dead-letter exchange for exhausted messages.</summary>
    public const string DeadLetterExchange = "worker.email.dlx";

    /// <summary>Dead-letter queue inspected by operators.</summary>
    public const string DeadLetterQueue = "worker.email.dlq";

    /// <summary>Routing key for <c>UserRegisteredEvent</c>.</summary>
    public const string UserRegisteredRoutingKey = "identity.user.registered";

    /// <summary>Routing key for <c>PasswordResetRequestedEvent</c>.</summary>
    public const string PasswordResetRoutingKey = "identity.user.password-reset-requested";

    /// <summary>Prefetch limit so one consumer cannot hog the queue.</summary>
    public const int PrefetchCount = 10;

    /// <summary>Retry intervals before a message goes to the DLQ (spec Q3).</summary>
    public static readonly TimeSpan[] RetryIntervals =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
    ];
}
