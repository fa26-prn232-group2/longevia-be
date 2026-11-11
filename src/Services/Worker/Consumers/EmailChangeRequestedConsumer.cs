using System.Security.Cryptography;
using System.Text;
using Longevia.Shared.Events;
using Longevia.Worker.Adapters.Email;
using Longevia.Worker.Idempotency;
using Longevia.Worker.Templates;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Longevia.Worker.Consumers;

/// <summary>
/// Gửi email xác nhận tới địa chỉ mới và email cảnh báo tới địa chỉ cũ khi có yêu
/// cầu đổi email. Không log token/email.
/// </summary>
public sealed class EmailChangeRequestedConsumer(
    IEmailSender emailSender,
    IIdempotencyStore idempotency,
    ILogger<EmailChangeRequestedConsumer> logger,
    IOptions<FrontendOptions> frontendOptions) : IConsumer<EmailChangeRequestedEvent>
{
    private static readonly TimeSpan DedupTtl = TimeSpan.FromDays(7);

    public Task Consume(ConsumeContext<EmailChangeRequestedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(EmailChangeRequestedEvent message,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Token)));
        var key = $"consumed:{EmailChangeRequestedEvent.SchemaVersion}:email-change:{tokenHash}";
        if (!await idempotency.TryClaimAsync(key, DedupTtl, cancellationToken))
        {
            logger.LogInformation("Duplicate EmailChangeRequestedEvent ignored");
            return;
        }

        var confirmation = EmailTemplates.EmailChangeConfirm(message.NewEmail, message.FullName,
            message.Token, message.ExpiresAt, frontendOptions.Value.BaseUrl);
        var warning = EmailTemplates.EmailChangeWarning(message.CurrentEmail, message.FullName,
            message.NewEmail);
        try
        {
            await emailSender.SendAsync(confirmation, cancellationToken);
            await emailSender.SendAsync(warning, cancellationToken);
        }
        catch
        {
            await ReleaseClaimAfterFailureAsync(key);
            throw;
        }

        logger.LogInformation("Email change confirmation dispatched");
    }

    private async Task ReleaseClaimAfterFailureAsync(string key)
    {
        try
        {
            await idempotency.ReleaseAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to release email-change idempotency claim");
        }
    }
}
