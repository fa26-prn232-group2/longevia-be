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
        var calculation = target.Calculation;
        // One PostgreSQL statement preserves the target ID and handles concurrent first PUTs.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO daily_diet_targets
                ("Id", "UserId", "TargetCalories", "TargetProteinGrams", "TargetCarbsGrams", "TargetFatGrams", "UpdatedAt",
                 "Calculation_WeightKg", "Calculation_WeightMeasuredAt", "Calculation_SourceMetricId",
                 "Calculation_WeightRequestedAsOf", "Calculation_HeightCm", "Calculation_AgeYears",
                 "Calculation_IsMale", "Calculation_ActivityFactor", "Calculation_CalculatedAt", "Calculation_Bmr", "Calculation_Tdee")
            VALUES ({target.Id}, {target.UserId}, {target.TargetCalories}, {target.TargetProteinGrams},
                {target.TargetCarbsGrams}, {target.TargetFatGrams}, {target.UpdatedAt},
                {calculation?.WeightKg}, {calculation?.WeightMeasuredAt},
                {calculation?.SourceMetricId}, {calculation?.WeightRequestedAsOf},
                {calculation?.HeightCm}, {calculation?.AgeYears},
                {calculation?.IsMale}, {calculation?.ActivityFactor},
                {calculation?.CalculatedAt}, {calculation?.Bmr}, {calculation?.Tdee})
            ON CONFLICT ("UserId") DO UPDATE SET
                "TargetCalories" = EXCLUDED."TargetCalories",
                "TargetProteinGrams" = EXCLUDED."TargetProteinGrams",
                "TargetCarbsGrams" = EXCLUDED."TargetCarbsGrams",
                "TargetFatGrams" = EXCLUDED."TargetFatGrams",
                "UpdatedAt" = EXCLUDED."UpdatedAt",
                "Calculation_WeightKg" = EXCLUDED."Calculation_WeightKg",
                "Calculation_WeightMeasuredAt" = EXCLUDED."Calculation_WeightMeasuredAt",
                "Calculation_SourceMetricId" = EXCLUDED."Calculation_SourceMetricId",
                "Calculation_WeightRequestedAsOf" = EXCLUDED."Calculation_WeightRequestedAsOf",
                "Calculation_HeightCm" = EXCLUDED."Calculation_HeightCm",
                "Calculation_AgeYears" = EXCLUDED."Calculation_AgeYears",
                "Calculation_IsMale" = EXCLUDED."Calculation_IsMale",
                "Calculation_ActivityFactor" = EXCLUDED."Calculation_ActivityFactor",
                "Calculation_CalculatedAt" = EXCLUDED."Calculation_CalculatedAt",
                "Calculation_Bmr" = EXCLUDED."Calculation_Bmr",
                "Calculation_Tdee" = EXCLUDED."Calculation_Tdee"
            """, ct);
        return await GetAsync(target.UserId, ct)
            ?? throw new InvalidOperationException("Không đọc lại được mục tiêu vừa lưu.");
    }
}
