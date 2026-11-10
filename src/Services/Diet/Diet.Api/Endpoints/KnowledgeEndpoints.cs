using System.Text.Json;
using Diet.Application.Features.Knowledge;

namespace Diet.Api.Endpoints;

public static class KnowledgeEndpoints
{
    private static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web);

    public static void MapKnowledgeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/knowledge").WithTags("Knowledge").RequireAuthorization();
        group.MapPost("/ask", async (AskKnowledgeRequest request, KnowledgeUseCases useCases,
            CancellationToken ct) => TypedResults.Ok(await useCases.AskAsync(request, ct)))
            .WithName("AskLongevityKnowledge").WithSummary("Hỏi tri thức sách có trích dẫn");
        group.MapGet("/search", async (string query, int? chapterFilter, int? topK,
            KnowledgeUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.SearchAsync(query, chapterFilter, topK, ct)))
            .WithName("SearchLongevityKnowledge").WithSummary("Tra cứu đoạn tri thức sách");

        group.MapPost("/ask/stream", AskStream)
            .WithName("AskLongevityKnowledgeStream")
            .WithSummary("Hỏi sách dạng chat, stream câu trả lời qua SSE");

        group.MapGet("/conversations", async (KnowledgeConversationUseCases useCases,
            CancellationToken ct) => TypedResults.Ok(await useCases.ListAsync(ct)))
            .WithName("ListKnowledgeConversations").WithSummary("Danh sách hội thoại hỏi sách");

        group.MapGet("/conversations/{id:guid}", async (Guid id, KnowledgeConversationUseCases useCases,
            CancellationToken ct) => TypedResults.Ok(await useCases.GetAsync(id, ct)))
            .WithName("GetKnowledgeConversation").WithSummary("Chi tiết một hội thoại hỏi sách");

        group.MapDelete("/conversations/{id:guid}", async (Guid id, KnowledgeConversationUseCases useCases,
            CancellationToken ct) =>
        {
            await useCases.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        })
            .WithName("DeleteKnowledgeConversation").WithSummary("Xoá hội thoại hỏi sách");
    }

    private static async Task AskStream(AskKnowledgeStreamRequest request, HttpContext http,
        KnowledgeConversationUseCases useCases, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var question = request.Question?.Trim() ?? string.Empty;
        if (question.Length is < 1 or > 500)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new
            {
                code = "diet.invalid_question",
                detail = "question phải có 1..500 ký tự.",
            }, ct);
            return;
        }

        if (request.ChapterFilter is < 0)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new
            {
                code = "diet.invalid_chapter_filter",
                detail = "chapterFilter không hợp lệ.",
            }, ct);
            return;
        }

        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache, no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        await http.Response.Body.FlushAsync(ct);

        try
        {
            await foreach (var chunk in useCases.AskStreamAsync(request.ConversationId, question,
                               request.ChapterFilter, ct).ConfigureAwait(false))
            {
                switch (chunk)
                {
                    case KnowledgeStreamDelta delta:
                        await WriteEvent(http, "delta", new { text = delta.Text }, ct);
                        break;
                    case KnowledgeStreamCitation citation:
                        await WriteEvent(http, "citation", citation.Citation, ct);
                        break;
                    case KnowledgeStreamDone done:
                        await WriteEvent(http, "done", new
                        {
                            conversationId = done.ConversationId,
                            messageId = done.MessageId,
                            abstained = done.Abstained,
                            abstainReason = done.AbstainReason,
                            disclaimer = done.Disclaimer,
                        }, ct);
                        break;
                    case KnowledgeStreamError error:
                        await WriteEvent(http, "error", new
                        {
                            code = error.Code,
                            message = error.Message,
                        }, ct);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client đóng kết nối: dừng im lặng, không ghi thêm vào response.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger("KnowledgeSse")
                .LogError(ex, "Stream hỏi sách thất bại sau khi đã mở SSE.");
            await WriteEvent(http, "error", new
            {
                code = "diet.ai_stream_failed",
                message = "Không thể hoàn tất câu trả lời. Bạn thử lại sau giây lát.",
            }, CancellationToken.None);
        }
    }

    private static async Task WriteEvent(HttpContext http, string eventName, object payload,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SseJson);
        await http.Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
}
