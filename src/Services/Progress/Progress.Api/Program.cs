using Longevia.Shared.Observability;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Progress.Api;
using Progress.Api.Extensions;
using Progress.Application;
using Progress.Infrastructure;
using Progress.Infrastructure.Reconciliation;

if (args is ["--reconcile-meal-snapshot", var snapshot])
{
    Environment.ExitCode = await MealSnapshotReconciler.RunAsync(snapshot,
        Environment.GetEnvironmentVariable("ConnectionStrings__Progress"), Console.Out);
    return;
}

if (args is ["--health-check", var address])
{
    Environment.ExitCode = await HealthProbe.RunAsync(address);
    return;
}

var builder = WebApplication.CreateBuilder(args);

var httpPort = builder.Configuration.GetValue("PROGRESS:HTTP_PORT", 8080);
var grpcPort = builder.Configuration.GetValue("PROGRESS:GRPC_PORT", 50051);
builder.WebHost.ConfigureKestrel(options =>
{
    if (httpPort > 0)
    {
        options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1);
    }
    if (grpcPort > 0)
    {
        options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
    }
});

builder.Services.AddProgressApplication();
builder.Services.AddProgressInfrastructure(builder.Configuration);
builder.Services.AddProgressHost(builder.Configuration);
builder.Services.AddObservability("ProgressService");

var app = builder.Build();
app.UseProgressHost();
app.MapPrometheusScrapingEndpoint();
await app.InitializeProgressDatabaseAsync(app.Lifetime.ApplicationStopping);
app.Run();

public partial class Program;
