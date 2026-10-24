using System.Security.Cryptography;
using System.Text;
using Testcontainers.PostgreSql;

namespace Progress.IntegrationTests;

public sealed class ProgressFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("progress_db").WithUsername("progress_user")
        .WithPassword(Guid.NewGuid().ToString("N")).Build();

    public ECDsa CurrentKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public ECDsa PreviousKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public ProgressApiFactory Factory { get; private set; } = null!;
    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new ProgressApiFactory(this);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
        CurrentKey.Dispose();
        PreviousKey.Dispose();
    }

    public static string PublicKey(ECDsa key) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(key.ExportSubjectPublicKeyInfoPem()));
}
