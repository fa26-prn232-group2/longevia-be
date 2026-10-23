using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Common.Errors;
using Recommendation.Application.Features.Search;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Ask;

/// <summary>
/// Use case hỏi tri thức trong sách (FR-015..FR-019, FR-030..FR-033): chặn câu hỏi
/// y tế ngoài phạm vi → truy xuất hybrid → prompt 3 tầng → mô hình → kiểm chứng
/// trích dẫn bằng code. Thiếu căn cứ hoặc provider lỗi thì abstain, không suy diễn.
/// </summary>
public sealed class AskLongevityQuestionUseCase(
    IKnowledgeStore knowledgeStore,
    PromptBuilder promptBuilder,
    IChatModel chatModel,
    CitationResolver citationResolver,
    SafetyGuard safetyGuard)
{
    private const string NoChunkAnswer =
        "Không tìm thấy nội dung liên quan trong sách để trả lời câu hỏi này.";

    private const string CorpusNotLoadedAnswer =
        "Kho tri thức chưa được nạp nên chưa thể trả lời; hãy nạp corpus "
        + "(service kb-init hoặc make kb-ensure).";

    private const string ProviderAnswer =
        "Dịch vụ AI tạm thời không phản hồi nên chưa thể trả lời; hệ thống không tạo nội dung "
        + "ngoài sách.";

    private const string NoCitationAnswer =
        "Không tìm thấy trích dẫn khớp với nội dung sách nên hệ thống không đưa ra câu trả lời.";

    /// <summary>Chạy toàn bộ pipeline hỏi sách cho một lời gọi.</summary>
    public async Task<AskLongevityQuestionResult> ExecuteAsync(
        AskLongevityQuestionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var question = QueryNormalizer.Normalize(request.Question);
        if (question.Length == 0)
        {
            throw RecommendationErrors.InvalidQuestion();
        }

        var refusal = safetyGuard.FindRefusal(question);
        if (refusal is not null)
        {
            return Refused(refusal);
        }

        var chunks = await RetrieveAsync(question, request.ChapterFilter, cancellationToken).ConfigureAwait(false);
        if (chunks.Count == 0)
        {
            // Phân biệt "chưa nạp corpus" với "có corpus nhưng không khớp câu hỏi":
            // người đọc cần biết phải chạy kb-init/kb-load, không phải đổi câu hỏi.
            var loaded = await knowledgeStore
                .CountActiveAsync(cancellationToken)
                .ConfigureAwait(false) > 0;
            return loaded
                ? Abstain(NoChunkAnswer, "no_matching_chunk")
                : Abstain(CorpusNotLoadedAnswer, "corpus_not_loaded");
        }

        return await AnswerFromChunksAsync(question, chunks, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AskLongevityQuestionResult> AnswerFromChunksAsync(
        string question,
        IReadOnlyList<RetrievedChunk> chunks,
        CancellationToken cancellationToken)
    {
        var prompt = promptBuilder.Build(question, chunks);
        var completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
        if (completion is not { Abstained: false, Payload: not null })
        {
            return Abstain(ProviderAnswer, completion.AbstainReason ?? "provider_unavailable");
        }

        var resolution = citationResolver.Resolve(completion.Payload, chunks);
        if (resolution.Abstained)
        {
            return Abstain(NoCitationAnswer, "citation_not_verified");
        }

        var answer = ReadAnswer(completion.Payload);
        if (answer.Length == 0)
        {
            return Abstain(NoCitationAnswer, "empty_answer");
        }

        return new AskLongevityQuestionResult
        {
            Answer = answer,
            Citations = resolution.Citations,
            Disclaimer = DisclaimerText.Value,
        };
    }

    private Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string question,
        int chapterFilter,
        CancellationToken cancellationToken) =>
        knowledgeStore.SearchAsync(
            new KnowledgeQuery
            {
                Query = question,
                ChapterNumber = chapterFilter,
            },
            cancellationToken);

    private static string ReadAnswer(JsonObject payload)
    {
        if (payload["answer"] is JsonValue value
            && value.TryGetValue<string>(out var text)
            && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return string.Empty;
    }

    private static AskLongevityQuestionResult Abstain(string answer, string reason) => new()
    {
        Answer = answer,
        Citations = [],
        Abstained = true,
        AbstainReason = reason,
        Disclaimer = DisclaimerText.Value,
    };

    private static AskLongevityQuestionResult Refused(string refusal) => new()
    {
        Answer = refusal,
        Citations = [],
        Disclaimer = DisclaimerText.Value,
    };
}
