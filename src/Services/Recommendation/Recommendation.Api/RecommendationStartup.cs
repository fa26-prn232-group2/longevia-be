using Google.GenAI;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Recommendation.Api.Deadlines;
using Recommendation.Api.Endpoints;
using Recommendation.Api.Extensions;
using Recommendation.Api.HealthChecks;
using Recommendation.Api.Middlewares;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.LongevityHabits;
using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Meals;
using Recommendation.Application.Features.NutrientGaps;
using Recommendation.Application.Features.Plans;
using Recommendation.Application.Features.Search;
using Recommendation.Application.Features.WorkoutPlans;
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
    /// <param name="migrateOnly">
    /// Chế độ deploy <c>kb-schema</c>: chỉ chạy migration nên không bind, không
    /// validate cấu hình provider (tránh đòi API key ở bước tạo schema).
    /// </param>
    public static void Configure(WebApplicationBuilder builder, bool migrateOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRecommendationTelemetry();
        ConfigureOptions(builder, migrateOnly);

        if (migrateOnly)
        {
            ConfigureKnowledgeStoreOnly(builder);
            return;
        }

        ConfigureKestrel(builder);
        ConfigureServices(builder);
        ConfigureGrpc(builder);
    }

    /// <summary>Chạy migration rồi thoát, không mở cổng phục vụ request.</summary>
    public static async Task MigrateOnlyAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateKnowledgeStoreAsync(cancellationToken).ConfigureAwait(false);
        app.Logger.LogInformation("Knowledge store schema is up to date.");
    }

    // Chế độ migrate-only chỉ cần knowledge store, không cần provider/gRPC.
    private static void ConfigureKnowledgeStoreOnly(WebApplicationBuilder builder) =>
        builder.Services.AddKnowledgeStore();

    /// <summary>Chạy migration và map endpoint trước khi server phục vụ request.</summary>
    public static async Task RunAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateKnowledgeStoreAsync(cancellationToken).ConfigureAwait(false);
        await LogKnowledgeCorpusStateAsync(app, cancellationToken).ConfigureAwait(false);

        app.MapGrpcService<RecommendationGrpcService>();
        app.MapGrpcHealthChecksService();
        app.MapHealthChecks("/health");
    }

    // Kho tri thức rỗng là trạng thái hợp lệ của database nhưng làm mọi câu trả lời
    // abstain; ghi log rõ để người vận hành biết phải chạy kb-init/kb-ensure.
    private static async Task LogKnowledgeCorpusStateAsync(
        WebApplication app,
        CancellationToken cancellationToken)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();

        int chunks;
        try
        {
            chunks = await store.CountActiveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            app.Logger.LogError(exception, "Could not read the knowledge corpus state at startup.");
            return;
        }

        if (chunks == 0)
        {
            app.Logger.LogWarning(
                "Knowledge corpus is empty. Run the kb-init service ('docker compose up -d') "
                + "or 'make kb-ensure'; until then grounded answers will abstain.");
            return;
        }

        app.Logger.LogInformation("Knowledge corpus loaded: {Chunks} chunks.", chunks);
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
    private static void ConfigureOptions(WebApplicationBuilder builder, bool migrateOnly)
    {
        if (!migrateOnly)
        {
            ConfigureProviderOptions(builder);
        }

        ConfigureRagOptions(builder);
        ConfigureRecommendationOptions(builder);
        ConfigureKnowledgeDbOptions(builder);
    }

    private static void ConfigureProviderOptions(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<GoogleAiOptions>()
            .BindEnvSection(builder.Configuration, GoogleAiOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "GOOGLE_AI__API_KEY chưa được cấu hình.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ChatModel), "GOOGLE_AI__CHAT_MODEL chưa được cấu hình.")
            .Validate(o => o.TimeoutMs is > 0 and <= 600_000, "GOOGLE_AI__TIMEOUT_MS không hợp lệ.")
            .ValidateOnStart();
    }

    private static void ConfigureRagOptions(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<RagOptions>()
            .BindEnvSection(builder.Configuration, RagOptions.SectionName)
            .Validate(o => o.TopK is > 0 and <= 20, "RAG__TOP_K phải nằm trong khoảng 1..20.")
            .Validate(o => o.MaxCandidates >= o.TopK, "RAG__MAX_CANDIDATES phải lớn hơn hoặc bằng RAG__TOP_K.")
            .Validate(o => o.ChunkTokens is > 0 and <= 4000, "RAG__CHUNK_TOKENS không hợp lệ.")
            .Validate(o => o.ChunkOverlap >= 0 && o.ChunkOverlap < o.ChunkTokens, "RAG__CHUNK_OVERLAP phải nhỏ hơn RAG__CHUNK_TOKENS.")
            .Validate(o => o.MinCitationSimilarity is >= 0 and <= 1, "RAG__MIN_CITATION_SIMILARITY không hợp lệ.")
            .Validate(o => o.EmbeddingDim is > 0 and <= 3072, "RAG__EMBEDDING_DIM không hợp lệ.")
            .ValidateOnStart();
    }

    private static void ConfigureRecommendationOptions(WebApplicationBuilder builder)
    {
        // Deadline của server phải lớn hơn độ trễ sinh văn bản thực tế của
        // provider (Gemini chat đo được tới ~16s), nếu không response dài sẽ
        // rơi vào DEADLINE_EXCEEDED. Kiểm tra ngay lúc khởi động (fail fast).
        builder.Services.AddOptions<RecommendationOptions>()
            .BindEnvSection(builder.Configuration, RecommendationOptions.SectionName)
            .Validate(
                o => RecommendationOptions.IsValidDeadlineSeconds(o.DeadlineSeconds),
                "RECOMMENDATION__DEADLINE_SECONDS phải nằm trong khoảng 5..120 giây.")
            .Validate(
                o => RecommendationOptions.IsValidDeadlineSeconds(o.PlanDeadlineSeconds),
                "RECOMMENDATION__PLAN_DEADLINE_SECONDS phải nằm trong khoảng 5..120 giây.")
            .ValidateOnStart();
    }

    private static void ConfigureKnowledgeDbOptions(WebApplicationBuilder builder)
    {
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

        // Phân tích vi chất (US5): bảng RDA + calculator thuần nên singleton; use
        // case phụ thuộc IKnowledgeStore (scoped) nên phải scoped.
        builder.Services.AddSingleton<NutrientGapCalculator>();
        builder.Services.AddScoped<AnalyzeNutrientGapsUseCase>();

        // Đánh giá thói quen trường thọ (US6): rubric thuần nên singleton; use case
        // phụ thuộc IKnowledgeStore (scoped) nên phải scoped.
        builder.Services.AddSingleton<LongevityRubricCalculator>();
        builder.Services.AddScoped<EvaluateLongevityHabitsUseCase>();

        // Hỏi tri thức trong sách (US3): guard deterministic + use case phụ thuộc
        // IKnowledgeStore (scoped) nên use case phải scoped.
        builder.Services.AddSingleton<SafetyGuard>();
        builder.Services.AddScoped<AskLongevityQuestionUseCase>();

        // Tra cứu đoạn sách (US4): use case phụ thuộc IKnowledgeStore (scoped) nên
        // cũng phải scoped.
        builder.Services.AddScoped<SearchLongevityKnowledgeUseCase>();

        // Thành phần dùng chung của hai RPC plan (Phase 2): đều thuần, không I/O
        // nên đăng ký singleton; use case plan (scoped) sẽ được thêm ở US1/US2.
        builder.Services.AddSingleton<PlanRequestValidator>();
        builder.Services.AddSingleton<PlanDateRange>();
        builder.Services.AddSingleton<PlanPromptFormatter>();
        builder.Services.AddSingleton<PlanRationaleBuilder>();

        // Kế hoạch ăn uống (US1): filter/splitter/parser/validator/fallback thuần
        // nên singleton; use case phụ thuộc IKnowledgeStore (scoped) nên scoped.
        builder.Services.AddSingleton<PlanAllergenFilter>();
        builder.Services.AddSingleton<MealPlanCandidateFilter>();
        builder.Services.AddSingleton<WeeklyBudgetSplitter>();
        builder.Services.AddSingleton<MealPlanPromptBuilder>();
        builder.Services.AddSingleton<MealPlanSelectionParser>();
        builder.Services.AddSingleton<MealPlanValidator>();
        builder.Services.AddSingleton<MealPlanFallbackSelector>();
        builder.Services.AddSingleton<MealPlanSuggestionBuilder>();
        builder.Services.AddScoped<GenerateMealPlanUseCase>();

        // Kế hoạch tập luyện (US2): catalog/skeleton/prompt/parser/validator thuần
        // nên singleton; use case phụ thuộc IKnowledgeStore (scoped) nên scoped.
        builder.Services.AddSingleton<WorkoutLimitationRules>();
        builder.Services.AddSingleton<WorkoutActivityCatalogBuilder>();
        builder.Services.AddSingleton<WorkoutPlanSkeletonBuilder>();
        builder.Services.AddSingleton<WorkoutPlanPromptBuilder>();
        builder.Services.AddSingleton<WorkoutPlanSelectionParser>();
        builder.Services.AddSingleton<WorkoutPlanValidator>();
        builder.Services.AddScoped<GenerateWorkoutPlanUseCase>();

        builder.Services.AddHealthChecks()
            .AddCheck("recommendation", () => HealthCheckResult.Healthy("Recommendation service is running."))
            .AddCheck<KnowledgeCorpusHealthCheck>("kb-corpus");
    }

    private static void ConfigureGrpc(WebApplicationBuilder builder)
    {
        // Policy thuần đọc IOptions nên đăng ký singleton; interceptor scoped để
        // giữ nguyên vòng đời hiện có.
        builder.Services.AddSingleton<RpcDeadlinePolicy>();
        builder.Services.AddScoped<GrpcExceptionInterceptor>();
        builder.Services.AddScoped(sp => new ServerDeadlineInterceptor(
            sp.GetRequiredService<ILogger<ServerDeadlineInterceptor>>(),
            sp.GetRequiredService<RpcDeadlinePolicy>()));

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
