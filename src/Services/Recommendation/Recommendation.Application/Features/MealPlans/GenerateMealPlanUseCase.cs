using System.Globalization;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Common.Errors;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.MealPlans;

/// <summary>
/// Pipeline của <c>GenerateMealPlan</c> (FR-013..FR-025): validate → lọc candidate
/// → chia ngân sách → truy xuất chương 4 + Phụ lục A → áp rule 105% → AI chọn
/// <c>food_index</c> → parse/validate ±10% → fallback xác định → suggestions và
/// metadata. Service chỉ trả kế hoạch, không lưu dữ liệu người dùng.
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
    IChatModel chatModel)
{
    private const int ContextTopK = 8;

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
        var selectedDays = await SelectDaysAsync(
                request,
                candidates,
                budget,
                chunks,
                cappedDates,
                cancellationToken)
            .ConfigureAwait(false);

        if (selectedDays is null && cappedDates.Count < dates.Count)
        {
            var fallback = fallbackSelector.Select(request, budget, candidates, cappedDates);
            selectedDays = fallback.Days;
            usedFallback = true;

            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.AiFallbackUsed,
                Message = "Không dùng được kết quả AI nên đã chọn phương án dự phòng xác định từ danh mục ứng viên.",
            });

            if (fallback.TargetNotMet)
            {
                warnings.Add(new PlanWarning
                {
                    Code = PlanWarningCodes.TargetNotMet,
                    Message = "Danh mục ứng viên không đủ để đạt ±10% mục tiêu ngày; kế hoạch là phương án gần nhất.",
                });
            }
        }

        selectedDays ??= [];

        var days = MergeDays(budget, cappedDates, selectedDays, chunks, warnings);
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
        };
    }

    private async Task<IReadOnlyList<MealPlanDay>?> SelectDaysAsync(
        GenerateMealPlanRequest request,
        IReadOnlyList<CandidateFood> candidates,
        WeeklyBudget budget,
        IReadOnlyList<RetrievedChunk> chunks,
        IReadOnlySet<string> cappedDates,
        CancellationToken cancellationToken)
    {
        if (cappedDates.Count == budget.Days.Count)
        {
            return [];
        }

        var prompt = promptBuilder.Build(request, candidates, budget, chunks);

        ChatCompletion completion;
        try
        {
            completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Provider lỗi/timeout: use case vẫn trả fallback thay vì làm hỏng request.
            return null;
        }

        if (completion is not { Abstained: false, Payload: not null })
        {
            return null;
        }

        var parsed = parser.Parse(completion.Payload, candidates, chunks, budget, cappedDates);
        return validator.Validate(request, budget, parsed, candidates, cappedDates).IsValid
            ? parsed
            : null;
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

        var allowed = new List<RetrievedChunk>(chapterFour.Count + chapterTwelve.Count);
        allowed.AddRange(chapterFour.Where(chunk => chunk.Chunk.ChapterNumber == 4));
        allowed.AddRange(chapterTwelve.Where(
            chunk => chunk.Chunk.ChapterNumber == MealPlanSuggestionBuilder.AppendixChapterNumber
                && chunk.Chunk.BookPageFrom >= MealPlanSuggestionBuilder.AppendixMinBookPage));

        return allowed;
    }

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
