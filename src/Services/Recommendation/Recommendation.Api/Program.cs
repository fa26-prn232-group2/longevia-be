using Google.GenAI;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Recommendation.Api.Extensions;
using Recommendation.Api.Middlewares;
using Recommendation.Infrastructure.DependencyInjection;
using Recommendation.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Cổng lắng nghe: đọc từ biến môi trường, không hard-code.
//   RECOMMENDATION__GRPC_PORT -> section RECOMMENDATION, key GRPC_PORT
//   RECOMMENDATION__HTTP_PORT -> section RECOMMENDATION, key HTTP_PORT
// Mặc định giữ nguyên hành vi cũ: 50051 (gRPC h2c) và 8080 (HTTP/1.1 /health).
// ---------------------------------------------------------------------------
const string serviceConfigSection = "RECOMMENDATION";
const int defaultGrpcPort = 50051;
const int defaultHttpPort = 8080;

builder.Services.AddRecommendationTelemetry();

var grpcPort = builder.Configuration.GetEnvInt(serviceConfigSection, "GrpcPort", defaultGrpcPort);
if (grpcPort is <= 0 or > 65535)
{
    grpcPort = defaultGrpcPort;
}

var httpPort = builder.Configuration.GetEnvInt(serviceConfigSection, "HttpPort", defaultHttpPort);
if (httpPort is <= 0 or > 65535)
{
    httpPort = defaultHttpPort;
}

// ---------------------------------------------------------------------------
// Kestrel: hai endpoint riêng.
//   - 50051: gRPC chạy trên HTTP/2 không TLS (h2c). Kestrel chỉ bật HTTP/2 cho
//     endpoint không TLS khi protocol là Http2 (prior knowledge).
//   - 8080: HTTP/1.1 cho /health, để probe đơn giản (curl, healthcheck) gọi được.
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(grpcPort, listen => listen.Protocols = HttpProtocols.Http2);
    options.ListenAnyIP(httpPort, listen => listen.Protocols = HttpProtocols.Http1);
});

// ---------------------------------------------------------------------------
// Cấu hình: bind và kiểm tra ngay lúc khởi động (fail fast).
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// Provider Google AI: SDK không có DI extension nên đăng ký thủ công.
// Client là IDisposable nên container sẽ dispose khi host dừng.
// ---------------------------------------------------------------------------

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<GoogleAiOptions>>().Value;
    return new Client(apiKey: options.ApiKey);
});

// ---------------------------------------------------------------------------
// Knowledge store + adapter RAG (Phase 2): đăng ký tường minh theo interface,
// không dùng service locator.
// ---------------------------------------------------------------------------

builder.Services.AddKnowledgeStore();

// ---------------------------------------------------------------------------
// gRPC server (service nội bộ, không đi qua Nginx - Constitution §I).
// ---------------------------------------------------------------------------

builder.Services.AddScoped<GrpcExceptionInterceptor>();
builder.Services.AddScoped(sp => new ServerDeadlineInterceptor(
    sp.GetRequiredService<ILogger<ServerDeadlineInterceptor>>(),
    ServerDeadlineInterceptor.DefaultDeadline));

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<GrpcExceptionInterceptor>();
    options.Interceptors.Add<ServerDeadlineInterceptor>();
});

builder.Services.AddHealthChecks()
    .AddCheck("recommendation", () => HealthCheckResult.Healthy("Recommendation service is running."));

builder.Services.AddGrpcHealthChecks(options =>
{
    // Tên service rỗng là trạng thái tổng thể trong gRPC health protocol; gắn nó
    // với mọi health check đã đăng ký.
    options.Services.Map(string.Empty, static _ => true);
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Knowledge store: migration của recommendation_db phải chạy trước khi phục vụ
// request (T024/T029). Chỉ migration của service này, không đụng DB khác.
// ---------------------------------------------------------------------------

await app.Services.MigrateKnowledgeStoreAsync();

app.MapGrpcHealthChecksService();
app.MapHealthChecks("/health");

app.Run();
