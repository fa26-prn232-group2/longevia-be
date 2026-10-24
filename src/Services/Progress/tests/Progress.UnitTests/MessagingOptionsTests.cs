using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progress.Infrastructure.Messaging;

namespace Progress.UnitTests;

public sealed class MessagingOptionsTests
{
    [Theory]
    [InlineData("RabbitMQ:Host", "")]
    [InlineData("RabbitMQ:User", "")]
    [InlineData("RabbitMQ:Password", "")]
    [InlineData("RabbitMQ:Port", "invalid")]
    [InlineData("RabbitMQ:Port", "0")]
    [InlineData("RabbitMQ:Port", "65536")]
    public void RabbitSettings_MissingOrInvalid_FailWithoutCredentialDefaults(string key, string value)
    {
        var settings = new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = "broker",
            ["RabbitMQ:User"] = "user",
            ["RabbitMQ:Password"] = "secret-not-in-errors",
            ["RabbitMQ:Port"] = "5672"
        };
        settings[key] = value;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddProgressMessaging(config));
        Assert.DoesNotContain("secret-not-in-errors", exception.Message);
    }
}
