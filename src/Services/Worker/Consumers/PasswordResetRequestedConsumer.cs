using System.Security.Cryptography;
using System.Text;
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
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.ResetToken)));
        var key = $"consumed:{PasswordResetRequestedEvent.SchemaVersion}:{tokenHash}";
        if (!await idempotency.TryClaimAsync(key, DedupTtl, cancellationToken))
        {
            logger.LogInformation("Duplicate PasswordResetRequestedEvent ignored");
            return;
        }

        var email = EmailTemplates.PasswordReset(message.Email, message.ResetToken, message.ExpiresAt);
        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch
        {
            await ReleaseClaimAfterFailureAsync(key);
            throw;
        }

        logger.LogInformation("Password reset email dispatched");
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
            logger.LogError(exception, "Failed to release password reset email idempotency claim");
        }
    }
}
