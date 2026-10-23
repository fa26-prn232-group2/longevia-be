using Recommendation.Application.Abstractions;
using Recommendation.Application.Common.Errors;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Search;

/// <summary>
/// Use case tra cứu các đoạn sách theo truy vấn tự do (FR-020, FR-026): chuẩn
/// hoá truy vấn, giữ <c>top_k</c> trong khoảng 1..20 (mặc định 8), chuyển bộ lọc
/// chương xuống kho tri thức rồi map kết quả kèm chương, trang và điểm liên quan.
/// </summary>
public sealed class SearchLongevityKnowledgeUseCase(IKnowledgeStore knowledgeStore)
{
    private const int DefaultTopK = 8;
    private const int MaxTopK = 20;

    /// <summary>Chạy một lượt tra cứu tri thức trong sách.</summary>
    public async Task<SearchKnowledgeResult> ExecuteAsync(
        SearchKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = QueryNormalizer.Normalize(request.Query);
        if (query.Length == 0)
        {
            throw RecommendationErrors.InvalidQuestion();
        }

        var chunks = await knowledgeStore
            .SearchAsync(BuildQuery(query, request), cancellationToken)
            .ConfigureAwait(false);

        return new SearchKnowledgeResult { Snippets = MapSnippets(chunks) };
    }

    private static KnowledgeQuery BuildQuery(string query, SearchKnowledgeRequest request) => new()
    {
        Query = query,
        ChapterNumber = request.ChapterFilter,
        TopK = ResolveTopK(request.TopK),
    };

    private static int ResolveTopK(int requested) => requested switch
    {
        <= 0 => DefaultTopK,
        > MaxTopK => MaxTopK,
        _ => requested,
    };

    private static IReadOnlyList<KnowledgeSnippet> MapSnippets(IReadOnlyList<RetrievedChunk> chunks) =>
        chunks
            .OrderByDescending(chunk => chunk.Score)
            .Select(ToSnippet)
            .ToList();

    private static KnowledgeSnippet ToSnippet(RetrievedChunk retrieved) => new()
    {
        ChapterNumber = retrieved.Chunk.ChapterNumber,
        ChapterTitle = retrieved.Chunk.ChapterTitle ?? string.Empty,
        BookPage = retrieved.Chunk.BookPageFrom,
        PdfPage = retrieved.Chunk.PdfPageFrom,
        Text = retrieved.Chunk.Content,
        Score = retrieved.Score,
        Verified = retrieved.Chunk.Verified,
    };
}
