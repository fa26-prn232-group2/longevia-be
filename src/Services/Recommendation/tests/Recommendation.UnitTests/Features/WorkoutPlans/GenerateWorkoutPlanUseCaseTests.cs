using System.Text.Json.Nodes;
using Longevia.Shared.Errors;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Application.Features.Plans;
using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.WorkoutPlans;

/// <summary>
/// T051 — FR-026..FR-037: happy path, provider abstain/throw → skeleton, corpus
/// rỗng (degraded), limitations rỗng/có, sessions &gt; days, sessions = 0, locale
/// và metadata của <c>GenerateWorkoutPlan</c>.
/// </summary>
public sealed class GenerateWorkoutPlanUseCaseTests
{
    private const string WeekStart = "2026-10-26";

    [Fact]
    public async Task ReturnsSessionsAndRestDaysOnHappyPath()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(7, result.Days.Count);
        Assert.Equal(WeekStart, result.WeekStartDate);
        Assert.Equal(7, result.DaysRequested);
        Assert.Equal(3, result.SessionsScheduled);
        Assert.Equal(30, result.MinutesPerSession);
        Assert.Equal(3, result.Days.Count(day => !day.RestDay));
        Assert.Equal(4, result.Days.Count(day => day.RestDay));
        Assert.False(result.UsedFallback);
        Assert.NotEmpty(result.Citations);
        Assert.All(
            result.Days.Where(day => !day.RestDay),
            day =>
            {
                Assert.Equal("walking", day.Activity);
                Assert.Equal(30, day.DurationMinutes);
                Assert.Empty(day.EquipmentUsed);
                Assert.NotEmpty(day.Citations);
                Assert.False(string.IsNullOrWhiteSpace(day.Intensity));
            });
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.NoLimitationsAssumed);
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Code == PlanWarningCodes.AiFallbackUsed);
        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
    }

    [Fact]
    public async Task FallsBackToSkeletonWhenProviderAbstains()
    {
        var useCase = CreateUseCase(new StubChatModel(null), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.AiFallbackUsed);
        Assert.Equal(3, result.SessionsScheduled);
        Assert.All(result.Days.Where(day => !day.RestDay), day => Assert.NotEmpty(day.Citations));
    }

    [Fact]
    public async Task FallsBackToSkeletonWhenProviderThrowsTransportException()
    {
        var useCase = CreateUseCase(new StubChatModel(null, throwOnCall: true), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.AiFallbackUsed);
        Assert.Equal(3, result.SessionsScheduled);
        Assert.Equal(7, result.Days.Count);
    }

    [Fact]
    public async Task FallsBackWhenSelectionUsesAnActivityOutsideTheCatalog()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection(index: 9)), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.All(result.Days.Where(day => !day.RestDay), day => Assert.Equal("walking", day.Activity));
    }

    [Fact]
    public async Task EmptyLimitationsAssumeGeneralGuidance()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.NoLimitationsAssumed);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == PlanWarningCodes.UnsupportedLimitation);
    }

    [Fact]
    public async Task LimitationsRemoveUnsafeActivityAndKeepRestDays()
    {
        var request = Request() with
        {
            ActivityPreference = ["running"],
            Limitations = ["đau khớp gối"],
        };
        var store = new StubKnowledgeStore([Chunk("Chạy bộ là hoạt động thể chất tốt.", 42)]);
        var useCase = CreateUseCase(new StubChatModel(Selection()), store);

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(0, result.SessionsScheduled);
        Assert.All(result.Days, day => Assert.True(day.RestDay));
        Assert.DoesNotContain(result.Days, day => day.Activity == "running");
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.NoSafeActivity);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == PlanWarningCodes.UnsupportedLimitation);
    }

    [Fact]
    public async Task UnsupportedLimitationIsReportedWithoutMedicalInference()
    {
        var request = Request() with { Limitations = ["bệnh tim"] };
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.UnsupportedLimitation);
        Assert.Equal(3, result.SessionsScheduled);
        Assert.DoesNotContain("bệnh tim", string.Join(' ', result.Days.Select(day => day.Rationale)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyCorpusUsesCallerDataOnly()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore());

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(3, result.SessionsScheduled);
        Assert.Empty(result.Citations);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.CorpusNotLoaded);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.ActivitySourceUnverified);
        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.IntensityUnverified);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == PlanWarningCodes.AiFallbackUsed);
        Assert.All(
            result.Days.Where(day => !day.RestDay),
            day =>
            {
                Assert.Equal("walking", day.Activity);
                Assert.Equal(30, day.DurationMinutes);
                Assert.Empty(day.Citations);
                Assert.True(string.IsNullOrEmpty(day.Intensity));
            });
    }

    [Fact]
    public async Task SessionsGreaterThanDaysReturnsInvalidWorkoutParams()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var exception = await Assert.ThrowsAsync<AppException>(
            () => useCase.ExecuteAsync(Request() with { Days = 2, SessionsPerWeek = 3 }, CancellationToken.None));

        Assert.Equal("recommendation.invalid_workout_params", exception.Code);
    }

    [Fact]
    public async Task ZeroSessionsUseMinOfThreeAndDays()
    {
        var request = Request() with { Days = 2, SessionsPerWeek = 0 };
        var useCase = CreateUseCase(
            new StubChatModel(Selection("2026-10-26", "2026-10-27")),
            new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(2, result.SessionsScheduled);
        Assert.Equal(2, result.Days.Count(day => !day.RestDay));
    }

    [Fact]
    public async Task ZeroMinutesDefaultToThirty()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(
            Request() with { MinutesPerSession = 0 },
            CancellationToken.None);

        Assert.Equal(30, result.MinutesPerSession);
        Assert.All(result.Days.Where(day => !day.RestDay), day => Assert.Equal(30, day.DurationMinutes));
    }

    [Fact]
    public async Task UnsupportedLocaleAddsWarningWithoutChangingThePlan()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request() with { Locale = "en-US" }, CancellationToken.None);

        Assert.Contains(result.Warnings, warning => warning.Code == PlanWarningCodes.UnsupportedLocale);
        Assert.Equal(7, result.Days.Count);
        Assert.Equal(3, result.SessionsScheduled);
    }

    [Fact]
    public async Task ReturnsPlanMetadata()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(WeekStart, result.WeekStartDate);
        Assert.Equal(7, result.DaysRequested);
        Assert.Equal(3, result.SessionsScheduled);
        Assert.Equal(30, result.MinutesPerSession);
        Assert.Equal(
            new[] { "2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29", "2026-10-30", "2026-10-31", "2026-11-01" },
            result.Days.Select(day => day.Date));
    }

    [Fact]
    public async Task RationaleAndDisclaimerContainNoMedicalAdvice()
    {
        var useCase = CreateUseCase(new StubChatModel(Selection()), new StubKnowledgeStore([ChapterFiveChunk()]));

        var result = await useCase.ExecuteAsync(Request(), CancellationToken.None);

        var text = string.Join(
            ' ',
            result.Days.Select(day => day.Rationale).Append(result.Disclaimer));

        Assert.DoesNotContain("chẩn đoán", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("điều trị", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liều thuốc", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kê đơn", text, StringComparison.OrdinalIgnoreCase);
    }

    private static GenerateWorkoutPlanUseCase CreateUseCase(
        IChatModel chatModel,
        IKnowledgeStore knowledgeStore) =>
        new(
            new PlanRequestValidator(),
            new WorkoutActivityCatalogBuilder(new WorkoutLimitationRules()),
            new WorkoutPlanSkeletonBuilder(new PlanRequestValidator(), new PlanDateRange()),
            new WorkoutPlanPromptBuilder(new PlanPromptFormatter()),
            new WorkoutPlanSelectionParser(
                new CitationResolver(),
                new PlanRationaleBuilder(new SafetyGuard())),
            new WorkoutPlanValidator(),
            new PlanRationaleBuilder(new SafetyGuard()),
            knowledgeStore,
            chatModel);

    private static GenerateWorkoutPlanRequest Request() => new()
    {
        Profile = new UserProfile
        {
            Age = 35,
            Sex = "FEMALE",
            HeightCm = 165,
            WeightKg = 60,
            ActivityLevel = "ModeratelyActive",
            DietaryPreference = "None",
            Allergies = [],
        },
        Goal = "LONGEVITY",
        WeekStartDate = WeekStart,
        Days = 7,
        SessionsPerWeek = 3,
        MinutesPerSession = 30,
        ActivityPreference = ["walking"],
        EquipmentAvailable = [],
        Limitations = [],
        Locale = "vi-VN",
    };

    private static RetrievedChunk ChapterFiveChunk() =>
        Chunk("Đi bộ nhanh một giờ mỗi ngày giúp tim mạch khỏe.", 105);

    private static RetrievedChunk Chunk(string content, int bookPage) => new()
    {
        Chunk = new KnowledgeChunk
        {
            CorpusId = Guid.NewGuid(),
            ChapterNumber = WorkoutActivities.SourceChapterNumber,
            ChapterTitle = "TẬP THỂ DỤC VÀ SỐNG LÂU KHỎE MẠNH",
            Content = content,
            BookPageFrom = bookPage,
            BookPageTo = bookPage,
            PdfPageFrom = bookPage - 7,
            PdfPageTo = bookPage - 7,
            Verified = true,
            SourceFile = "05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md",
        },
        Score = 0.9,
    };

    private static ChatCompletion Selection(params string[] dates)
    {
        if (dates.Length == 0)
        {
            dates = ["2026-10-26", "2026-10-28", "2026-10-30"];
        }

        return SelectionForDates(dates, index: 0);
    }

    private static ChatCompletion Selection(int index) =>
        SelectionForDates(["2026-10-26", "2026-10-28", "2026-10-30"], index);

    private static ChatCompletion SelectionForDates(IReadOnlyList<string> dates, int index)
    {
        var days = new JsonArray();
        foreach (var date in dates)
        {
            days.Add(new JsonObject
            {
                ["date"] = date,
                ["activity_index"] = index,
                ["citations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["chunk_index"] = 0,
                        ["quote"] = "Đi bộ nhanh",
                    },
                },
            });
        }

        return new ChatCompletion { Payload = new JsonObject { ["days"] = days } };
    }

    private sealed class StubChatModel : IChatModel
    {
        private readonly ChatCompletion? _completion;
        private readonly bool _throwOnCall;

        public StubChatModel(ChatCompletion? completion = null, bool throwOnCall = false)
        {
            _completion = completion;
            _throwOnCall = throwOnCall;
        }

        public List<StructuredPrompt> Prompts { get; } = [];

        public Task<ChatCompletion> CompleteAsync(StructuredPrompt prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);

            if (_throwOnCall)
            {
                throw new InvalidOperationException("transport failure");
            }

            return Task.FromResult(
                _completion ?? new ChatCompletion { Abstained = true, AbstainReason = "provider_unavailable" });
        }
    }

    private sealed class StubKnowledgeStore(IReadOnlyList<RetrievedChunk>? chunks = null) : IKnowledgeStore
    {
        private readonly IReadOnlyList<RetrievedChunk> _chunks = chunks ?? [];

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken)
        {
            var result = _chunks
                .Where(chunk => query.ChapterNumber == 0 || chunk.Chunk.ChapterNumber == query.ChapterNumber)
                .ToList();

            return Task.FromResult<IReadOnlyList<RetrievedChunk>>(result);
        }

        public Task<int> UpsertAsync(IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int> CountAsync(string corpusKey, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }
}
