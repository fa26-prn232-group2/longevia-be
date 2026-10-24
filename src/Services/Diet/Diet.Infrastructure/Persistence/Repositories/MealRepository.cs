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
}
