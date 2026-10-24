using Longevia.Shared.Events;
using Longevia.Worker.Consumers;
using Microsoft.Extensions.Logging;
using Worker.UnitTests.Fakes;

namespace Worker.UnitTests;

/// <summary>
/// Consumer behavior without a broker or SMTP (T017): idempotent redelivery,
/// outcome-only logging (redaction), and per-event claim keys.
/// </summary>
public sealed class ConsumerTests
{
    private static readonly DateTimeOffset OccurredOn =
        new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UserRegistered_DuplicateDelivery_SendsOnce()
    {
        var sender = new FakeEmailSender();
        var logs = new CaptureLoggerProvider();
        var consumer = CreateUserRegisteredConsumer(sender, logs);
        var message = new UserRegisteredEvent(Guid.NewGuid(), "a@example.com", "A", "tok", OccurredOn);

        await consumer.HandleAsync(message);
        await consumer.HandleAsync(message);

        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task PasswordReset_DuplicateDelivery_SendsOnce()
    {
        var sender = new FakeEmailSender();
        var logs = new CaptureLoggerProvider();
        var consumer = CreatePasswordResetConsumer(sender, logs);
        var message = new PasswordResetRequestedEvent("a@example.com", "tok", OccurredOn.AddMinutes(15), OccurredOn);

        await consumer.HandleAsync(message);
        await consumer.HandleAsync(message);

        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task PasswordReset_SendFails_RetryCanClaimAndSend()
    {
        var sender = new FakeEmailSender { FailNextSend = true };
        var consumer = CreatePasswordResetConsumer(sender, new CaptureLoggerProvider());
        var message = new PasswordResetRequestedEvent("a@example.com", "retry-token", OccurredOn.AddMinutes(15), OccurredOn);

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.HandleAsync(message));
        await consumer.HandleAsync(message);

        Assert.Single(sender.Sent);
        Assert.Contains("retry-token", sender.Sent[0].HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserRegistered_SendFails_RetryCanClaimAndSend()
    {
        var sender = new FakeEmailSender { FailNextSend = true };
        var consumer = CreateUserRegisteredConsumer(sender, new CaptureLoggerProvider());
        var message = new UserRegisteredEvent(Guid.NewGuid(), "a@example.com", "A", "retry-token", OccurredOn);

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.HandleAsync(message));
        await consumer.HandleAsync(message);

        Assert.Single(sender.Sent);
        Assert.Contains("retry-token", sender.Sent[0].HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasswordReset_DistinctTokensForSameEmailAndTimestamp_SendSeparately()
    {
        var sender = new FakeEmailSender();
        var consumer = CreatePasswordResetConsumer(sender, new CaptureLoggerProvider());

        await consumer.HandleAsync(new PasswordResetRequestedEvent("a@example.com", "token-1", OccurredOn.AddMinutes(15), OccurredOn));
        await consumer.HandleAsync(new PasswordResetRequestedEvent("a@example.com", "token-2", OccurredOn.AddMinutes(15), OccurredOn));

        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    public async Task UserRegistered_DistinctEvents_SendSeparately()
    {
        var sender = new FakeEmailSender();
        var consumer = CreateUserRegisteredConsumer(sender, new CaptureLoggerProvider());

        await consumer.HandleAsync(new UserRegisteredEvent(Guid.NewGuid(), "a@example.com", "A", "t1", OccurredOn));
        await consumer.HandleAsync(new UserRegisteredEvent(Guid.NewGuid(), "b@example.com", "B", "t2", OccurredOn));

        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    public async Task UserRegistered_LogsNeverContainTokenOrBody()
    {
        const string token = "SECRET-VERIFY-TOKEN-9f8e7d";
        var sender = new FakeEmailSender();
        var logs = new CaptureLoggerProvider();
        var consumer = CreateUserRegisteredConsumer(sender, logs);
        var message = new UserRegisteredEvent(Guid.NewGuid(), "a@example.com", "A", token, OccurredOn);

        await consumer.HandleAsync(message);

        // The email itself carries the token (it must), but the logs must not.
        Assert.Contains(sender.Sent, e => e.HtmlBody.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PasswordReset_LogsNeverContainTokenOrBody()
    {
        const string token = "SECRET-RESET-TOKEN-1a2b3c";
        var sender = new FakeEmailSender();
        var logs = new CaptureLoggerProvider();
        var consumer = CreatePasswordResetConsumer(sender, logs);
        var message = new PasswordResetRequestedEvent("a@example.com", token, OccurredOn.AddMinutes(15), OccurredOn);

        await consumer.HandleAsync(message);

        Assert.Contains(sender.Sent, e => e.HtmlBody.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain(sender.Sent, e => e.HtmlBody.Contains("/auth/reset-password?token=", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(token, StringComparison.Ordinal));
    }

    private static UserRegisteredConsumer CreateUserRegisteredConsumer(
        FakeEmailSender sender, CaptureLoggerProvider logs)
    {
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        return new UserRegisteredConsumer(sender, new InMemoryIdempotencyStore(), factory.CreateLogger<UserRegisteredConsumer>());
    }

    private static PasswordResetRequestedConsumer CreatePasswordResetConsumer(
        FakeEmailSender sender, CaptureLoggerProvider logs)
    {
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        return new PasswordResetRequestedConsumer(sender, new InMemoryIdempotencyStore(), factory.CreateLogger<PasswordResetRequestedConsumer>());
    }
}
