using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;
using Recommendation.Infrastructure.Options;

namespace Recommendation.Infrastructure.Adapters.GoogleAi;

/// <summary>
/// Adapter sinh câu trả lời có cấu trúc qua Gemini. Model đọc từ
/// <see cref="GoogleAiOptions.ChatModel"/>; schema lấy từ <see cref="SchemaFactory"/>.
/// Retry 2 lần với exponential backoff; lỗi provider hoặc JSON không đọc được
/// được trả về dưới dạng tín hiệu abstain, không ném ra ngoài (D-12).
/// </summary>
public sealed class GoogleAiChatModel : IChatModel
{
    private const double Temperature = 0.1;

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
    private readonly GoogleAiOptions _options;
    private readonly ILogger<GoogleAiChatModel> _logger;

    public GoogleAiChatModel(
        Client client,
        IOptions<GoogleAiOptions> options,
        ILogger<GoogleAiChatModel> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatCompletion> CompleteAsync(
        StructuredPrompt prompt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var config = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseJsonSchema = SchemaFactory.Create(prompt.ResponseKind),
            SystemInstruction = new Content
            {
                Parts = [new Part { Text = prompt.SystemInstruction }],
            },
            Temperature = Temperature,
        };

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.TimeoutMs);

        try
        {
            var response = await RetryPipeline.ExecuteAsync(
                async token => await _client.Models
                    .GenerateContentAsync(_options.ChatModel, prompt.UserPrompt, config, token)
                    .ConfigureAwait(false),
                timeoutSource.Token).ConfigureAwait(false);

            return Parse(response.Text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini chat vượt quá thời gian chờ; trả abstain.");
            return Abstain("provider_timeout");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Gọi Gemini chat thất bại sau khi retry; trả abstain.");
            return Abstain("provider_unavailable");
        }
    }

    private static ChatCompletion Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Abstain("empty_response");
        }

        try
        {
            return JsonNode.Parse(text) is JsonObject payload
                ? new ChatCompletion { Payload = payload }
                : Abstain("unexpected_response_shape");
        }
        catch (JsonException)
        {
            return Abstain("invalid_json");
        }
    }

    private static ChatCompletion Abstain(string reason) =>
        new() { Abstained = true, AbstainReason = reason };
}
