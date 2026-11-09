using System.Globalization;
using Microsoft.Extensions.Logging;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Common.Errors;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Pipeline của <c>GenerateMealPlan</c> (FR-013..FR-025): validate → lọc candidate
/// → chia ngân sách → truy xuất chương 4 + Phụ lục A (+ chương 11 cho suggestions)
/// → áp rule 105% → AI chọn <c>food_index</c> → parse/validate ±10% → fallback xác
/// định → suggestions và metadata. Service chỉ trả kế hoạch, không lưu dữ liệu
/// người dùng.
/// </summary>
public sealed class GenerateMealPlanUseCase(
    PlanRequestValidator requestValidator,
    MealPlanCandidateFilter candidateFilter,
    WeeklyBudgetSplitter budgetSplitter,
    MealPlanPromptBuilder promptBuilder,
    MealPlanSelectionParser parser,
    MealPlanValidator validator,
    MealPlanFallbackSelector fallbackSelector,
    MealPlanSuggestionBuilder suggestionBuilder,
    IKnowledgeStore knowledgeStore,
    IChatModel chatModel,
    ILogger<GenerateMealPlanUseCase> logger)
{
    private const int ContextTopK = 8;
    private const int MaxCandidatesPerDayPrompt = 40;

    // Truy vấn hẹp, chỉ nhắm đoạn nước/trà thảo mộc không đường của chương 11
    // dùng cho suggestions ngày chạm trần (FR-023).
    private const string SuggestionQuery = "nước trà thảo mộc không đường";

    /// <summary>Chạy toàn bộ pipeline cho một lời gọi.</summary>
    public async Task<GenerateMealPlanResult> ExecuteAsync(
        GenerateMealPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        requestValidator.Validate(request);

        var warnings = new List<PlanWarning>();
        if (!PlanRequestValidator.IsSupportedLocale(request.Locale))
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.UnsupportedLocale,
                Message = "Locale không được hỗ trợ nên nội dung dùng tiếng Việt (vi-VN).",
            });
        }

        var filtered = candidateFilter.Filter(request);
        warnings.AddRange(filtered.Warnings);

        var candidates = filtered.Candidates;
        if (candidates.Count == 0)
        {
            throw RecommendationErrors.AllCandidatesExcluded();
        }

        var budget = budgetSplitter.Split(request);
        var dates = budget.Days.Select(day => day.Date).ToList();
        var cappedDates = ResolveCappedDates(request, dates, warnings);

        var chunks = await RetrieveAsync(request, cancellationToken).ConfigureAwait(false);
        if (chunks.Count == 0)
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.CorpusNotLoaded,
                Message = "Kho tri thức sách chưa có đoạn phù hợp nên citations để rỗng.",
            });
        }

        var usedFallback = false;
        var fallbackReason = string.Empty;
        var selectedDays = new List<MealPlanDay>();

        // Prompt gọn cho từng ngày: giới hạn candidate vài chục món để model dễ
        // chọn đúng ngân sách ±10%, thay vì một lời gọi cho cả tuần.
        var dayCandidates = candidates
            .OrderBy(food => MealPlanCandidateFilter.MacroFitScore(food, request))
            .ThenBy(food => food.FoodId, StringComparer.Ordinal)
            .Take(MaxCandidatesPerDayPrompt)
            .ToList();

        foreach (var budgetDay in budget.Days)
        {
            if (cappedDates.Contains(budgetDay.Date))
            {
                continue;
            }

            var selection = await SelectDayAsync(
                    request,
                    budgetDay,
                    dayCandidates,
                    chunks,
                    cancellationToken)
                .ConfigureAwait(false);

            if (selection.Days is { Count: > 0 } day)
            {
                selectedDays.AddRange(day);
                foreach (var macroWarning in selection.MacroWarnings)
                {
                    warnings.Add(new PlanWarning
                    {
                        Code = PlanWarningCodes.MacroTargetMissed,
                        Message = macroWarning,
                        Scope = budgetDay.Date,
                    });
                }

                continue;
            }

            var dayBudget = new WeeklyBudget { Days = [budgetDay] };
            var fallback = fallbackSelector.Select(request, dayBudget, candidates, cappedDates);
            selectedDays.AddRange(fallback.Days);
            usedFallback = true;
            fallbackReason = string.IsNullOrEmpty(fallbackReason)
                ? selection.Reason ?? "invalid_model_output"
                : fallbackReason;

            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.AiFallbackUsed,
                Message = $"Ngày {budgetDay.Date}: không dùng được kết quả AI nên đã chọn phương án dự phòng cho riêng ngày này.",
                Scope = budgetDay.Date,
            });

            if (fallback.TargetNotMet)
            {
                warnings.Add(new PlanWarning
                {
                    Code = PlanWarningCodes.TargetNotMet,
                    Message = $"Danh mục ứng viên không đủ để đạt ±10% mục tiêu ngày {budgetDay.Date}; đây là phương án gần nhất.",
                    Scope = budgetDay.Date,
                });
            }
        }

        var days = MergeDays(budget, cappedDates, selectedDays, chunks, warnings);
        AddFiberWarnings(request, days, warnings);
        var citations = CollectCitations(days);

        return new GenerateMealPlanResult
        {
            Days = days,
            WeekStartDate = request.WeekStartDate,
            DaysRequested = request.Days,
            WeeklyTargetCalories = request.TargetCalories * request.Days,
            WeeklyTargetProteinGrams = request.TargetProteinGrams * request.Days,
            WeeklyTargetCarbsGrams = request.TargetCarbsGrams * request.Days,
            WeeklyTargetFatGrams = request.TargetFatGrams * request.Days,
            WeeklyTargetFiberGrams = request.TargetFiberGrams * request.Days,
            Citations = citations,
            Warnings = Deduplicate(warnings),
            Disclaimer = DisclaimerText.Value,
            UsedFallback = usedFallback,
            UsedFallbackReason = fallbackReason,
        };
    }

    private async Task<(IReadOnlyList<MealPlanDay>? Days, string? Reason, IReadOnlyList<string> MacroWarnings)>
        SelectDayAsync(
        GenerateMealPlanRequest request,
        WeeklyBudget.DayBudget budgetDay,
        IReadOnlyList<CandidateFood> candidates,
        IReadOnlyList<RetrievedChunk> chunks,
        CancellationToken cancellationToken)
    {
        var dayBudget = new WeeklyBudget { Days = [budgetDay] };
        var dayRequest = request with { WeekStartDate = budgetDay.Date, Days = 1 };
        var mealChunks = MealChunks(chunks);
        string? feedback = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var prompt = promptBuilder.Build(dayRequest, candidates, dayBudget, mealChunks, feedback);

            ChatCompletion completion;
            try
            {
                completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogFallback(budgetDay, "provider_unavailable", null, candidates.Count,
                    prompt.UserPrompt.Length, attempt, exception);
                return (null, "provider_unavailable", []);
            }

            if (completion is not { Abstained: false, Payload: not null })
            {
                var reason = completion.AbstainReason ?? "provider_unavailable";
                LogFallback(budgetDay, reason, completion.AbstainReason, candidates.Count,
                    prompt.UserPrompt.Length, attempt, null);
                return (null, reason, []);
            }

            var parsed = parser.Parse(completion.Payload, candidates, mealChunks, dayBudget, null);
            var scaled = ScaleToBudget(parsed, budgetDay.TargetCalories, out var scaleReason);
            if (scaled is null)
            {
                if (attempt < 2)
                {
                    feedback = $"Tổng calo của lần chọn trước không thể điều chỉnh khẩu phần ({scaleReason}). Hãy chọn lại món/khẩu phần để tổng calo gần {budgetDay.TargetCalories} kcal hơn.";
                    continue;
                }

                LogFallback(budgetDay, "invalid_model_output", scaleReason, candidates.Count,
                    prompt.UserPrompt.Length, attempt, null);
                return (null, "invalid_model_output", []);
            }

            var validation = validator.Validate(dayRequest, dayBudget, scaled, candidates);
            if (validation.IsValid)
            {
                return (scaled, null, validation.MacroWarnings);
            }

            if (attempt < 2)
            {
                feedback = BuildFeedback(parsed, budgetDay);
                continue;
            }

            LogFallback(budgetDay, "invalid_model_output", validation.FailureReason,
                candidates.Count, prompt.UserPrompt.Length, attempt, null);
            return (null, "invalid_model_output", []);
        }

        return (null, "invalid_model_output", []);
    }

    /// <summary>
    /// Co giãn khẩu phần của cả ngày theo một hệ số chung để tổng calo khớp mục
    /// tiêu. Chỉ chấp nhận hệ số 0.5–1.8; ngoài khoảng đó coi là model chọn sai
    /// món và để lần chọn sau/fallback xử lý.
    /// </summary>
    private static IReadOnlyList<MealPlanDay>? ScaleToBudget(
        IReadOnlyList<MealPlanDay> days,
        int targetCalories,
        out string? reason)
    {
        reason = null;
        var foods = days.SelectMany(day => day.Meals).SelectMany(meal => meal.Foods).ToList();
        var total = foods.Sum(food => food.Calories);
        if (total <= 0)
        {
            reason = "không có món";
            return null;
        }

        var factor = (double)targetCalories / total;
        if (factor is < 0.5 or > 1.8)
        {
            reason = string.Create(CultureInfo.InvariantCulture,
                $"tổng {total} kcal so với mục tiêu {targetCalories} kcal cần hệ số {factor:0.00}×");
            return null;
        }

        if (Math.Abs(factor - 1) < 0.02)
        {
            return days;
        }

        return days
            .Select(day =>
            {
                var meals = day.Meals.Select(meal => meal with
                {
                    Foods = meal.Foods.Select(food => food with
                    {
                        ServingSize = ScaleServingSize(food.ServingSize, factor),
                        Calories = (int)Math.Round(food.Calories * factor,
                            MidpointRounding.AwayFromZero),
                        ProteinGrams = Math.Round(food.ProteinGrams * factor, 1),
                        CarbsGrams = Math.Round(food.CarbsGrams * factor, 1),
                        FatGrams = Math.Round(food.FatGrams * factor, 1),
                        FiberGrams = Math.Round(food.FiberGrams * factor, 1),
                    }).ToList(),
                }).ToList();

                var scaledFoods = meals.SelectMany(meal => meal.Foods).ToList();
                return day with
                {
                    Meals = meals,
                    TotalCalories = scaledFoods.Sum(food => food.Calories),
                    TotalProteinGrams = Math.Round(scaledFoods.Sum(food => food.ProteinGrams), 1),
                    TotalCarbsGrams = Math.Round(scaledFoods.Sum(food => food.CarbsGrams), 1),
                    TotalFatGrams = Math.Round(scaledFoods.Sum(food => food.FatGrams), 1),
                    TotalFiberGrams = Math.Round(scaledFoods.Sum(food => food.FiberGrams), 1),
                };
            })
            .ToList();
    }

    private static string ScaleServingSize(string servingSize, double factor) =>
        double.TryParse(servingSize, NumberStyles.Number, CultureInfo.InvariantCulture, out var size)
            ? (size * factor).ToString("0.#", CultureInfo.InvariantCulture)
            : servingSize;

    /// <summary>
    /// Tổng hiện tại so với mục tiêu để model tự sửa ở lần chọn thứ hai, thay vì
    /// nới lỏng gate ±10% (FR-019).
    /// </summary>
    private static string BuildFeedback(IReadOnlyList<MealPlanDay> parsed,
        WeeklyBudget.DayBudget budgetDay)
    {
        var foods = parsed.SelectMany(day => day.Meals).SelectMany(meal => meal.Foods).ToList();
        var calories = foods.Sum(food => food.Calories);
        var protein = foods.Sum(food => food.ProteinGrams);
        var carbs = foods.Sum(food => food.CarbsGrams);
        var fat = foods.Sum(food => food.FatGrams);

        return FormattableString.Invariant(
            $"Lần trước: {calories} kcal, protein {protein:0.#} g, carb {carbs:0.#} g, chất béo {fat:0.#} g. Mục tiêu ngày: {budgetDay.TargetCalories} kcal, protein {budgetDay.TargetProteinGrams:0.#} g, carb {budgetDay.TargetCarbsGrams:0.#} g, chất béo {budgetDay.TargetFatGrams:0.#} g. Chọn lại trong cùng danh sách ứng viên sao cho calo nằm trong ±10% mục tiêu; nếu calo thấp hơn hãy thêm món, nếu cao hơn hãy bớt món hoặc chọn món ít calo hơn. Được phép dùng lại một món ở bữa khác.");
    }

    private void LogFallback(
        WeeklyBudget.DayBudget day,
        string reason,
        string? detail,
        int candidateCount,
        int promptChars,
        int attempt,
        Exception? exception)
    {
        logger.LogWarning(exception,
            "Meal plan day {Date} dùng fallback: reason={Reason} detail={Detail} candidates={Candidates} promptChars={PromptChars} attempt={Attempt} traceId={TraceId}",
            day.Date,
            reason,
            detail ?? string.Empty,
            candidateCount,
            promptChars,
            attempt,
            System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty);
    }

    private static HashSet<string> ResolveCappedDates(
        GenerateMealPlanRequest request,
        IReadOnlyList<string> dates,
        List<PlanWarning> warnings)
    {
        var capped = new HashSet<string>(StringComparer.Ordinal);

        if (request.ConsumedCalories <= 0 || string.IsNullOrWhiteSpace(request.ConsumedDate))
        {
            return capped;
        }

        if (!dates.Contains(request.ConsumedDate, StringComparer.Ordinal))
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.ConsumedDateOutsidePlan,
                Message = "Ngày đã ăn calo nằm ngoài tuần của kế hoạch nên bỏ qua rule chạm trần 105%.",
            });

            return capped;
        }

        if (request.ConsumedCalories >= request.TargetCalories * PlanDefaults.CalorieCapRatio)
        {
            capped.Add(request.ConsumedDate);
        }

        return capped;
    }

    private async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        GenerateMealPlanRequest request,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(request);

        var chapterFour = await knowledgeStore.SearchAsync(
                new KnowledgeQuery { Query = query, ChapterNumber = 4, TopK = ContextTopK },
                cancellationToken)
            .ConfigureAwait(false);

        var chapterTwelve = await knowledgeStore.SearchAsync(
                new KnowledgeQuery { Query = query, ChapterNumber = 12, TopK = ContextTopK },
                cancellationToken)
            .ConfigureAwait(false);

        var chapterEleven = await knowledgeStore.SearchAsync(
                new KnowledgeQuery
                {
                    Query = SuggestionQuery,
                    ChapterNumber = MealPlanSuggestionBuilder.SuggestionChapterNumber,
                    TopK = ContextTopK,
                },
                cancellationToken)
            .ConfigureAwait(false);

        var allowed = new List<RetrievedChunk>(
            chapterFour.Count + chapterTwelve.Count + chapterEleven.Count);
        allowed.AddRange(chapterFour.Where(chunk => chunk.Chunk.ChapterNumber == 4));
        allowed.AddRange(chapterTwelve.Where(
            chunk => chunk.Chunk.ChapterNumber == MealPlanSuggestionBuilder.AppendixChapterNumber
                && chunk.Chunk.BookPageFrom >= MealPlanSuggestionBuilder.AppendixMinBookPage));

        // Chương 11 chỉ bổ sung nguồn cho suggestions, và chỉ khi đoạn văn nói về
        // nước/trà thảo mộc không đường; phần này nằm sau các chunk chọn món để
        // chunk_index trong prompt không đổi.
        allowed.AddRange(chapterEleven.Where(
            chunk => chunk.Chunk.ChapterNumber == MealPlanSuggestionBuilder.SuggestionChapterNumber
                && MealPlanSuggestionBuilder.IsSuggestionTopic(chunk.Chunk.Content)));

        return allowed;
    }

    private static IReadOnlyList<RetrievedChunk> MealChunks(IReadOnlyList<RetrievedChunk> chunks) =>
        chunks
            .Where(chunk => MealPlanSuggestionBuilder.IsAllowedSource(chunk.Chunk))
            .ToList();

    private static string BuildQuery(GenerateMealPlanRequest request) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"thực đơn chế độ ăn trường thọ {request.Profile.DietaryPreference} {request.Profile.ActivityLevel}");

    private IReadOnlyList<MealPlanDay> MergeDays(
        WeeklyBudget budget,
        IReadOnlySet<string> cappedDates,
        IReadOnlyList<MealPlanDay> selectedDays,
        IReadOnlyList<RetrievedChunk> chunks,
        List<PlanWarning> warnings)
    {
        var selectedByDate = selectedDays
            .GroupBy(day => day.Date, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var days = new List<MealPlanDay>(budget.Days.Count);

        foreach (var budgetDay in budget.Days)
        {
            if (cappedDates.Contains(budgetDay.Date))
            {
                var suggestion = suggestionBuilder.Build(budgetDay.Date, chunks);
                warnings.AddRange(suggestion.Warnings);

                days.Add(new MealPlanDay
                {
                    Date = budgetDay.Date,
                    TargetCalories = budgetDay.TargetCalories,
                    TargetProteinGrams = budgetDay.TargetProteinGrams,
                    TargetCarbsGrams = budgetDay.TargetCarbsGrams,
                    TargetFatGrams = budgetDay.TargetFatGrams,
                    TargetFiberGrams = budgetDay.TargetFiberGrams,
                    TotalCalories = 0,
                    TotalProteinGrams = 0,
                    TotalCarbsGrams = 0,
                    TotalFatGrams = 0,
                    TotalFiberGrams = 0,
                    Meals = [],
                    Suggestions = suggestion.Suggestions,
                    CalorieCapReached = true,
                });

                continue;
            }

            if (selectedByDate.TryGetValue(budgetDay.Date, out var selected))
            {
                days.Add(selected);
            }
        }

        return days;
    }

    private static void AddFiberWarnings(
        GenerateMealPlanRequest request,
        IReadOnlyList<MealPlanDay> days,
        List<PlanWarning> warnings)
    {
        if (request.TargetFiberGrams <= 0)
        {
            return;
        }

        foreach (var day in days)
        {
            if (day.CalorieCapReached || day.TotalFiberGrams >= day.TargetFiberGrams)
            {
                continue;
            }

            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.FiberBelowTarget,
                Message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Tổng chất xơ ngày {day.Date} thấp hơn mục tiêu {day.TargetFiberGrams:0.#} g."),
                Scope = day.Date,
            });
        }
    }

    private static IReadOnlyList<Citation> CollectCitations(IReadOnlyList<MealPlanDay> days) =>
        days
            .SelectMany(day => day.Meals
                .SelectMany(meal => meal.Citations)
                .Concat(day.Suggestions.SelectMany(suggestion => suggestion.Citations)))
            .GroupBy(citation => (citation.ChapterNumber, citation.BookPage, citation.Quote))
            .Select(group => group.First())
            .ToList();

    private static IReadOnlyList<PlanWarning> Deduplicate(List<PlanWarning> warnings) =>
        warnings
            .GroupBy(warning => (warning.Code, warning.Scope))
            .Select(group => group.First())
            .ToList();
}
