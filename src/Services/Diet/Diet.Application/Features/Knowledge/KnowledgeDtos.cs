namespace Diet.Application.Features.Knowledge;

public sealed record AskKnowledgeRequest(string Question, int? ChapterFilter);

public sealed record AskKnowledgeStreamRequest(Guid? ConversationId, string Question, int? ChapterFilter);

public sealed record KnowledgeCitationResponse(int ChapterNumber, string ChapterTitle, int BookPage,
    int PdfPage, string Quote, bool Verified, string SourceFile);

public sealed record KnowledgeAnswerResponse(string Answer, IReadOnlyList<KnowledgeCitationResponse> Citations,
    bool Abstained, string AbstainReason, string Disclaimer);

public sealed record KnowledgeSnippetResponse(int ChapterNumber, string ChapterTitle, int BookPage,
    int PdfPage, string Text, double Score, bool Verified);

public sealed record KnowledgeSearchResponse(IReadOnlyList<KnowledgeSnippetResponse> Snippets);

internal static class KnowledgeMapper
{
    internal static KnowledgeCitationResponse Citation(Longevia.Recommendation.Contracts.V1.Citation value) =>
        new(value.ChapterNumber, value.ChapterTitle, value.BookPage, value.PdfPage, value.Quote,
            value.Verified, value.SourceFile);

    internal static KnowledgeAnswerResponse From(Longevia.Recommendation.Contracts.V1.AskLongevityQuestionResponse value) =>
        new(value.Answer, value.Citations.Select(Citation).ToArray(), value.Abstained, value.AbstainReason,
            value.Disclaimer?.Text ?? string.Empty);

    internal static KnowledgeSearchResponse From(Longevia.Recommendation.Contracts.V1.SearchKnowledgeResponse value) =>
        new(value.Snippets.Select(x => new KnowledgeSnippetResponse(x.ChapterNumber, x.ChapterTitle,
            x.BookPage, x.PdfPage, x.Text, x.Score, x.Verified)).ToArray());
}
