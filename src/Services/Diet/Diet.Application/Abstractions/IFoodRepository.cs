using Diet.Domain.Entities;
using Longevia.Shared.Pagination;

namespace Diet.Application.Abstractions;

public interface IFoodRepository
{
    Task<PagedResult<FoodItem>> SearchAsync(Guid userId, string? search, string? category,
        PaginationQuery pagination, CancellationToken ct);
    Task<IReadOnlyList<FoodItem>> GetVisibleAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<FoodItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<FoodItem>> GetRecommendationCandidatesAsync(Guid userId,
        IReadOnlyList<string> allergies, CancellationToken ct);
    void Add(FoodItem food);
}
