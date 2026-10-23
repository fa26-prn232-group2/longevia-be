using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Recommendation.Domain.Models;

namespace Recommendation.IntegrationTests.Features.Shared;

/// <summary>
/// Test double tất định cho phản hồi có căn cứ: chọn đoạn trong CONTEXT có nhiều
/// từ chung nhất với câu hỏi, trích nguyên văn phần đầu đoạn đó. Không có từ chung
/// (câu ngoài sách) thì trả payload rỗng để pipeline abstain — không gọi provider thật.
/// </summary>
internal static class GroundedAnswerDouble
{
    private const int MinTokenLength = 4;
    private const int MaxQuoteLength = 180;

    private static readonly char[] Separators =
    [
        ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '"', '“', '”', '-', '/',
    ];

    internal static ChatCompletion Complete(StructuredPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var chunks = ParseChunks(prompt.UserPrompt);
        var questionTokens = Tokenize(ExtractQuestion(prompt.UserPrompt));

        var best = FindBestChunk(chunks, questionTokens);
        if (best is null)
        {
            return EmptyAnswer();
        }

        var quote = best.Content.Length <= MaxQuoteLength
            ? best.Content
            : best.Content[..MaxQuoteLength];

        var citations = new JsonArray(
            new JsonObject
            {
                ["chunk_index"] = best.Index,
                ["quote"] = quote,
            });

        return new ChatCompletion
        {
            Payload = new JsonObject
            {
                ["answer"] = $"Theo sách: {quote}",
                ["citations"] = citations,
            },
        };
    }

    private static PromptChunk? FindBestChunk(
        IReadOnlyList<PromptChunk> chunks,
        HashSet<string> questionTokens)
    {
        PromptChunk? best = null;
        var bestOverlap = 0;

        foreach (var chunk in chunks)
        {
            var overlap = CountOverlap(questionTokens, Tokenize(chunk.Content));
            if (overlap > bestOverlap)
            {
                best = chunk;
                bestOverlap = overlap;
            }
        }

        return best;
    }

    private static IReadOnlyList<PromptChunk> ParseChunks(string userPrompt)
    {
        const string beginMarker = "[BEGIN CORPUS DATA]";
        const string endMarker = "[END CORPUS DATA]";
        const string chunkMarker = "--- ĐOẠN ";

        var begin = userPrompt.IndexOf(beginMarker, StringComparison.Ordinal);
        var end = userPrompt.IndexOf(endMarker, StringComparison.Ordinal);
        if (begin < 0 || end <= begin)
        {
            return [];
        }

        var corpus = userPrompt[(begin + beginMarker.Length)..end];
        var blocks = corpus.Split(chunkMarker, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<PromptChunk>(blocks.Length);

        foreach (var block in blocks)
        {
            var newline = block.IndexOf('\n');
            if (newline < 0)
            {
                continue;
            }

            var header = block[..newline];
            var indexText = header.Split('|')[0].Trim();
            if (!int.TryParse(indexText, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            chunks.Add(new PromptChunk(index, block[(newline + 1)..].Trim()));
        }

        return chunks;
    }

    private static string ExtractQuestion(string userPrompt)
    {
        const string marker = "CÂU HỎI:";
        const string terminator = "Chỉ trả lời";

        var start = userPrompt.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var rest = userPrompt[(start + marker.Length)..];
        var stop = rest.IndexOf(terminator, StringComparison.Ordinal);
        return (stop >= 0 ? rest[..stop] : rest).Trim();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (var token in Unaccent(text).Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length >= MinTokenLength)
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static int CountOverlap(HashSet<string> questionTokens, HashSet<string> chunkTokens)
    {
        var overlap = 0;
        foreach (var token in questionTokens)
        {
            if (chunkTokens.Contains(token))
            {
                overlap++;
            }
        }

        return overlap;
    }

    private static string Unaccent(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => character,
            });
        }

        return builder.ToString().ToLowerInvariant();
    }

    private static ChatCompletion EmptyAnswer() => new()
    {
        Payload = new JsonObject
        {
            ["answer"] = string.Empty,
            ["citations"] = new JsonArray(),
        },
    };

    private sealed record PromptChunk(int Index, string Content);
}
