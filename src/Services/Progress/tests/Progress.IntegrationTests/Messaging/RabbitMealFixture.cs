using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progress.Infrastructure.Messaging;
using Progress.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Progress.IntegrationTests.Messaging;

public sealed class RabbitMealFixture : IAsyncLifetime
{
    private readonly string _password = Guid.NewGuid().ToString("N");
    public PostgreSqlContainer Database { get; } = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("progress_db").WithUsername("progress_user").WithPassword(Guid.NewGuid().ToString("N")).Build();
    public IContainer Rabbit { get; private set; } = null!;
    public ServiceProvider Services { get; private set; } = null!;
    public HttpClient Management { get; private set; } = null!;
    public IBusControl Bus => Services.GetRequiredService<IBusControl>();
    public MealConsumeObserver Observer { get; } = new();
    public ProgressDbContext Db() => new(new DbContextOptionsBuilder<ProgressDbContext>().UseNpgsql(Database.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        Rabbit = new ContainerBuilder("rabbitmq:4-management-alpine")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "progress_test")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", _password)
            .WithPortBinding(5672, true).WithPortBinding(15672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(15672)
                .ForPath("/api/overview").WithBasicAuthentication("progress_test", _password))).Build();
        await Database.StartAsync();
        await Rabbit.StartAsync();
        Management = new HttpClient { BaseAddress = new Uri($"http://{Rabbit.Hostname}:{Rabbit.GetMappedPublicPort(15672)}/api/") };
        Management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"progress_test:{_password}")));
        await using (var db = Db()) await db.Database.MigrateAsync();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Host"] = Rabbit.Hostname,
            ["RabbitMQ:Port"] = Rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMQ:User"] = "progress_test",
            ["RabbitMQ:Password"] = _password
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FoundationTimeProvider());
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Database.GetConnectionString()) { Timeout = 2, CommandTimeout = 2 };
        services.AddDbContext<ProgressDbContext>(o => o.UseNpgsql(connection.ConnectionString));
        services.AddProgressMessaging(settings);
        Services = services.BuildServiceProvider();
        Bus.ConnectConsumeObserver(Observer);
        await Bus.StartAsync();
    }

    public async Task<JsonElement> GetAsync(string path)
    {
        using var response = await Management.GetAsync(path);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    public System.Diagnostics.Process StartConsumerProcess()
    {
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var pair in new Dictionary<string, string>
        {
            ["ConnectionStrings__Progress"] = Database.GetConnectionString(),
            ["Messaging__UseInMemory"] = "false",
            ["RabbitMQ__Host"] = Rabbit.Hostname,
            ["RabbitMQ__Port"] = Rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMQ__User"] = "progress_test",
            ["RabbitMQ__Password"] = _password,
            ["Jwt__Issuer"] = "test",
            ["Jwt__Audience"] = "test",
            ["Jwt__KeyId"] = "test",
            ["Jwt__SigningPublicKey"] = ProgressFixture.PublicKey(key),
            ["Jwt__SigningPrivateKey"] = "",
            ["Jwt__PreviousKeyId"] = "",
            ["Jwt__PreviousPublicKey"] = "",
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:0"
        }) start.Environment[pair.Key] = pair.Value;
        var process = System.Diagnostics.Process.Start(start)!;
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        return process;
    }

    public static async Task EventuallyAsync(Func<Task<bool>> condition, int seconds = 45)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail($"Condition not observed within {seconds}s.");
    }

    public async Task DisposeAsync()
    {
        if (Services is not null) { await Bus.StopAsync(); await Services.DisposeAsync(); }
        Management?.Dispose();
        if (Rabbit is not null) await Rabbit.DisposeAsync();
        await Database.DisposeAsync();
    }
}
