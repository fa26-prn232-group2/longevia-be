using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Ask;

/// <summary>
/// Kiểm chứng trích dẫn do mô hình sinh ra: <c>quote</c> phải xuất hiện nguyên
/// văn trong <c>content</c> của đoạn nguồn sau khi gộp whitespace và bỏ dấu.
/// Không khớp thì loại; mất hết trích dẫn thì hạ kết quả xuống abstain (T045).
/// </summary>
public sealed partial class CitationResolver
{
    /// <summary>Đối chiếu payload của mô hình với các đoạn đã truy xuất.</summary>
    public CitationResolution Resolve(JsonObject? payload, IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (payload?["citations"] is not JsonArray citations || chunks.Count == 0)
        {
            return Abstained();
        }

        var resolved = new List<Citation>();
        foreach (var node in citations)
        {
            var citation = ResolveOne(node as JsonObject, chunks);
            if (citation is not null)
            {
                resolved.Add(citation);
            }
        }

        return new CitationResolution
        {
            Citations = resolved,
            Abstained = resolved.Count == 0,
        };
    }

    /// <summary>
    /// Biến thể dùng cho hai RPC plan (D-08): giữ các citation có <c>quote</c> khớp
    /// nội dung đoạn nguồn, bỏ citation sai và **không** abstain khi danh sách
    /// rỗng — plan vẫn hợp lệ khi corpus rỗng.
    /// </summary>
    public IReadOnlyList<Citation> ResolveOptional(JsonObject? payload, IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (payload?["citations"] is not JsonArray citations || chunks.Count == 0)
        {
            return [];
        }

        var resolved = new List<Citation>();
        foreach (var node in citations)
        {
            var citation = ResolveOne(node as JsonObject, chunks);
            if (citation is not null)
            {
                resolved.Add(citation);
            }
        }

        return resolved;
    }

    private static Citation? ResolveOne(JsonObject? node, IReadOnlyList<RetrievedChunk> chunks)
    {
        if (node is null || !TryReadQuote(node, out var quote))
        {
            return null;
        }

        var normalizedQuote = NormalizeForMatch(quote);
        if (normalizedQuote.Length == 0)
        {
            return null;
        }

        var chunk = FindMatchingChunk(node, normalizedQuote, chunks);
        if (chunk is null)
        {
            return null;
        }

        return new Citation
        {
            ChapterNumber = chunk.ChapterNumber,
            ChapterTitle = chunk.ChapterTitle ?? string.Empty,
            BookPage = chunk.BookPageFrom,
            PdfPage = chunk.PdfPageFrom,
            Quote = quote,
            Verified = chunk.Verified,
            SourceFile = chunk.SourceFile ?? string.Empty,
        };
    }

    private static KnowledgeChunk? FindMatchingChunk(
        JsonObject node,
        string normalizedQuote,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        if (TryReadChunkIndex(node, out var chunkIndex)
            && chunkIndex >= 0
            && chunkIndex < chunks.Count
            && Contains(chunks[chunkIndex].Chunk, normalizedQuote))
        {
            return chunks[chunkIndex].Chunk;
        }

        foreach (var chunk in chunks)
        {
            if (Contains(chunk.Chunk, normalizedQuote))
            {
                return chunk.Chunk;
            }
        }

        return null;
    }

    private static bool Contains(KnowledgeChunk chunk, string normalizedQuote) =>
        NormalizeForMatch(chunk.Content).Contains(normalizedQuote, StringComparison.Ordinal);

    private static bool TryReadQuote(JsonObject node, out string quote)
    {
        quote = string.Empty;
        if (!node.TryGetPropertyValue("quote", out var value)
            || value is not JsonValue jsonValue
            || !jsonValue.TryGetValue<string>(out var text)
            || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        quote = text.Trim();
        return true;
    }

    private static bool TryReadChunkIndex(JsonObject node, out int chunkIndex)
    {
        chunkIndex = -1;
        return node.TryGetPropertyValue("chunk_index", out var value)
            && value is JsonValue jsonValue
            && jsonValue.TryGetValue<int>(out chunkIndex);
    }

    private static string NormalizeForMatch(string text)
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

        return Whitespace().Replace(builder.ToString(), " ").Trim().ToLowerInvariant();
    }

    private static CitationResolution Abstained() => new() { Citations = [], Abstained = true };

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
