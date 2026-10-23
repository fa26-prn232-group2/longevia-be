using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Longevia.Shared.Observability;

/// <summary>
/// Shared OpenTelemetry wiring for Longevia microservices.
/// Every service enables observability with two lines in <c>Program.cs</c>:
/// <c>builder.Services.AddObservability("ServiceName")</c> and
/// <c>app.MapPrometheusScrapingEndpoint()</c>.
/// Prometheus and Grafana ports are configured in compose files and environment
/// files, never in code.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing (ASP.NET Core, HttpClient) and metrics
    /// (ASP.NET Core, HttpClient, .NET runtime) with a Prometheus exporter.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="serviceName">Logical service name reported in resource attributes.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddObservability(this IServiceCollection services, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        return services;
    }

    /// <summary>
    /// Maps the Prometheus scraping endpoint (<c>/metrics</c>).
    /// The receiver is <c>WebApplication</c> (not the exporter's
    /// <c>IEndpointRouteBuilder</c>) on purpose: with both namespaces in scope
    /// the compiler picks this more specific overload, so
    /// <c>app.MapPrometheusScrapingEndpoint()</c> never hits CS0121 ambiguity.
    /// </summary>
    /// <param name="app">The web application to map <c>/metrics</c> on.</param>
    /// <returns>The same <paramref name="app"/> instance for chaining.</returns>
    public static WebApplication MapPrometheusScrapingEndpoint(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Fully qualified: this class defines a same-named extension, so an
        // unqualified call would recurse into itself instead of reaching the exporter.
        global::Microsoft.AspNetCore.Builder.PrometheusExporterEndpointRouteBuilderExtensions.MapPrometheusScrapingEndpoint(app, "/metrics");

        return app;
    }
}
