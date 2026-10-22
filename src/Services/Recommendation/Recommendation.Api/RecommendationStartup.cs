using Google.GenAI;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Recommendation.Api.Endpoints;
using Recommendation.Api.Extensions;
using Recommendation.Api.Middlewares;
using Recommendation.Application.Features.Meals;
using Recommendation.Infrastructure.DependencyInjection;
using Recommendation.Infrastructure.Options;

namespace Recommendation.Api;

/// <summary>
/// Composition root của Recommendation: tách khỏi <c>Program.cs</c> để integration
/// test dựng đúng cây DI thật trong tiến trình (thay provider bằng test double)
/// thay vì dựng lại cấu hình bằng tay.
/// </summary>
public static class RecommendationStartup
{
    private const string ServiceConfigSection = "RECOMMENDATION";
    private const int DefaultGrpcPort = 50051;
    private const int DefaultHttpPort = 8080;

    /// <summary>Đăng ký toàn bộ cấu hình, dịch vụ và gRPC server.</summary>
    public static void Configure(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRecommendationTelemetry();
        ConfigureKestrel(builder);
        ConfigureOptions(builder);
        ConfigureServices(builder);
        ConfigureGrpc(builder);
    }

    /// <summary>Chạy migration và map endpoint trước khi server phục vụ request.</summary>
    public static async Task RunAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateKnowledgeStoreAsync(cancellationToken).ConfigureAwait(false);

        app.MapGrpcService<RecommendationGrpcService>();
        app.MapGrpcHealthChecksService();
        app.MapHealthChecks("/health");
    }

    // Cổng lắng nghe đọc từ biến môi trường, không hard-code:
    //   RECOMMENDATION__GRPC_PORT -> section RECOMMENDATION, key GRPC_PORT
    //   RECOMMENDATION__HTTP_PORT -> section RECOMMENDATION, key HTTP_PORT
    private static void ConfigureKestrel(WebApplicationBuilder builder)
    {
        var grpcPort = ResolvePort(builder.Configuration, "GrpcPort", DefaultGrpcPort);
        var httpPort = ResolvePort(builder.Configuration, "HttpPort", DefaultHttpPort);

        builder.WebHost.ConfigureKestrel(options =>
        {
            // 50051: gRPC trên HTTP/2 không TLS (h2c) — bắt buộc prior knowledge.
            options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
            // 8080: HTTP/1.1 cho /health để probe đơn giản.
            options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1);
        });
    }

    private static int ResolvePort(IConfiguration configuration, string propertyName, int defaultPort)
    {
        var port = configuration.GetEnvInt(ServiceConfigSection, propertyName, defaultPort);
        return port is > 0 and <= 65535 ? port : defaultPort;
    }

    // Bind và kiểm tra ngay lúc khởi động (fail fast).
    private static void ConfigureOptions(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<GoogleAiOptions>()
            .BindEnvSection(builder.Configuration, GoogleAiOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "GOOGLE_AI__API_KEY chưa được cấu hình.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ChatModel), "GOOGLE_AI__CHAT_MODEL chưa được cấu hình.")
            .Validate(o => o.TimeoutMs is > 0 and <= 600_000, "GOOGLE_AI__TIMEOUT_MS không hợp lệ.")
            .ValidateOnStart();

        builder.Services.AddOptions<RagOptions>()
            .BindEnvSection(builder.Configuration, RagOptions.SectionName)
            .Validate(o => o.TopK is > 0 and <= 20, "RAG__TOP_K phải nằm trong khoảng 1..20.")
            .Validate(o => o.MaxCandidates >= o.TopK, "RAG__MAX_CANDIDATES phải lớn hơn hoặc bằng RAG__TOP_K.")
            .Validate(o => o.ChunkTokens is > 0 and <= 4000, "RAG__CHUNK_TOKENS không hợp lệ.")
            .Validate(o => o.ChunkOverlap >= 0 && o.ChunkOverlap < o.ChunkTokens, "RAG__CHUNK_OVERLAP phải nhỏ hơn RAG__CHUNK_TOKENS.")
            .Validate(o => o.MinCitationSimilarity is >= 0 and <= 1, "RAG__MIN_CITATION_SIMILARITY không hợp lệ.")
            .Validate(o => o.EmbeddingDim is > 0 and <= 3072, "RAG__EMBEDDING_DIM không hợp lệ.")
            .ValidateOnStart();

        builder.Services.AddOptions<KnowledgeDbOptions>()
            .BindEnvSection(builder.Configuration, KnowledgeDbOptions.SectionName)
            .Configure(o =>
            {
                var connectionString = builder.Configuration.GetConnectionString("Recommendation");
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    o.ConnectionString = connectionString;
                }
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "ConnectionStrings__Recommendation chưa được cấu hình.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.CorpusKey), "KNOWLEDGE_DB__CORPUS_KEY chưa được cấu hình.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Schema), "KNOWLEDGE_DB__SCHEMA chưa được cấu hình.")
            .ValidateOnStart();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Provider Google AI: SDK không có DI extension nên đăng ký thủ công.
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GoogleAiOptions>>().Value;
            return new Client(apiKey: options.ApiKey);
        });

        builder.Services.AddKnowledgeStore();

        // Bộ lọc và use case gợi ý bữa ăn (US1). MealSelector phụ thuộc
        // IKnowledgeStore (scoped) nên phải scoped, không singleton.
        builder.Services.AddSingleton<AllergenGuard>();
        builder.Services.AddSingleton<DietaryPreferenceFilter>();
        builder.Services.AddSingleton<CalorieCapRule>();
        builder.Services.AddSingleton<MealSelectionParser>();
        builder.Services.AddScoped<MealSelector>();
        builder.Services.AddScoped<GetMealRecommendationsUseCase>();

        builder.Services.AddHealthChecks()
            .AddCheck("recommendation", () => HealthCheckResult.Healthy("Recommendation service is running."));
    }

    private static void ConfigureGrpc(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<GrpcExceptionInterceptor>();
        builder.Services.AddScoped(sp => new ServerDeadlineInterceptor(
            sp.GetRequiredService<ILogger<ServerDeadlineInterceptor>>(),
            ServerDeadlineInterceptor.DefaultDeadline));

        // gRPC service được resolve qua DI để tôn trọng constructor injection.
        builder.Services.AddScoped<RecommendationGrpcService>();

        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<GrpcExceptionInterceptor>();
            options.Interceptors.Add<ServerDeadlineInterceptor>();
        });

        builder.Services.AddGrpcHealthChecks(options =>
        {
            // Tên service rỗng là trạng thái tổng thể trong gRPC health protocol;
            // gắn nó với mọi health check đã đăng ký.
            options.Services.Map(string.Empty, static _ => true);
        });
    }
}
