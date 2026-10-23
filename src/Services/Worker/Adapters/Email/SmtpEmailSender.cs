using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Longevia.Worker.Adapters.Email;

/// <summary>
/// MailKit-based <see cref="IEmailSender"/> supporting both profiles:
/// no-auth plain SMTP (Mailpit) and StartTLS + credentials (Brevo relay).
/// The profile is driven 100% by <see cref="SmtpOptions"/> (env
/// <c>SMTP__*</c>); this class contains no host, port, or credential literals.
/// Secret handling: NEVER logs tokens, passwords, or message bodies — only
/// infra-level facts (host:port, outcome) reach the logs.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        // Values are validated on start (ValidateDataAnnotations + ValidateOnStart).
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var client = new SmtpClient();

        _logger.LogDebug("Connecting to SMTP relay {Host}:{Port}", _options.Host, _options.Port);

        var socketOptions = _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            await client.AuthenticateAsync(_options.User, _options.Password, cancellationToken);
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Longevia", _options.SenderEmail));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody,
        }.ToMessageBody();

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Email dispatched via SMTP relay");
    }
}
