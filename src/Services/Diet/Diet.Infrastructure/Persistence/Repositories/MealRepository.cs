using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence.Repositories;

public sealed class MealRepository(DietDbContext db) : IMealRepository
{
    public void Add(MealLog meal) => db.MealLogs.Add(meal);

    public async Task<IReadOnlyList<MealLog>> GetDailyAsync(Guid userId, DateOnly date, CancellationToken ct) =>
        await db.MealLogs.AsNoTracking().Include(x => x.Items)
            .Where(x => x.UserId == userId && x.Date == date).ToListAsync(ct);

    /// <summary>Bản có tracking để xoá; dùng riêng cho luồng xoá bữa ăn.</summary>
    public async Task<IReadOnlyList<MealLog>> GetDailyTrackedAsync(Guid userId, DateOnly date,
        CancellationToken ct) =>
        await db.MealLogs.Include(x => x.Items)
            .Where(x => x.UserId == userId && x.Date == date).ToListAsync(ct);

    public void RemoveRange(IEnumerable<MealLog> meals) => db.MealLogs.RemoveRange(meals);

    public async Task<IReadOnlyList<MealLog>> GetRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.MealLogs.AsNoTracking().Include(x => x.Items)
            .Where(x => x.UserId == userId && x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Date).ToListAsync(ct);
}
