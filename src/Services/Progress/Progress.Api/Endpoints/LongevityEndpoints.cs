using Progress.Application.Features.Longevity;

namespace Progress.Api.Endpoints;

public static class LongevityEndpoints
{
    public static void MapLongevityEndpoints(this WebApplication app)
    {
        app.MapGet("/longevity-habits", async (int? days, LongevityUseCase useCase, CancellationToken ct) =>
            TypedResults.Ok(await useCase.ExecuteAsync(days ?? 7, ct)))
            .WithTags("Longevity").RequireAuthorization()
            .WithName("GetLongevityHabits").WithSummary("Điểm và phân rã thói quen trường thọ");
    }
}
