using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Progress.IntegrationTests;

public sealed class ProgressApiFactory(ProgressFixture fixture,
    IReadOnlyDictionary<string, string?>? overrides = null, TimeProvider? time = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Progress"] = fixture.ConnectionString,
            ["Jwt:Issuer"] = "progress-tests",
            ["Jwt:Audience"] = "progress-test-clients",
            ["Jwt:KeyId"] = "current",
            ["Jwt:SigningPublicKey"] = ProgressFixture.PublicKey(fixture.CurrentKey),
            ["Jwt:PreviousKeyId"] = "previous",
            ["Jwt:PreviousPublicKey"] = ProgressFixture.PublicKey(fixture.PreviousKey),
            ["Jwt:SigningPrivateKey"] = "",
            ["Messaging:UseInMemory"] = "true",
        };
        foreach (var setting in overrides ?? new Dictionary<string, string?>())
        {
            settings[setting.Key] = setting.Value;
        }
        foreach (var setting in settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, FoundationProbeStartupFilter>();
            services.AddScoped<IValidator<ProbeRequest>, ProbeRequestValidator>();
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time ?? new FoundationTimeProvider()));
        });
    }
}
