using Diet.Application.Features.Foods;
using Longevia.Shared.Pagination;
using Longevia.Shared.Validation;

namespace Diet.Api.Endpoints;

public static class FoodEndpoints
{
    public static void MapFoodEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/foods").WithTags("Foods").RequireAuthorization();
        group.MapGet("", async (string? search, string? category, int? pageNumber, int? pageSize,
            FoodUseCases useCases, CancellationToken ct) =>
            TypedResults.Ok(await useCases.SearchAsync(search, category,
                new PaginationQuery(pageNumber ?? 1, pageSize ?? 20), ct)))
            .WithName("SearchFoods").WithSummary("Tìm món chung và món tùy chỉnh của bạn");
        group.MapPost("", async (CreateFoodRequest request, FoodUseCases useCases, CancellationToken ct) =>
        {
            var result = await useCases.CreateAsync(request, ct);
            return TypedResults.Created($"/api/diet/foods?search={Uri.EscapeDataString(result.Name)}", result);
        }).AddEndpointFilter<ValidationFilter<CreateFoodRequest>>()
            .WithName("CreateFood").WithSummary("Tạo món tùy chỉnh cá nhân");
    }
}
