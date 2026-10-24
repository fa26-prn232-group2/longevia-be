using Longevia.Shared.Events;
using Longevia.Worker.Adapters.Email;
using Longevia.Worker.Idempotency;
using Longevia.Worker.Templates;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Longevia.Worker.Consumers;

/// <summary>
/// Renders and sends the verification email for <see cref="UserRegisteredEvent"/>.
/// Duplicate deliveries are acked without resending (Redis dedup, 7-day TTL);
/// failures bubble up to the MassTransit retry policy (2s/5s/10s) and then to
/// the DLX/DLQ declared in <c>Program.cs</c>.
/// NEVER logs tokens, emails, or bodies — log lines carry outcomes only.
/// </summary>
public sealed class UserRegisteredConsumer(
    IEmailSender emailSender,
    IIdempotencyStore idempotency,
    ILogger<UserRegisteredConsumer> logger) : IConsumer<UserRegisteredEvent>
{
    private static readonly TimeSpan DedupTtl = TimeSpan.FromDays(7);

    /// <inheritdoc />
    public Task Consume(ConsumeContext<UserRegisteredEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    /// <summary>
    /// Core handling, kept MassTransit-free so unit tests can call it directly
    /// with fakes (no broker, no real SMTP).
    /// </summary>
    public async Task HandleAsync(UserRegisteredEvent message, CancellationToken cancellationToken = default)
    {
        var key = $"consumed:{UserRegisteredEvent.SchemaVersion}:{message.UserId}:{message.OccurredOn.Ticks}";
        if (!await idempotency.TryClaimAsync(key, DedupTtl, cancellationToken))
        {
            logger.LogInformation("Duplicate UserRegisteredEvent ignored");
            return;
        }

        var email = EmailTemplates.Verification(message.Email, message.FullName, message.VerificationToken);
        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch
        {
            await ReleaseClaimAfterFailureAsync(key);
            throw;
        }

        logger.LogInformation("Verification email dispatched");
    }

    private async Task ReleaseClaimAfterFailureAsync(string key)
    {
        try
        {
            // The message cancellation token is already canceled on some failed
            // deliveries; cleanup must still run before MassTransit retries.
            await idempotency.ReleaseAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Preserve the SMTP exception so MassTransit can apply its retry/DLQ
            // policy. This log intentionally contains no message/token data.
            logger.LogError(exception, "Failed to release verification email idempotency claim");
        }
    }
}
