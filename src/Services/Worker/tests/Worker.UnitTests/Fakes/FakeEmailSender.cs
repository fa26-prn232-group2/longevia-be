using Longevia.Worker.Adapters.Email;

namespace Worker.UnitTests.Fakes;

/// <summary>Test double recording sent emails — no network, no SMTP.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
