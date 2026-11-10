using Longevia.Shared.Events;
using Longevia.Worker.Adapters.Email;
using Longevia.Worker.Idempotency;
using Longevia.Worker.Templates;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Longevia.Worker.Consumers;

/// <summary>Gửi email chào mừng sau khi tài khoản được xác minh, idempotent theo event.</summary>
public sealed class UserVerifiedConsumer(IEmailSender emailSender, IIdempotencyStore idempotency,
    ILogger<UserVerifiedConsumer> logger) : IConsumer<UserVerifiedEvent>
{
    private static readonly TimeSpan DedupTtl = TimeSpan.FromDays(7);

    public Task Consume(ConsumeContext<UserVerifiedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(UserVerifiedEvent message, CancellationToken cancellationToken = default)
    {
        var key = $"consumed:{UserVerifiedEvent.SchemaVersion}:verified:{message.UserId}:{message.OccurredOn.Ticks}";
        if (!await idempotency.TryClaimAsync(key, DedupTtl, cancellationToken))
        {
            logger.LogInformation("Duplicate UserVerifiedEvent ignored");
            return;
        }

        var email = EmailTemplates.Welcome(message.Email, message.FullName);
        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch
        {
            await idempotency.ReleaseAsync(key, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Welcome email dispatched");
    }
}
