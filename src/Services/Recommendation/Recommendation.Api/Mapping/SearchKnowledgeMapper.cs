using Recommendation.Application.Features.Search;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>
/// Map giữa message gRPC của contract và DTO nội bộ của Application cho RPC tra
/// cứu tri thức. Contract type chỉ tồn tại ở tầng Api nên Application không tham
/// chiếu generated code.
/// </summary>
internal static class SearchKnowledgeMapper
{
    /// <summary>Chuyển request gRPC sang DTO nội bộ.</summary>
    internal static SearchKnowledgeRequest ToApplicationRequest(Contracts.SearchKnowledgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SearchKnowledgeRequest
        {
            Query = request.Query,
            ChapterFilter = request.ChapterFilter,
            TopK = request.TopK,
        };
    }

    /// <summary>Chuyển kết quả use case sang message phản hồi gRPC.</summary>
    internal static Contracts.SearchKnowledgeResponse ToProtoResponse(SearchKnowledgeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var response = new Contracts.SearchKnowledgeResponse();
        response.Snippets.AddRange(result.Snippets.Select(ToProtoSnippet));

        return response;
    }

    private static Contracts.KnowledgeSnippet ToProtoSnippet(KnowledgeSnippet snippet) => new()
    {
        ChapterNumber = snippet.ChapterNumber,
        ChapterTitle = snippet.ChapterTitle,
        BookPage = snippet.BookPage,
        PdfPage = snippet.PdfPage,
        Text = snippet.Text,
        Score = snippet.Score,
        Verified = snippet.Verified,
    };
}
