using Diet.Application.Abstractions;
using Diet.Application.Common;
using Longevia.Recommendation.Contracts.V1;
using Longevia.Shared.Auth;
using Longevia.Shared.Errors;
using Microsoft.Extensions.Options;

namespace Diet.Application.Features.Knowledge;

public sealed class KnowledgeUseCases(IKnowledgeProvider provider, ICurrentUser user, TimeProvider time,
    IOptions<RecommendationBudgetOptions> budget)
{
    public async Task<KnowledgeAnswerResponse> AskAsync(AskKnowledgeRequest request, CancellationToken ct)
    {
        var question = request.Question?.Trim() ?? string.Empty;
        if (question.Length is < 1 or > 500)
            throw AppException.BadRequest("question phải có 1..500 ký tự.", "diet.invalid_question");
        if (request.ChapterFilter is < 0)
            throw AppException.BadRequest("chapterFilter không hợp lệ.", "diet.invalid_chapter_filter");

        var proto = new AskLongevityQuestionRequest
        {
            UserId = user.RequiredId().ToString("D"),
            Question = question,
            ChapterFilter = request.ChapterFilter ?? 0,
        };
        var result = await provider.AskAsync(proto, Deadline(), ct);
        if (string.IsNullOrWhiteSpace(result.Disclaimer?.Text))
            throw new AppException("Câu trả lời thiếu disclaimer an toàn.", 502,
                "Dịch vụ tri thức trả dữ liệu không hợp lệ.", "diet.ai_integration_failed");
        return KnowledgeMapper.From(result);
    }

    public async Task<KnowledgeSearchResponse> SearchAsync(string query, int? chapterFilter, int? topK,
        CancellationToken ct)
    {
        var normalized = query?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 300)
            throw AppException.BadRequest("query phải có 1..300 ký tự.", "diet.invalid_query");
        if (chapterFilter is < 0)
            throw AppException.BadRequest("chapterFilter không hợp lệ.", "diet.invalid_chapter_filter");
        if (topK is < 1 or > 20)
            throw AppException.BadRequest("topK phải nằm trong 1..20.", "diet.invalid_top_k");

        var proto = new SearchKnowledgeRequest
        {
            Query = normalized,
            ChapterFilter = chapterFilter ?? 0,
            TopK = topK ?? 8,
        };
        return KnowledgeMapper.From(await provider.SearchAsync(proto, Deadline(), ct));
    }

    private DateTimeOffset Deadline() =>
        time.GetUtcNow().AddMilliseconds(budget.Value.DeadlineMilliseconds);
}
