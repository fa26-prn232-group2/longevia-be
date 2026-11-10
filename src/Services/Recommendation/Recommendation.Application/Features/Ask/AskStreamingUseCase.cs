using System.Runtime.CompilerServices;
using System.Text;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Search;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Ask;

/// <summary>Một lượt hội thoại trước đó (chat nhiều lượt).</summary>
public sealed record ConversationTurn(string Role, string Content);

/// <summary>Sự kiện stream trả về cho client khi hỏi sách dạng chat.</summary>
public abstract record AskStreamEvent;

/// <summary>Một đoạn markdown của câu trả lời.</summary>
public sealed record AskDeltaEvent(string Text) : AskStreamEvent;

/// <summary>Trích dẫn sách gửi sau khi stream xong.</summary>
public sealed record AskCitationEvent(Citation Citation) : AskStreamEvent;

/// <summary>Kết thúc lượt trả lời.</summary>
public sealed record AskDoneEvent(bool Abstained, string AbstainReason, string Disclaimer) : AskStreamEvent;

/// <summary>Lỗi có thể hiển thị cho người dùng (không kèm chi tiết nội bộ).</summary>
public sealed record AskErrorEvent(string Code, string Message) : AskStreamEvent;

public sealed record AskStreamRequest(
    string Question,
    int ChapterFilter,
    IReadOnlyList<ConversationTurn> History);

/// <summary>
/// Chat hỏi sách: giữ nguyên nguyên tắc grounded (chỉ trả lời từ CONTEXT, luôn
/// kèm trích dẫn) nhưng stream từng đoạn để người dùng thấy phản hồi ngay.
/// Chào hỏi/cảm ơn/câu ngoài phạm vi được trả lời bằng câu cố định, không gọi AI.
/// </summary>
public sealed class AskStreamingUseCase(
    IKnowledgeStore knowledgeStore,
    PromptBuilder promptBuilder,
    IChatStreamModel chatStreamModel,
    SafetyGuard safetyGuard)
{
    private const int MaxCitationCount = 3;
    private const int CitationQuoteLength = 280;
    private const int HistoryLimit = 6;

    private const string CorpusNotLoadedAnswer =
        "Kho tri thức của sách chưa được nạp nên mình chưa thể trả lời từ sách. "
        + "Quản trị viên cần nạp corpus trước khi hỏi.";

    private const string NoMatchingChunkAnswer =
        "Mình chưa tìm thấy đoạn nào trong sách thật sự khớp với câu hỏi này. "
        + "Bạn thử nêu cụ thể hơn (tên thực phẩm, thói quen, tên chương) nhé.";

    private const string ProviderErrorAnswer =
        "Trợ lý AI tạm thời không phản hồi nên mình chưa thể trả lời. Bạn thử lại sau giây lát nhé.";

    private const string CapabilityAnswer =
        "Mình là trợ lý đọc sách “Chế độ ăn trường thọ”. Bạn có thể hỏi về dinh dưỡng, "
        + "nhịn ăn gián đoạn, vận động, giấc ngủ và các thói quen sống khỏe trong sách. "
        + "Mình chỉ trả lời khi tìm được đoạn sách tương ứng và luôn kèm trích dẫn.";

    /// <summary>Chạy pipeline chat và phát sự kiện theo thứ tự delta → citation → done.</summary>
    public async IAsyncEnumerable<AskStreamEvent> ExecuteAsync(
        AskStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var question = QueryNormalizer.Normalize(request.Question);
        if (question.Length == 0)
        {
            yield return new AskDoneEvent(false, "invalid_question", DisclaimerText.Value);
            yield break;
        }

        if (MetaIntent.TryGet(question, out var metaAnswer))
        {
            yield return new AskDeltaEvent(metaAnswer);
            yield return new AskDoneEvent(false, "meta_intent", DisclaimerText.Value);
            yield break;
        }

        var refusal = safetyGuard.FindRefusal(question);
        if (refusal is not null)
        {
            yield return new AskDeltaEvent(refusal);
            yield return new AskDoneEvent(false, "medical_refusal", DisclaimerText.Value);
            yield break;
        }

        var query = BuildRetrievalQuery(question, request.History);
        var chunks = await knowledgeStore.SearchAsync(
                new KnowledgeQuery { Query = query, ChapterNumber = request.ChapterFilter },
                cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            var loaded = await knowledgeStore.CountActiveAsync(cancellationToken).ConfigureAwait(false) > 0;
            var (answer, reason) = loaded
                ? (NoMatchingChunkAnswer, "no_matching_chunk")
                : (CorpusNotLoadedAnswer, "corpus_not_loaded");

            yield return new AskDeltaEvent(answer);
            yield return new AskDoneEvent(true, reason, DisclaimerText.Value);
            yield break;
        }

        var prompt = promptBuilder.BuildStreaming(question, request.History, chunks);
        var complete = new StringBuilder();
        var failed = false;

        await using var enumerator = chatStreamModel
            .StreamTextAsync(prompt.SystemInstruction, prompt.UserPrompt, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            string chunk;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                chunk = enumerator.Current;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception)
            {
                failed = true;
                break;
            }

            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            complete.Append(chunk);
            yield return new AskDeltaEvent(chunk);
        }

        if (failed)
        {
            if (complete.Length == 0)
            {
                yield return new AskDeltaEvent(ProviderErrorAnswer);
            }

            yield return new AskDoneEvent(false, "provider_unavailable", DisclaimerText.Value);
            yield break;
        }

        if (complete.Length == 0)
        {
            yield return new AskDeltaEvent(ProviderErrorAnswer);
            yield return new AskDoneEvent(false, "empty_answer", DisclaimerText.Value);
            yield break;
        }

        foreach (var citation in BuildCitations(chunks))
        {
            yield return new AskCitationEvent(citation);
        }

        yield return new AskDoneEvent(false, string.Empty, DisclaimerText.Value);
    }

    private static string BuildRetrievalQuery(string question, IReadOnlyList<ConversationTurn> history)
    {
        // Câu hỏi nối tiếp ngắn ("còn 18:6 thì sao?") cần ngữ cảnh câu trước để
        // truy xuất đúng đoạn sách.
        if (question.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 6)
        {
            return question;
        }

        var previous = history
            .LastOrDefault(turn => string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase));
        return previous is null ? question : $"{previous.Content} {question}";
    }

    private static IEnumerable<Citation> BuildCitations(IReadOnlyList<RetrievedChunk> chunks)
    {
        var seen = new HashSet<(int, int)>();

        foreach (var item in chunks)
        {
            var chunk = item.Chunk;
            if (!seen.Add((chunk.ChapterNumber, chunk.BookPageFrom)))
            {
                continue;
            }

            yield return new Citation
            {
                ChapterNumber = chunk.ChapterNumber,
                ChapterTitle = chunk.ChapterTitle ?? string.Empty,
                BookPage = chunk.BookPageFrom,
                PdfPage = chunk.PdfPageFrom,
                Quote = Excerpt(chunk.Content),
                Verified = chunk.Verified,
                SourceFile = chunk.SourceFile ?? string.Empty,
            };

            if (seen.Count >= MaxCitationCount)
            {
                yield break;
            }
        }
    }

    private static string Excerpt(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Length <= CitationQuoteLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, CitationQuoteLength).TrimEnd(), "…");
    }

    private static class MetaIntent
    {
        private static readonly string[] Greetings = ["chào", "chao", "hello", "hi", "hey", "alo"];
        private static readonly string[] Thanks = ["cảm ơn", "cam on", "thanks", "thank you", "tks"];
        private static readonly string[] Capability =
            ["bạn là ai", "ban la ai", "bạn làm được gì", "giúp được gì", "hướng dẫn dùng"];

        public static bool TryGet(string question, out string answer)
        {
            var normalized = question.Trim().ToLowerInvariant();

            if (Capability.Any(normalized.Contains))
            {
                answer = CapabilityAnswer;
                return true;
            }

            if (Greetings.Any(greeting => normalized.StartsWith(greeting, StringComparison.Ordinal)))
            {
                answer =
                    "Chào bạn! Mình là trợ lý đọc sách của Longevia. Bạn muốn hỏi điều gì về "
                    + "dinh dưỡng, nhịn ăn, vận động hay thói quen sống khỏe trong sách?";
                return true;
            }

            if (Thanks.Any(normalized.Contains))
            {
                answer = "Rất vui được giúp bạn. Bạn cần tra thêm điều gì trong sách cứ hỏi mình nhé.";
                return true;
            }

            answer = string.Empty;
            return false;
        }
    }
}
