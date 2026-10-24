using Longevia.Shared.Observability;
using Progress.Api;
using Progress.Api.Extensions;
using Progress.Application;
using Progress.Infrastructure;

if (args is ["--health-check", var address])
{
    Environment.ExitCode = await HealthProbe.RunAsync(address);
    return;
}

var builder = WebApplication.CreateBuilder(args);
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
