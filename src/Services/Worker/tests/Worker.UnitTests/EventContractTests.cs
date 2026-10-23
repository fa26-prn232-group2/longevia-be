using System.Text.Json;
using Longevia.Shared.Events;

namespace Worker.UnitTests;

/// <summary>
/// Forward-compatibility: consumers tolerate unknown fields so additive-optional
/// contract changes stay on version 1 (contracts/events/v1 policy, T017).
/// </summary>
public sealed class EventContractTests
{
    [Fact]
    public void UserRegisteredEvent_IgnoresUnknownFields()
    {
        const string json = """
            {
              "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
              "email": "new@example.com",
              "fullName": "New User",
              "verificationToken": "tok-1",
              "occurredOn": "2026-10-22T08:00:00Z",
              "futureOptionalField": "added by a newer producer"
            }
            """;

        var message = JsonSerializer.Deserialize<UserRegisteredEvent>(json, EventJsonOptions.Create());

        Assert.NotNull(message);
        Assert.Equal("new@example.com", message.Email);
        Assert.Equal("tok-1", message.VerificationToken);
    }

    [Fact]
    public void PasswordResetRequestedEvent_IgnoresUnknownFields()
    {
        const string json = """
            {
              "email": "forgot@example.com",
              "resetToken": "tok-2",
              "expiresAt": "2026-10-22T08:15:00Z",
              "occurredOn": "2026-10-22T08:00:00Z",
              "futureOptionalField": 42
            }
            """;

        var message = JsonSerializer.Deserialize<PasswordResetRequestedEvent>(json, EventJsonOptions.Create());

        Assert.NotNull(message);
        Assert.Equal("forgot@example.com", message.Email);
        Assert.Equal("tok-2", message.ResetToken);
    }
}
