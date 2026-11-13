using Longevia.Worker.Adapters.Email;
using Microsoft.Extensions.Configuration;

namespace Worker.UnitTests;

public sealed class SmtpOptionsTests
{
    [Fact]
    public void Bind_MapsUnderscoreEnvironmentKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SMTP:HOST"] = "smtp-relay.brevo.com",
                ["SMTP:PORT"] = "587",
                ["SMTP:USE_SSL"] = "true",
                ["SMTP:USER"] = "user",
                ["SMTP:PASSWORD"] = "secret",
                ["SMTP:SENDER_EMAIL"] = "no-reply@mail.hycat.online",
            })
            .Build();

        var options = configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>();

        Assert.NotNull(options);
        Assert.True(options.UseSsl);
        Assert.Equal("no-reply@mail.hycat.online", options.SenderEmail);
    }
}
