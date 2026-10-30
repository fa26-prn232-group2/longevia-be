using System.Net;
using System.Net.Sockets;
using Grpc.Net.Client;
using Longevia.Progress.Contracts.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Progress.Api.Grpc;
using Progress.Application;
using Progress.Infrastructure;
using Progress.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Progress.IntegrationTests.Metrics;

public sealed class ProgressWeightGrpcFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    private WebApplication? _app;
    private GrpcChannel? _channel;
    public string? ExternalConnectionString { get; init; }
    public bool SkipMigration { get; init; }
    public ILoggerProvider? LogProvider { get; init; }

    public int GrpcPort { get; private set; }
    public ProgressService.ProgressServiceClient Client { get; private set; } = null!;
    public IServiceProvider Services => _app?.Services ?? throw new InvalidOperationException("Fixture not initialized.");
    public string ConnectionString => ExternalConnectionString ?? _postgres?.GetConnectionString()
        ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        if (ExternalConnectionString is null)
        {
            _postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("progress_grpc_test").WithUsername("test_user").WithPassword("test_password").Build();
            await _postgres.StartAsync();
        }
        GrpcPort = FreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, GrpcPort, listen => listen.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddProgressApplication();
        if (LogProvider is not null) builder.Logging.AddProvider(LogProvider);
        builder.Services.AddGrpc();
        builder.Services.AddDbContext<ProgressDbContext>(options =>
            options.UseNpgsql(ConnectionString));
        builder.Services.AddScoped<Progress.Application.Abstractions.IBodyMetricRepository, Progress.Infrastructure.Repositories.BodyMetricRepository>();

        _app = builder.Build();

        if (!SkipMigration)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
            await db.Database.MigrateAsync();
        }

        _app.MapGrpcService<ProgressGrpcService>();
        await _app.StartAsync();

        _channel = GrpcChannel.ForAddress($"http://127.0.0.1:{GrpcPort}", new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
        });

        Client = new ProgressService.ProgressServiceClient(_channel);
    }

    public async Task DisposeAsync()
    {
        _channel?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
