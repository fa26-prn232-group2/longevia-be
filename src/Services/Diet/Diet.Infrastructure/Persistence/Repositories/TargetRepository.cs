using Diet.Application.Abstractions;
using Diet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Diet.Infrastructure.Persistence.Repositories;

public sealed class TargetRepository(DietDbContext db) : ITargetRepository
{
    public Task<DailyDietTarget?> GetAsync(Guid userId, CancellationToken ct) =>
        db.DailyDietTargets.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<DailyDietTarget> UpsertAsync(DailyDietTarget target, CancellationToken ct)
    {
        // One PostgreSQL statement preserves the target ID and handles concurrent first PUTs.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO daily_diet_targets
                ("Id", "UserId", "TargetCalories", "TargetProteinGrams", "TargetCarbsGrams", "TargetFatGrams", "UpdatedAt")
            VALUES ({target.Id}, {target.UserId}, {target.TargetCalories}, {target.TargetProteinGrams},
                {target.TargetCarbsGrams}, {target.TargetFatGrams}, {target.UpdatedAt})
            ON CONFLICT ("UserId") DO UPDATE SET
                "TargetCalories" = EXCLUDED."TargetCalories",
                "TargetProteinGrams" = EXCLUDED."TargetProteinGrams",
                "TargetCarbsGrams" = EXCLUDED."TargetCarbsGrams",
                "TargetFatGrams" = EXCLUDED."TargetFatGrams",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, ct);
        return await GetAsync(target.UserId, ct)
            ?? throw new InvalidOperationException("Không đọc lại được mục tiêu vừa lưu.");
    }
}
