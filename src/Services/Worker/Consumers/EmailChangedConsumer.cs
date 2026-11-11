using Longevia.Shared.Events;
using Longevia.Worker.Adapters.Email;
using Longevia.Worker.Idempotency;
using Longevia.Worker.Templates;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Longevia.Worker.Consumers;

/// <summary>Gửi thông báo hoàn tất đổi email tới địa chỉ cũ.</summary>
public sealed class EmailChangedConsumer(
    IEmailSender emailSender,
    IIdempotencyStore idempotency,
    ILogger<EmailChangedConsumer> logger) : IConsumer<EmailChangedEvent>
{
    private static readonly TimeSpan DedupTtl = TimeSpan.FromDays(7);

    public Task Consume(ConsumeContext<EmailChangedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(EmailChangedEvent message,
        CancellationToken cancellationToken = default)
    {
        var key = $"consumed:{EmailChangedEvent.SchemaVersion}:email-changed:{message.UserId}:{message.OccurredOn.Ticks}";
        if (!await idempotency.TryClaimAsync(key, DedupTtl, cancellationToken))
        {
            logger.LogInformation("Duplicate EmailChangedEvent ignored");
            return;
        }

        // Tên hiển thị không có trong event; dùng lời chào trung tính.
        var email = EmailTemplates.EmailChanged(message.OldEmail, "bạn", message.NewEmail);
        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch
        {
            await ReleaseClaimAfterFailureAsync(key);
            throw;
        }

        logger.LogInformation("Email changed notice dispatched");
    }

    private async Task ReleaseClaimAfterFailureAsync(string key)
    {
        try
        {
            await idempotency.ReleaseAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to release email-changed idempotency claim");
        }
    }
}
