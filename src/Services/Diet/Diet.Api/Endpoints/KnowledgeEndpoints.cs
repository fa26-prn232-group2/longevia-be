using Diet.Application.Features.Knowledge;

namespace Diet.Api.Endpoints;

public static class KnowledgeEndpoints
{
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
    }
}
