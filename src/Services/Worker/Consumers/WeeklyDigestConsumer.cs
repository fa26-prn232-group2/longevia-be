using Longevia.Shared.Events;
using Longevia.Worker.Adapters.Email;
using Longevia.Worker.Idempotency;
using Longevia.Worker.Templates;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Longevia.Worker.Consumers;

/// <summary>Gửi báo cáo tuần từ số liệu đã tổng hợp sẵn trong event.</summary>
public sealed class WeeklyDigestConsumer(IEmailSender emailSender, IIdempotencyStore idempotency,
    ILogger<WeeklyDigestConsumer> logger) : IConsumer<WeeklyDigestReadyEvent>
{
    private static readonly TimeSpan DedupTtl = TimeSpan.FromDays(14);

    public Task Consume(ConsumeContext<WeeklyDigestReadyEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(WeeklyDigestReadyEvent message, CancellationToken cancellationToken = default)
    {
        var key = $"consumed:{WeeklyDigestReadyEvent.SchemaVersion}:digest:{message.EventId}";
        if (!await idempotency.TryClaimAsync(key, DedupTtl, cancellationToken))
        {
            logger.LogInformation("Duplicate WeeklyDigestReadyEvent ignored");
            return;
        }

        var email = EmailTemplates.WeeklyDigest(message.Email, message.FullName,
            new WeeklyDigestData($"{message.WeekStart} – {message.WeekEnd}", message.AverageCalories,
                message.AverageFastingHours * 7, message.CurrentStreak));
        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch
        {
            await idempotency.ReleaseAsync(key, CancellationToken.None);
            throw;
        }
        logger.LogInformation("Weekly digest email dispatched");
    }
}
