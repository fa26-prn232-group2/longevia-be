using System.Text.Json.Nodes;
using Recommendation.Domain.Models;

namespace Recommendation.IntegrationTests.Features.Shared;

/// <summary>
/// Test double tất định cho phản hồi phân tích vi chất: trích nguyên văn phần
/// đầu đoạn CONTEXT đầu tiên làm citation; không gợi ý thực phẩm do double không
/// có khả năng đối chiếu ngữ nghĩa. Không gọi provider thật.
/// </summary>
internal static class NutrientGapAnalysisDouble
{
    private const string BeginMarker = "[BEGIN CORPUS DATA]";
    private const string EndMarker = "[END CORPUS DATA]";
    private const string ChunkMarker = "--- ĐOẠN ";
    private const int MaxQuoteLength = 160;

    internal static ChatCompletion Complete(StructuredPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var content = FirstChunkContent(prompt.UserPrompt);
        if (content.Length == 0)
        {
            return Payload(string.Empty, null);
        }

        var quote = content.Length <= MaxQuoteLength ? content : content[..MaxQuoteLength];
        return Payload($"Theo sách: {quote}", quote);
    }

    private static ChatCompletion Payload(string summary, string? quote)
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

        return new ChatCompletion
        {
            Payload = new JsonObject
            {
                ["analysis_summary"] = summary,
                ["suggested_foods"] = new JsonArray(),
                ["citations"] = citations,
            },
        };
    }

    private static string FirstChunkContent(string userPrompt)
    {
        var begin = userPrompt.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = userPrompt.IndexOf(EndMarker, StringComparison.Ordinal);
        if (begin < 0 || end <= begin)
        {
            return string.Empty;
        }

        var corpus = userPrompt[(begin + BeginMarker.Length)..end];
        var chunkStart = corpus.IndexOf(ChunkMarker, StringComparison.Ordinal);
        if (chunkStart < 0)
        {
            return string.Empty;
        }

        var block = corpus[(chunkStart + ChunkMarker.Length)..];
        var newline = block.IndexOf('\n');
        return newline < 0 ? string.Empty : block[(newline + 1)..].Trim();
    }
}
