using System.Security.Cryptography;
using System.Text;
using Longevia.Shared.Auth;
using Microsoft.Extensions.DependencyInjection;
using Progress.Api.Auth;
using Progress.Application;
using Progress.Infrastructure.Persistence;

namespace Progress.UnitTests;

public sealed class FoundationOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Host=localhost;Database=identity_db;Username=progress_user")]
    [InlineData("Host=localhost;Database=progress_db;Username=postgres")]
    [InlineData("Database=progress_db;Username=progress_user")]
    [InlineData("not a connection string")]
    public void DatabaseOptions_InvalidConnection_IsRejected(string? connection)
    {
        Assert.False(new ProgressDatabaseOptions { Progress = connection }.IsValid());
    }

    [Fact]
    public void DatabaseOptions_ProgressDatabaseAndUser_AreAccepted()
    {
        Assert.True(new ProgressDatabaseOptions
        {
            Progress = "Host=localhost;Database=progress_db;Username=progress_user",
        }.IsValid());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("raw-pem")]
    [InlineData("private-pem")]
    [InlineData("wrong-curve")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("kid")]
    [InlineData("private-setting")]
    [InlineData("previous-id-only")]
    [InlineData("previous-key-only")]
    [InlineData("previous-same-id")]
    [InlineData("previous-invalid")]
    public void JwtOptions_InvalidConfiguration_IsRejectedWithoutLeakingKey(string scenario)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var options = ValidOptions(key);
        switch (scenario)
        {
            case "missing": options.SigningPublicKey = null; break;
            case "raw-pem": options.SigningPublicKey = key.ExportSubjectPublicKeyInfoPem(); break;
            case "private-pem": options.SigningPublicKey = Encode(key.ExportPkcs8PrivateKeyPem()); break;
            case "wrong-curve": options.SigningPublicKey = Encode(wrongCurve.ExportSubjectPublicKeyInfoPem()); break;
            case "issuer": options.Issuer = ""; break;
            case "audience": options.Audience = ""; break;
            case "kid": options.KeyId = ""; break;
            case "private-setting": options.SigningPrivateKey = "test-private-value"; break;
            case "previous-id-only": options.PreviousKeyId = "old"; break;
            case "previous-key-only": options.PreviousPublicKey = options.SigningPublicKey; break;
            case "previous-same-id":
                options.PreviousPublicKey = options.SigningPublicKey; options.PreviousKeyId = options.KeyId; break;
            case "previous-invalid": options.PreviousPublicKey = "invalid"; options.PreviousKeyId = "old"; break;
        }
        var result = new ProgressJwtOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.DoesNotContain("test-private-value", result.FailureMessage);
        Assert.DoesNotContain(options.SigningPublicKey ?? "never-present", result.FailureMessage);
    }

    [Fact]
    public void JwtOptions_PublicKeysWithDistinctIds_AreAccepted()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = ValidOptions(key);
        options.PreviousKeyId = "old";
        options.PreviousPublicKey = options.SigningPublicKey;
        Assert.True(new ProgressJwtOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void ApplicationRegistration_TestClock_IsPreserved()
    {
        var services = new ServiceCollection();
        var clock = new FixedTimeProvider();
        services.AddSingleton<TimeProvider>(clock);
        services.AddProgressApplication();
        using var provider = services.BuildServiceProvider();
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), clock.GetUtcNow());
    }

    private static JwtOptions ValidOptions(ECDsa key) => new()
    {
        Issuer = "progress-tests",
        Audience = "progress-test-clients",
        KeyId = "current",
        SigningPublicKey = Encode(key.ExportSubjectPublicKeyInfoPem()),
    };

    private static string Encode(string pem) => Convert.ToBase64String(Encoding.UTF8.GetBytes(pem));

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
    }
}
