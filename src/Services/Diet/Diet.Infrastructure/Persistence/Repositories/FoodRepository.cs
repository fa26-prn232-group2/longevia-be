using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using Longevia.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence.Repositories;

public sealed class FoodRepository(DietDbContext db) : IFoodRepository
{
    public async Task<PagedResult<FoodItem>> SearchAsync(Guid userId, string? search, string? category,
        PaginationQuery pagination, CancellationToken ct)
    {
        var page = pagination.Normalized();
        var query = db.FoodItems.AsNoTracking().Where(x => x.UserId == null || x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category.Trim());
        }

        var total = await query.LongCountAsync(ct);
        var skip = ((long)page.Page - 1) * page.PageSize;
        var items = skip > int.MaxValue ? [] : await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((int)skip).Take(page.PageSize).ToListAsync(ct);
        return new(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<FoodItem>> GetVisibleAsync(Guid userId,
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.FoodItems.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && (x.UserId == null || x.UserId == userId)).ToListAsync(ct);

    public void Add(FoodItem food) => db.FoodItems.Add(food);
}
