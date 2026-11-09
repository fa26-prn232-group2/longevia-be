using Grpc.AspNetCore.Server;
using Longevia.Shared.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Recommendation.Api;

namespace Recommendation.IntegrationTests;

/// <summary>
/// Regression: the service token interceptor used to be registered inside the
/// <c>AddGrpc</c> options delegate, which runs when <c>GrpcServiceOptions</c> is
/// resolved — after the service collection is read-only. Docker sets
/// <c>SERVICE__TOKEN</c>, so the host crashed on startup.
/// </summary>
public sealed class ServiceTokenRegistrationTests
{
    [Fact]
    public void Configure_ServiceTokenConfigured_RegistersServerInterceptor()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RECOMMENDATION:GRPC_PORT"] = "50051",
            ["RECOMMENDATION:HTTP_PORT"] = "8080",
            ["GOOGLE_AI:API_KEY"] = "integration-test-key",
            ["GOOGLE_AI:CHAT_MODEL"] = "gemini-2.0-flash",
            ["KNOWLEDGE_DB:CORPUS_KEY"] = "test-corpus",
            ["KNOWLEDGE_DB:SCHEMA"] = "kb",
            ["ConnectionStrings:Recommendation"] =
                "Host=127.0.0.1;Port=1;Database=test;Username=test;Password=test",
            ["SERVICE:TOKEN"] = "recommendation-integration-service-token",
        });

        RecommendationStartup.Configure(builder);
        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;

        Assert.Contains(options.Interceptors, registration => registration.Type == typeof(ServiceTokenServerInterceptor));
    }
}
