using System.Security.Cryptography;
using System.Text;
using Longevia.Recommendation.Contracts.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;
using Recommendation.Infrastructure.Persistence;
using Recommendation.Infrastructure.Persistence.Entities;
using Recommendation.IntegrationTests.Features.Meals;
using Recommendation.IntegrationTests.Features.Shared;
using UserProfile = Longevia.Recommendation.Contracts.V1.UserProfile;

namespace Recommendation.IntegrationTests.Features.Plans;

/// <summary>
/// T053 — FR-002/FR-003/FR-027/FR-034: citation của workout chỉ thuộc chương 5;
/// quote sai bị loại và kế hoạch chuyển sang skeleton; corpus rỗng thì degraded
/// kèm <c>activity_source_unverified</c>; limitation lạ bị báo warning và không
/// suy diễn y tế; không có căn cứ cường độ thì <c>intensity_unverified</c>.
/// </summary>
public sealed class WorkoutPlanCitationGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>
{
    private const int EmbeddingDimensions = 768;
    private const string Chapter5Content =
        "Đi bộ là hoạt động nhẹ nhàng. Chạy bộ và đạp xe cũng được nhắc trong chương này.";

    [Fact]
    public async Task CitationsBelongOnlyToChapterFive()
    {
        await EnsureChapterFiveAsync(fixture);

        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.NotEmpty(response.Citations);
        Assert.DoesNotContain(response.Warnings, warning => warning.Code == "corpus_not_loaded");
        Assert.All(response.Citations, citation =>
        {
            Assert.Equal(5, citation.ChapterNumber);
            Assert.Contains(citation.Quote, Chapter5Content, StringComparison.Ordinal);
        });
        Assert.All(
            response.Days.Where(day => !day.RestDay),
            day => Assert.NotEmpty(day.Citations));
    }

    [Fact]
    public async Task DropsInvalidCitationAndFallsBackToTheSkeleton()
    {
        await EnsureChapterFiveAsync(fixture);
        fixture.UseInvalidCitationForNextWorkoutPlan();

        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.True(response.UsedFallback);
        Assert.Contains(response.Warnings, warning => warning.Code == "ai_fallback_used");
        Assert.DoesNotContain(
            response.Citations,
            citation => citation.Quote.Contains("không xuất hiện", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            response.Citations,
            citation => Assert.Equal(5, citation.ChapterNumber));
        Assert.Equal(3, response.SessionsScheduled);
    }

    [Fact]
    public async Task FallsBackToSkeletonWhenProviderAbstains()
    {
        await EnsureChapterFiveAsync(fixture);
        fixture.AbstainNextWorkoutPlan();

        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.True(response.UsedFallback);
        Assert.Contains(response.Warnings, warning => warning.Code == "ai_fallback_used");
        Assert.Equal(3, response.SessionsScheduled);
        Assert.All(response.Days.Where(day => !day.RestDay), day => Assert.NotEmpty(day.Citations));
    }

    [Fact]
    public async Task LimitationsExcludeThePreferredActivity()
    {
        await EnsureChapterFiveAsync(fixture);
        var request = BuildRequest();
        request.ActivityPreference.Clear();
        request.ActivityPreference.Add("running");
        request.Limitations.Add("đau khớp gối");

        var response = await fixture.Client.GenerateWorkoutPlanAsync(request);

        Assert.DoesNotContain(response.Days, day => day.Activity == "running");
        Assert.Contains(response.Warnings, warning => warning.Code == "no_safe_activity");
        Assert.DoesNotContain(response.Warnings, warning => warning.Code == "unsupported_limitation");
        Assert.Equal(0, response.SessionsScheduled);
    }

    [Fact]
    public async Task UnsupportedLimitationIsReported()
    {
        await EnsureChapterFiveAsync(fixture);
        var request = BuildRequest();
        request.Limitations.Add("bệnh tim");

        var response = await fixture.Client.GenerateWorkoutPlanAsync(request);

        Assert.Contains(response.Warnings, warning => warning.Code == "unsupported_limitation");
        Assert.Equal(3, response.SessionsScheduled);
    }

    [Fact]
    public async Task IntensityStaysEmptyWhenTheCorpusHasNoEvidence()
    {
        await EnsureChapterFiveAsync(fixture);

        var response = await fixture.Client.GenerateWorkoutPlanAsync(BuildRequest());

        Assert.All(
            response.Days.Where(day => !day.RestDay),
            day => Assert.True(string.IsNullOrEmpty(day.Intensity)));
        Assert.Contains(response.Warnings, warning => warning.Code == "intensity_unverified");
    }

    [Fact]
    public async Task EmptyCorpusReturnsNoCitationsAndDegradedWarnings()
    {
        var empty = new RecommendationApiFixture();
        await empty.InitializeAsync();

        try
        {
            var response = await empty.Client.GenerateWorkoutPlanAsync(BuildRequest());

            Assert.Empty(response.Citations);
            Assert.Contains(response.Warnings, warning => warning.Code == "corpus_not_loaded");
            Assert.Contains(response.Warnings, warning => warning.Code == "activity_source_unverified");
            Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
            Assert.Equal(3, response.SessionsScheduled);
        }
        finally
        {
            await empty.DisposeAsync();
        }
    }

    private static GenerateWorkoutPlanRequest BuildRequest()
    {
        var request = new GenerateWorkoutPlanRequest
        {
            Profile = new UserProfile
            {
                Age = 35,
                Sex = BiologicalSex.Female,
                HeightCm = 165,
                WeightKg = 60,
                ActivityLevel = "ModeratelyActive",
                DietaryPreference = "None",
            },
            Goal = UserGoal.Longevity,
            WeekStartDate = "2026-10-26",
            Days = 7,
            SessionsPerWeek = 3,
            MinutesPerSession = 30,
            Locale = "vi-VN",
        };

        request.ActivityPreference.Add("walking");

        return request;
    }

    private static async Task EnsureChapterFiveAsync(RecommendationApiFixture fixture)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();

        var corpus = await db.Corpus
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.CorpusKey == CorpusSeeder.CorpusKey && entity.IsActive);

        if (corpus is null)
        {
            corpus = new Corpus
            {
                Id = Guid.NewGuid(),
                CorpusKey = CorpusSeeder.CorpusKey,
                Version = "integration-test",
                Title = "Chế độ ăn trường thọ",
                SourcePath = "docs/reference/che-do-an-truong-tho",
                ChapterCount = 1,
                ChunkCount = 1,
                EmbeddingModel = "gemini-embedding-001",
                EmbeddingDim = EmbeddingDimensions,
                ChunkerParams = "{\"tokens\":1000,\"overlap\":150}",
                IsActive = true,
                EmbeddedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.Corpus.Add(corpus);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var chapter = await db.Chapter
            .FirstOrDefaultAsync(entity => entity.CorpusId == corpus.Id && entity.ChapterNumber == 5);

        if (chapter is null)
        {
            chapter = new Chapter
            {
                Id = Guid.NewGuid(),
                CorpusId = corpus.Id,
                ChapterNumber = 5,
                Title = "TẬP THỂ DỤC VÀ SỐNG LÂU KHỎE MẠNH",
                SourceFile = "05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md",
                BookPageFrom = 105,
                BookPageTo = 113,
                PdfPageFrom = 98,
                PdfPageTo = 106,
            };

            db.Chapter.Add(chapter);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
        await store.UpsertAsync(
            [KnowledgeChunk(corpus.Id, chapter.Id)],
            CancellationToken.None);
    }

    private static KnowledgeChunk KnowledgeChunk(Guid corpusId, Guid chapterId) => new()
    {
        CorpusId = corpusId,
        ChapterId = chapterId,
        ChapterNumber = 5,
        ChunkIndex = 0,
        Content = Chapter5Content,
        TokenCount = Chapter5Content.Length / 4,
        ContentHash = Hash(Chapter5Content),
        BookPageFrom = 105,
        BookPageTo = 105,
        PdfPageFrom = 98,
        PdfPageTo = 98,
        Verified = true,
        NeedsReview = false,
        Embedding = UniformVector(),
    };

    private static float[] UniformVector()
    {
        var value = 1f / MathF.Sqrt(EmbeddingDimensions);
        var vector = new float[EmbeddingDimensions];
        Array.Fill(vector, value);
        return vector;
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
