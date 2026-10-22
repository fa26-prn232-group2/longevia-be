// Phase 1 enabling stub — keeps the solution loadable/buildable after T003.
// T018 (Phase 5) replaces this file with the full MassTransit + SMTP +
// OpenTelemetry host configuration.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<StubWorker>();

var app = builder.Build();
app.Run();

internal sealed class StubWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Delay(Timeout.Infinite, stoppingToken);
}
