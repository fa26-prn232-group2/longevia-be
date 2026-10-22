namespace Longevia.Worker.Adapters.Email;

/// <summary>
/// Outbound email port. Implementations deliver only — rendering is done by
/// <c>EmailTemplates</c> from the event payload (render-only Worker, no
/// business aggregation).
/// NEVER log tokens, passwords, or message bodies at call sites or inside
/// implementations (see <c>SmtpEmailSender</c>).
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends an already-rendered email.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Rendered email, ready to deliver (data-model §2.1).
/// </summary>
/// <param name="ToEmail">Recipient address.</param>
/// <param name="ToName">Recipient display name (HTML-encoded at render time).</param>
/// <param name="Subject">Email subject line.</param>
/// <param name="HtmlBody">Responsive HTML body.</param>
/// <param name="PlainTextBody">Optional plain-text fallback body.</param>
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);
