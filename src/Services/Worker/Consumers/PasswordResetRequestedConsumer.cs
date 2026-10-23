using Longevia.Shared.Events;
using Longevia.Worker.Adapters.Email;
using Longevia.Worker.Idempotency;
using Longevia.Worker.Templates;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Longevia.Worker.Consumers;

/// <summary>
/// Renders and sends the password-reset email for
/// <see cref="PasswordResetRequestedEvent"/>.
/// Duplicate deliveries are acked without resending (Redis dedup, 7-day TTL);
/// failures bubble up to the MassTransit retry policy (2s/5s/10s) and then to
/// the DLX/DLQ declared in <c>Program.cs</c>.
/// NEVER logs tokens, emails, or bodies — log lines carry outcomes only.
/// </summary>
public sealed class PasswordResetRequestedConsumer(
    IEmailSender emailSender,
    IIdempotencyStore idempotency,
    ILogger<PasswordResetRequestedConsumer> logger) : IConsumer<PasswordResetRequestedEvent>
{
    private static readonly TimeSpan DedupTtl = TimeSpan.FromDays(7);

    /// <inheritdoc />
    public Task Consume(ConsumeContext<PasswordResetRequestedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    /// <summary>
    /// Core handling, kept MassTransit-free so unit tests can call it directly
    /// with fakes (no broker, no real SMTP).
    /// </summary>
    public async Task HandleAsync(PasswordResetRequestedEvent message, CancellationToken cancellationToken = default)
    {
        var key = $"consumed:{PasswordResetRequestedEvent.SchemaVersion}:{message.Email}:{message.OccurredOn.Ticks}";
        if (!await idempotency.TryClaimAsync(key, DedupTtl, cancellationToken))
        {
            logger.LogInformation("Duplicate PasswordResetRequestedEvent ignored");
            return;
        }

        var email = EmailTemplates.PasswordReset(message.Email, message.ResetToken, message.ExpiresAt);
        await emailSender.SendAsync(email, cancellationToken);
        logger.LogInformation("Password reset email dispatched");
    }
}
