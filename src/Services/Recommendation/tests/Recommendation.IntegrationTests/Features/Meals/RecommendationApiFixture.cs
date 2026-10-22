using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Grpc.Net.Client;
using Longevia.Recommendation.Contracts.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Recommendation.Api;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;
using Testcontainers.PostgreSql;

namespace Recommendation.IntegrationTests.Features.Meals;

/// <summary>
/// Host thật của <c>Recommendation.Api</c> (Kestrel + DI + migration) trên
/// PostgreSQL thật qua Testcontainers; provider AI được thay bằng test double
/// tất định để không phụ thuộc mạng hay API key.
/// </summary>
public sealed class RecommendationApiFixture : IAsyncLifetime
{
    private const int EmbeddingDimensions = 768;

    private readonly DeterministicChatModel _chatModel = new();
    private PostgreSqlContainer? _postgres;
    private WebApplication? _app;
    private GrpcChannel? _channel;

    public RecommendationService.RecommendationServiceClient Client { get; private set; } = null!;

    public IReadOnlyList<StructuredPrompt> CapturedPrompts => _chatModel.Prompts;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
        await _postgres.StartAsync();

        var grpcPort = GetFreePort();
        var httpPort = GetFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RECOMMENDATION:GRPC_PORT"] = grpcPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RECOMMENDATION:HTTP_PORT"] = httpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["GOOGLE_AI:API_KEY"] = "integration-test-key",
            ["GOOGLE_AI:CHAT_MODEL"] = "gemini-2.0-flash",
            ["KNOWLEDGE_DB:CORPUS_KEY"] = "che-do-an-truong-tho",
            ["KNOWLEDGE_DB:SCHEMA"] = "kb",
            ["ConnectionStrings:Recommendation"] = _postgres.GetConnectionString(),
        });

        RecommendationStartup.Configure(builder);

        builder.Services.RemoveAll<IChatModel>();
        builder.Services.RemoveAll<IEmbeddingProvider>();
        builder.Services.AddSingleton<IChatModel>(_chatModel);
        builder.Services.AddSingleton<IEmbeddingProvider>(new StubEmbeddingProvider());

        _app = builder.Build();
        await RecommendationStartup.RunAsync(_app, CancellationToken.None);
        await _app.StartAsync();

        _channel = GrpcChannel.ForAddress(
            $"http://127.0.0.1:{grpcPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true },
            });
        Client = new RecommendationService.RecommendationServiceClient(_channel);
    }

    public async Task DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.ShutdownAsync();
            _channel.Dispose();
        }

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// Chọn 5 ứng viên đầu tiên của danh sách ĐÃ ĐƯỢC LỌC (chỉ số trong prompt),
    /// không sinh món mới và không sinh số liệu dinh dưỡng.
    /// </summary>
    private sealed class DeterministicChatModel : IChatModel
    {
        private const int SelectionCount = 5;

        public List<StructuredPrompt> Prompts { get; } = [];

        public Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);

            var selections = new JsonArray();
            for (var index = 0; index < SelectionCount; index++)
            {
                selections.Add(new JsonObject
                {
                    ["food_index"] = index,
                    ["rationale"] = $"Phù hợp mục tiêu còn thiếu (ứng viên {index}).",
                    ["citations"] = new JsonArray(),
                });
            }

            return Task.FromResult(new ChatCompletion
            {
                Payload = new JsonObject { ["selections"] = selections },
            });
        }
    }

    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        private static readonly float[] Vector = BuildVector();

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(Vector);

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(texts);

            var vectors = new List<float[]>(texts.Count);
            foreach (var _ in texts)
            {
                vectors.Add(Vector);
            }

            return Task.FromResult<IReadOnlyList<float[]>>(vectors);
        }

        private static float[] BuildVector()
        {
            var value = 1f / MathF.Sqrt(EmbeddingDimensions);
            var vector = new float[EmbeddingDimensions];
            Array.Fill(vector, value);
            return vector;
        }
    }
}
