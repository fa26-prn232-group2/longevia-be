using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
using Recommendation.IntegrationTests.Features.Shared;
using Testcontainers.PostgreSql;

namespace Recommendation.IntegrationTests.Features.Meals;

/// <summary>
/// Host thật của <c>Recommendation.Api</c> (Kestrel + DI + migration) trên
/// PostgreSQL thật qua Testcontainers; provider AI được thay bằng test double
/// tất định để không phụ thuộc mạng hay API key.
/// </summary>
public sealed partial class RecommendationApiFixture : IAsyncLifetime
{
    private const int EmbeddingDimensions = 768;

    private readonly DeterministicChatModel _chatModel = new();
    private PostgreSqlContainer? _postgres;
    private WebApplication? _app;
    private GrpcChannel? _channel;

    public RecommendationService.RecommendationServiceClient Client { get; private set; } = null!;

    public IReadOnlyList<StructuredPrompt> CapturedPrompts => _chatModel.Prompts;

    /// <summary>Cây DI của host thật, dùng để nạp corpus cho test cần dữ liệu sách.</summary>
    public IServiceProvider Services =>
        _app?.Services ?? throw new InvalidOperationException("Fixture chưa khởi động.");

    /// <summary>Lượt gọi <c>GenerateMealPlan</c> kế tiếp sẽ nhận phản hồi abstain.</summary>
    public void AbstainNextMealPlan() => _chatModel.AbstainNextMealPlan();

    /// <summary>Lượt gọi <c>GenerateMealPlan</c> kế tiếp sẽ trả quote không có trong sách.</summary>
    public void UseInvalidCitationForNextMealPlan() => _chatModel.UseInvalidCitationForNextMealPlan();

    /// <summary>Lượt gọi <c>GenerateWorkoutPlan</c> kế tiếp sẽ nhận phản hồi abstain.</summary>
    public void AbstainNextWorkoutPlan() => _chatModel.AbstainNextWorkoutPlan();

    /// <summary>Lượt gọi <c>GenerateWorkoutPlan</c> kế tiếp sẽ trả quote không có trong sách.</summary>
    public void UseInvalidCitationForNextWorkoutPlan() => _chatModel.UseInvalidCitationForNextWorkoutPlan();

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
    /// Double tất định theo loại phản hồi: chọn món lấy 5 ứng viên đầu tiên của
    /// danh sách ĐÃ LỌC (không sinh món/số liệu mới); hỏi sách do
    /// <see cref="GroundedAnswerDouble"/> trả lời từ chính CONTEXT đã truy xuất.
    /// Hai kind plan chỉ trả chỉ số món/hoạt động + citation, đúng schema
    /// <c>MealPlanSelection</c>/<c>WorkoutPlanSelection</c>.
    /// </summary>
    private sealed partial class DeterministicChatModel : IChatModel
    {
        private const int SelectionCount = 5;
        private const int CitationWordCount = 12;

        private int _abstainNextMealPlan;
        private int _invalidCitationNextMealPlan;
        private int _abstainNextWorkoutPlan;
        private int _invalidCitationNextWorkoutPlan;

        public List<StructuredPrompt> Prompts { get; } = [];

        public void AbstainNextMealPlan() => Interlocked.Exchange(ref _abstainNextMealPlan, 1);

        public void UseInvalidCitationForNextMealPlan() => Interlocked.Exchange(ref _invalidCitationNextMealPlan, 1);

        public void AbstainNextWorkoutPlan() => Interlocked.Exchange(ref _abstainNextWorkoutPlan, 1);

        public void UseInvalidCitationForNextWorkoutPlan() => Interlocked.Exchange(ref _invalidCitationNextWorkoutPlan, 1);

        public Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);

            var completion = prompt.ResponseKind switch
            {
                StructuredResponseKind.GroundedAnswer => GroundedAnswerDouble.Complete(prompt),
                StructuredResponseKind.NutrientGapAnalysis => NutrientGapAnalysisDouble.Complete(prompt),
                StructuredResponseKind.MealPlanSelection => CompleteMealPlan(prompt.UserPrompt),
                StructuredResponseKind.WorkoutPlanSelection => CompleteWorkoutPlan(prompt.UserPrompt),
                _ => MealSelection(),
            };

            return Task.FromResult(completion);
        }

        private ChatCompletion CompleteMealPlan(string userPrompt)
        {
            if (Interlocked.Exchange(ref _abstainNextMealPlan, 0) == 1)
            {
                return new ChatCompletion { Abstained = true, AbstainReason = "provider_unavailable" };
            }

            var invalidCitation = Interlocked.Exchange(ref _invalidCitationNextMealPlan, 0) == 1;

            return MealPlanSelection(userPrompt, invalidCitation);
        }

        private static ChatCompletion MealSelection()
        {
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

            return new ChatCompletion
            {
                Payload = new JsonObject { ["selections"] = selections },
            };
        }

        private static ChatCompletion MealPlanSelection(string userPrompt, bool invalidCitation)
        {
            var quote = invalidCitation
                ? "Câu trích dẫn này không xuất hiện trong bất kỳ đoạn sách nào."
                : ExtractFirstCorpusQuote(userPrompt);
            var days = new JsonArray();

            foreach (var (date, segment) in SplitByDate(userPrompt))
            {
                var meals = new JsonArray();
                foreach (var mealType in DetectMealTypes(segment))
                {
                    meals.Add(new JsonObject
                    {
                        ["meal_type"] = mealType,
                        ["food_indices"] = new JsonArray(0),
                        ["citations"] = CitationArray(quote),
                    });
                }

                days.Add(new JsonObject
                {
                    ["date"] = date,
                    ["meals"] = meals,
                });
            }

            return new ChatCompletion
            {
                Payload = new JsonObject { ["days"] = days },
            };
        }

        private ChatCompletion CompleteWorkoutPlan(string userPrompt)
        {
            if (Interlocked.Exchange(ref _abstainNextWorkoutPlan, 0) == 1)
            {
                return new ChatCompletion { Abstained = true, AbstainReason = "provider_unavailable" };
            }

            var invalidCitation = Interlocked.Exchange(ref _invalidCitationNextWorkoutPlan, 0) == 1;

            return WorkoutPlanSelection(userPrompt, invalidCitation);
        }

        private static ChatCompletion WorkoutPlanSelection(string userPrompt, bool invalidCitation)
        {
            var quote = invalidCitation
                ? "Câu trích dẫn này không xuất hiện trong bất kỳ đoạn sách nào."
                : ExtractFirstCorpusQuote(userPrompt);
            var days = new JsonArray();

            foreach (var (date, segment) in SplitByDate(userPrompt))
            {
                if (!LooksLikeWorkoutDay(segment))
                {
                    continue;
                }

                days.Add(new JsonObject
                {
                    ["date"] = date,
                    ["activity_index"] = 0,
                    ["citations"] = CitationArray(quote),
                });
            }

            return new ChatCompletion
            {
                Payload = new JsonObject { ["days"] = days },
            };
        }

        private static IReadOnlyList<(string Date, string Segment)> SplitByDate(string userPrompt)
        {
            var matches = IsoDatePattern().Matches(userPrompt);
            var orderedDates = new List<string>();
            var segments = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var index = 0; index < matches.Count; index++)
            {
                var match = matches[index];
                var date = match.Value;
                var start = match.Index + match.Length;
                var end = index + 1 < matches.Count ? matches[index + 1].Index : userPrompt.Length;
                var segment = userPrompt[start..end];

                if (segments.TryGetValue(date, out var existing))
                {
                    segments[date] = existing + "\n" + segment;
                    continue;
                }

                orderedDates.Add(date);
                segments[date] = segment;
            }

            return orderedDates.Select(date => (date, segments[date])).ToList();
        }

        private static IReadOnlyList<string> DetectMealTypes(string segment)
        {
            var lower = segment.ToLowerInvariant();
            var mealTypes = new List<string>(4);

            if (lower.Contains("breakfast", StringComparison.Ordinal)
                || lower.Contains("bữa sáng", StringComparison.Ordinal))
            {
                mealTypes.Add("BREAKFAST");
            }

            if (lower.Contains("lunch", StringComparison.Ordinal)
                || lower.Contains("bữa trưa", StringComparison.Ordinal))
            {
                mealTypes.Add("LUNCH");
            }

            if (lower.Contains("dinner", StringComparison.Ordinal)
                || lower.Contains("bữa tối", StringComparison.Ordinal))
            {
                mealTypes.Add("DINNER");
            }

            if (lower.Contains("snack", StringComparison.Ordinal)
                || lower.Contains("bữa phụ", StringComparison.Ordinal))
            {
                mealTypes.Add("SNACK");
            }

            return mealTypes;
        }

        private static bool LooksLikeWorkoutDay(string segment)
        {
            var lower = segment.ToLowerInvariant();

            return lower.Contains("buổi", StringComparison.Ordinal)
                || lower.Contains("session", StringComparison.Ordinal)
                || lower.Contains("activity", StringComparison.Ordinal)
                || lower.Contains("hoạt động", StringComparison.Ordinal);
        }

        private static string? ExtractFirstCorpusQuote(string userPrompt)
        {
            const string begin = "[BEGIN CORPUS DATA]";
            const string end = "[END CORPUS DATA]";

            var start = userPrompt.IndexOf(begin, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += begin.Length;
            var endIndex = userPrompt.IndexOf(end, start, StringComparison.Ordinal);
            var block = endIndex < 0 ? userPrompt[start..] : userPrompt[start..endIndex];

            foreach (var line in block.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith('[')
                    && (line.Contains("chapter=", StringComparison.Ordinal)
                        || line.Contains("status=", StringComparison.Ordinal)))
                {
                    continue;
                }

                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    return string.Join(' ', words.Take(CitationWordCount));
                }
            }

            return null;
        }

        private static JsonArray CitationArray(string? quote)
        {
            var citations = new JsonArray();
            if (quote is not null)
            {
                citations.Add(new JsonObject
                {
                    ["chunk_index"] = 0,
                    ["quote"] = quote,
                });
            }

            return citations;
        }

        [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant)]
        private static partial Regex IsoDatePattern();
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
