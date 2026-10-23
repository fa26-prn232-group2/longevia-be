using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Common.Errors;
using Recommendation.Infrastructure.Options;

namespace Recommendation.Infrastructure.Adapters.GoogleAi;

/// <summary>
/// Adapter nhúng vector qua Gemini Developer API. Model và số chiều chỉ đọc từ
/// <see cref="RagOptions"/> để luôn khớp cột <c>vector(n)</c> của <c>kb.chunk</c>
/// (D-04, D-11). Mọi vector trả về đều được L2-normalize (D-13).
/// </summary>
public sealed class GoogleAiEmbeddingProvider : IEmbeddingProvider
{
    private const string DocumentTaskType = "RETRIEVAL_DOCUMENT";
    private const string QueryTaskType = "RETRIEVAL_QUERY";

    private static readonly ResiliencePipeline RetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<Exception>(static ex => ex is not OperationCanceledException),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
        })
        .Build();

    private readonly Client _client;
    private readonly RagOptions _ragOptions;
    private readonly ILogger<GoogleAiEmbeddingProvider> _logger;

    public GoogleAiEmbeddingProvider(
        Client client,
        IOptions<RagOptions> ragOptions,
        ILogger<GoogleAiEmbeddingProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(ragOptions);

        _client = client;
        _ragOptions = ragOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // Luồng runtime chỉ nhúng câu hỏi, nên dùng RETRIEVAL_QUERY; nhúng tài
        // liệu là RETRIEVAL_DOCUMENT (D-12).
        var response = await EmbedCoreAsync(
            [text],
            QueryTaskType,
            cancellationToken).ConfigureAwait(false);

        return response[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
        {
            return [];
        }

        return await EmbedCoreAsync(texts, DocumentTaskType, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<float[]>> EmbedCoreAsync(
        IReadOnlyList<string> texts,
        string taskType,
        CancellationToken cancellationToken)
    {
        var contents = texts
            .Select(text => new Content { Parts = [new Part { Text = text }] })
            .ToList();
        var config = new EmbedContentConfig
        {
            OutputDimensionality = _ragOptions.EmbeddingDim,
            TaskType = taskType,
        };

        try
        {
            var response = await RetryPipeline.ExecuteAsync(
                async token => await _client.Models
                    .EmbedContentAsync(_ragOptions.EmbeddingModel, contents, config, token)
                    .ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return ReadVectors(response, texts.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Gọi Gemini Embedding thất bại sau khi retry.");
            throw RecommendationErrors.ProviderUnavailable();
        }
    }

    private static IReadOnlyList<float[]> ReadVectors(EmbedContentResponse response, int expectedCount)
    {
        var embeddings = response.Embeddings;
        if (embeddings is null || embeddings.Count != expectedCount)
        {
            throw RecommendationErrors.ProviderUnavailable();
        }

        var vectors = new float[embeddings.Count][];
        for (var index = 0; index < embeddings.Count; index++)
        {
            var values = embeddings[index].Values
                ?? throw RecommendationErrors.ProviderUnavailable();
            vectors[index] = Normalize(values);
        }

        return vectors;
    }

    private static float[] Normalize(IReadOnlyList<double> values)
    {
        var result = new float[values.Count];
        double sumOfSquares = 0;

        for (var index = 0; index < values.Count; index++)
        {
            sumOfSquares += values[index] * values[index];
        }

        var norm = Math.Sqrt(sumOfSquares);
        if (norm <= double.Epsilon)
        {
            return result;
        }

        for (var index = 0; index < values.Count; index++)
        {
            result[index] = (float)(values[index] / norm);
        }

        return result;
    }
}
