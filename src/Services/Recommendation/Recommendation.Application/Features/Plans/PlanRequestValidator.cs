using System.Globalization;
using Recommendation.Application.Common.Errors;
using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Plans;

/// <summary>
/// Kiểm tra đầu vào của hai RPC plan (FR-038..FR-047) trước khi use case chạy
/// pipeline. Mọi lỗi trả <see cref="Common.Errors.RecommendationErrors"/> để tầng
/// Api map sang gRPC với mã ổn định; validator không tự điền giá trị ngoài các
/// mặc định đã chốt (<c>sessions = 0</c>, <c>minutes = 0</c>).
/// </summary>
public sealed class PlanRequestValidator
{
    /// <summary>Locale duy nhất được hỗ trợ (FR-050).</summary>
    public const string SupportedLocale = "vi-VN";

    private static readonly HashSet<string> Sexes = new(StringComparer.Ordinal)
    {
        "MALE",
        "FEMALE",
    };

    private static readonly HashSet<string> ActivityLevels = new(StringComparer.Ordinal)
    {
        "Sedentary",
        "LightlyActive",
        "ModeratelyActive",
        "VeryActive",
    };

    private static readonly HashSet<string> DietaryPreferences = new(StringComparer.Ordinal)
    {
        "None",
        "Keto",
        "Vegan",
        "Mediterranean",
        "LowCarb",
    };

    private static readonly HashSet<string> Goals = new(StringComparer.Ordinal)
    {
        "LOSE_WEIGHT",
        "MAINTAIN",
        "BUILD_MUSCLE",
        "LONGEVITY",
    };

    /// <summary>Kiểm tra toàn bộ đầu vào của <c>GenerateMealPlan</c>.</summary>
    public void Validate(GenerateMealPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateProfile(request.Profile);
        ValidateMealTargets(request);
        ValidateWeekStart(request.WeekStartDate);
        ValidateDays(request.Days);
        ValidateMealsPerDay(request.MealsPerDay);
        ValidateCandidateFoods(request.CandidateFoods);
        ValidateConsumedCalories(request.ConsumedCalories, request.ConsumedDate);
        _ = ResolveLocale(request.Locale);
    }

    /// <summary>Kiểm tra toàn bộ đầu vào của <c>GenerateWorkoutPlan</c>.</summary>
    public void Validate(GenerateWorkoutPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateProfile(request.Profile);
        ValidateGoal(request.Goal);
        ValidateWeekStart(request.WeekStartDate);
        ValidateDays(request.Days);
        ValidateWorkoutParameters(request.SessionsPerWeek, request.MinutesPerSession, request.Days);
        _ = ResolveLocale(request.Locale);
    }

    /// <summary>
    /// Số buổi thực tế sau khi áp mặc định: <c>0</c> → <c>min(3, days)</c>
    /// (FR-041). Giá trị khác được giữ nguyên sau khi validator đã kiểm tra.
    /// </summary>
    public int ResolveSessionsPerWeek(int sessionsPerWeek, int days) =>
        sessionsPerWeek == 0 ? Math.Min(PlanDefaults.DefaultSessionsPerWeek, days) : sessionsPerWeek;

    /// <summary>Số phút thực tế sau khi áp mặc định: <c>0</c> → 30 (FR-041).</summary>
    public int ResolveMinutesPerSession(int minutesPerSession) =>
        minutesPerSession == 0 ? PlanDefaults.DefaultMinutesPerSession : minutesPerSession;

    /// <summary>
    /// Locale dùng để dựng text; mọi giá trị không phải <c>vi-VN</c> đều quay về
    /// <c>vi-VN</c> kèm warning ở use case (FR-050), không phải lỗi.
    /// </summary>
    public static string ResolveLocale(string? locale) => SupportedLocale;

    /// <summary><c>true</c> khi locale đúng là <c>vi-VN</c>.</summary>
    public static bool IsSupportedLocale(string? locale) =>
        string.Equals(locale, SupportedLocale, StringComparison.Ordinal);

    /// <summary>Đọc ngày ISO <c>YYYY-MM-DD</c> theo đúng định dạng contract.</summary>
    public static bool TryParseIsoDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static void ValidateProfile(UserProfile? profile)
    {
        if (profile is null
            || profile.Age is < 1 or > 120
            || !Sexes.Contains(profile.Sex)
            || profile.HeightCm <= 0
            || profile.WeightKg <= 0
            || !ActivityLevels.Contains(profile.ActivityLevel)
            || !DietaryPreferences.Contains(profile.DietaryPreference))
        {
            throw RecommendationErrors.InvalidProfile();
        }
    }

    private static void ValidateMealTargets(GenerateMealPlanRequest request)
    {
        if (request.TargetCalories < 0
            || request.TargetProteinGrams < 0
            || request.TargetCarbsGrams < 0
            || request.TargetFatGrams < 0
            || request.TargetFiberGrams < 0)
        {
            throw RecommendationErrors.InvalidTargets();
        }

        if (request.TargetCalories == 0
            || (request.TargetProteinGrams == 0
                && request.TargetCarbsGrams == 0
                && request.TargetFatGrams == 0))
        {
            throw RecommendationErrors.MissingTargets();
        }
    }

    private static void ValidateGoal(string? goal)
    {
        if (!Goals.Contains(goal ?? string.Empty))
        {
            throw RecommendationErrors.InvalidGoal();
        }
    }

    private static void ValidateWeekStart(string? weekStartDate)
    {
        if (!TryParseIsoDate(weekStartDate, out var date) || date.DayOfWeek != DayOfWeek.Monday)
        {
            throw RecommendationErrors.InvalidWeekStart();
        }
    }

    private static void ValidateDays(int days)
    {
        if (days is < PlanDefaults.MinDays or > PlanDefaults.MaxDays)
        {
            throw RecommendationErrors.InvalidDays();
        }
    }

    private static void ValidateMealsPerDay(int mealsPerDay)
    {
        if (mealsPerDay is < PlanDefaults.MinMealsPerDay or > PlanDefaults.MaxMealsPerDay)
        {
            throw RecommendationErrors.InvalidMealsPerDay();
        }
    }

    private static void ValidateCandidateFoods(IReadOnlyList<CandidateFood>? candidateFoods)
    {
        if (candidateFoods is null || candidateFoods.Count == 0)
        {
            throw RecommendationErrors.NoCandidateFood();
        }

        foreach (var food in candidateFoods)
        {
            if (string.IsNullOrWhiteSpace(food.FoodId)
                || string.IsNullOrWhiteSpace(food.Name)
                || food.Calories < 0
                || food.ProteinGrams < 0
                || food.CarbsGrams < 0
                || food.FatGrams < 0
                || food.FiberGrams < 0)
            {
                throw RecommendationErrors.InvalidCandidateFood();
            }
        }
    }

    private static void ValidateConsumedCalories(int consumedCalories, string? consumedDate)
    {
        if (consumedCalories < 0)
        {
            throw RecommendationErrors.InvalidConsumedCalories();
        }

        if (string.IsNullOrWhiteSpace(consumedDate))
        {
            if (consumedCalories > 0)
            {
                throw RecommendationErrors.MissingConsumedDate();
            }

            return;
        }

        if (!TryParseIsoDate(consumedDate, out _))
        {
            throw RecommendationErrors.InvalidConsumedDate();
        }
    }

    private static void ValidateWorkoutParameters(int sessionsPerWeek, int minutesPerSession, int days)
    {
        if (sessionsPerWeek < 0
            || sessionsPerWeek > PlanDefaults.MaxDays
            || sessionsPerWeek > days)
        {
            throw RecommendationErrors.InvalidWorkoutParams();
        }

        if (minutesPerSession < 0
            || (minutesPerSession != 0
                && (minutesPerSession < PlanDefaults.MinMinutesPerSession
                    || minutesPerSession > PlanDefaults.MaxMinutesPerSession)))
        {
            throw RecommendationErrors.InvalidWorkoutParams();
        }
    }
}
