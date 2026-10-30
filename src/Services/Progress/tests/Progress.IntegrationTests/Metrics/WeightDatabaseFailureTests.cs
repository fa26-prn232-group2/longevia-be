using System.Collections.Concurrent;
using Grpc.Core;
using Longevia.Progress.Contracts.V1;
using Microsoft.Extensions.Logging;

namespace Progress.IntegrationTests.Metrics;

[Collection("Weight database fault")]
public sealed class WeightDatabaseFailureTests
{
    [Fact]
    public async Task GetLatestWeight_ReachableServerWithUnreachablePostgres_ReturnsUnavailableAndSafeLog()
    {
        using var logs = new CapturedLogs();
        const string secret = "outage-test-secret";
        var server = new ProgressWeightGrpcFixture
        {
            ExternalConnectionString = $"Host=127.0.0.1;Port=1;Database=unavailable;Username=outage_user;Password={secret};Timeout=1;Command Timeout=1;Pooling=false",
            SkipMigration = true,
            LogProvider = logs
        };
        try
        {
            await server.InitializeAsync();
            // INVALID_ARGUMENT proves the real HTTP/2 endpoint remains reachable before and after the DB fault.
            await AssertInvalidArgumentAsync(server);
            var ex = await Assert.ThrowsAsync<RpcException>(async () => await server.Client.GetLatestWeightAsync(
                new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, deadline: DateTime.UtcNow.AddSeconds(5)));
            Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
            Assert.Equal("Database service is temporarily unavailable.", ex.Status.Detail);
            Assert.DoesNotContain(secret, ex.Status.Detail);
            Assert.DoesNotContain("Host=", ex.Status.Detail);
            await AssertInvalidArgumentAsync(server);
            Assert.Contains(logs.Entries, x => x.Level == LogLevel.Warning && x.Message.Contains("Database unavailable"));
            Assert.All(logs.Entries, x =>
            {
                Assert.Null(x.Exception);
                Assert.DoesNotContain(secret, x.Message);
                Assert.DoesNotContain("Host=", x.Message);
                Assert.DoesNotContain("Password=", x.Message);
            });
        }
        finally { await server.DisposeAsync(); }
    }

    private static async Task AssertInvalidArgumentAsync(ProgressWeightGrpcFixture server)
    {
        var ex = await Assert.ThrowsAsync<RpcException>(async () => await server.Client.GetLatestWeightAsync(
            new GetLatestWeightRequest { UserId = "invalid" }, deadline: DateTime.UtcNow.AddSeconds(5)));
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);
        public void Dispose() { }

        private sealed class Capture(CapturedLogs owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => category == "Progress.Api.Grpc.ProgressGrpcService";
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(level)) owner.Entries.Enqueue((level, formatter(state, exception), exception));
            }
        }
    }
}
