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
using CandidateFood = Longevia.Recommendation.Contracts.V1.CandidateFood;
using UserProfile = Longevia.Recommendation.Contracts.V1.UserProfile;

namespace Recommendation.IntegrationTests.Features.Plans;

/// <summary>
/// T032 — FR-002/FR-003/FR-023: citation trả về chỉ thuộc chương 4 hoặc chương 12
/// có <c>book_page_from &gt;= 238</c> (Phụ lục A); quote khớp chunk được giữ, quote
/// sai bị loại; corpus rỗng thì citations rỗng + <c>corpus_not_loaded</c>.
/// </summary>
public sealed class MealPlanCitationGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>
{
    private const int EmbeddingDimensions = 768;
    private const string Chapter4Content =
        "Chế độ ăn trường thọ ưu tiên rau xanh, đậu và ngũ cốc nguyên hạt.";
    private const string AppendixContent =
        "Phụ lục A gợi ý uống trà thảo mộc không đường vào buổi tối.";
    private const string BeforeAppendixContent =
        "Nội dung trước Phụ lục A không được dùng làm căn cứ cho kế hoạch.";

    [Fact]
    public async Task KeepsVerifiedCitationWhenQuoteMatchesTheChunk()
    {
        await EnsureCorpusAsync(fixture);

        var response = await fixture.Client.GenerateMealPlanAsync(ExactRequest());

        Assert.NotEmpty(response.Citations);
        Assert.DoesNotContain(response.Warnings, warning => warning.Code == "corpus_not_loaded");
        Assert.All(response.Citations, citation => Assert.Contains(citation.Quote, Chapter4Content, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DropsCitationWhenQuoteDoesNotMatchAnyChunk()
    {
        await EnsureCorpusAsync(fixture);
        fixture.UseInvalidCitationForNextMealPlan();

        var response = await fixture.Client.GenerateMealPlanAsync(ExactRequest());

        Assert.Empty(response.Citations);
        Assert.DoesNotContain(response.Warnings, warning => warning.Code == "corpus_not_loaded");
    }

    [Fact]
    public async Task CitationsBelongOnlyToChapterFourOrAppendixA()
    {
        await EnsureCorpusAsync(fixture);

        var response = await fixture.Client.GenerateMealPlanAsync(ExactRequest());

        Assert.All(response.Citations, citation =>
        {
            var allowed = citation.ChapterNumber == 4
                || (citation.ChapterNumber == 12 && citation.BookPage >= 238);
            Assert.True(
                allowed,
                $"Citation ngoài chương 4/Phụ lục A: chapter={citation.ChapterNumber}, page={citation.BookPage}.");
        });
    }

    [Fact]
    public async Task EmptyCorpusReturnsNoCitationsAndCorpusWarning()
    {
        var empty = new RecommendationApiFixture();
        await empty.InitializeAsync();

        try
        {
            var response = await empty.Client.GenerateMealPlanAsync(ExactRequest());

            Assert.Empty(response.Citations);
            Assert.Contains(response.Warnings, warning => warning.Code == "corpus_not_loaded");
            Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
        }
        finally
        {
            await empty.DisposeAsync();
        }
    }

    private static GenerateMealPlanRequest ExactRequest()
    {
        var request = new GenerateMealPlanRequest
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
            TargetCalories = 2000,
            TargetProteinGrams = 100,
            TargetCarbsGrams = 250,
            TargetFatGrams = 67,
            WeekStartDate = "2026-10-26",
            Days = 1,
            MealsPerDay = 1,
            Locale = "vi-VN",
        };

        request.CandidateFoods.Add(new CandidateFood
        {
            FoodId = "f-exact",
            Name = "Bữa ăn cân bằng",
            ServingSize = "1",
            ServingUnit = "phần",
            Calories = 2000,
            ProteinGrams = 100,
            CarbsGrams = 250,
            FatGrams = 67,
            FiberGrams = 30,
        });

        return request;
    }

    private static async Task EnsureCorpusAsync(RecommendationApiFixture fixture)
    {
        await CorpusSeeder.SeedAsync(
            fixture.Services,
            [
                new SeedChunk
                {
                    Content = Chapter4Content,
                    BookPageFrom = 72,
                    PdfPageFrom = 64,
                    Verified = true,
                },
            ],
            CancellationToken.None);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();

        var corpus = await db.Corpus
            .AsNoTracking()
            .SingleAsync(entity => entity.CorpusKey == CorpusSeeder.CorpusKey && entity.IsActive);

        var chapter = await db.Chapter
            .FirstOrDefaultAsync(entity => entity.CorpusId == corpus.Id && entity.ChapterNumber == 12);

        if (chapter is null)
        {
            chapter = new Chapter
            {
                Id = Guid.NewGuid(),
                CorpusId = corpus.Id,
                ChapterNumber = 12,
                Title = "CÁCH ĐỂ GIỮ MÃI TUỔI THANH XUÂN",
                SourceFile = "12-chuong-12-cach-de-giu-mai-tuoi-thanh-xuan.md",
                BookPageFrom = 230,
                BookPageTo = 260,
                PdfPageFrom = 220,
                PdfPageTo = 250,
            };

            db.Chapter.Add(chapter);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
        await store.UpsertAsync(
            [
                KnowledgeChunk(corpus.Id, chapter.Id, 0, AppendixContent, 238),
                KnowledgeChunk(corpus.Id, chapter.Id, 1, BeforeAppendixContent, 237),
            ],
            CancellationToken.None);
    }

    private static KnowledgeChunk KnowledgeChunk(
        Guid corpusId,
        Guid chapterId,
        int chunkIndex,
        string content,
        int bookPage) => new()
        {
            CorpusId = corpusId,
            ChapterId = chapterId,
            ChapterNumber = 12,
            ChunkIndex = chunkIndex,
            Content = content,
            TokenCount = content.Length / 4,
            ContentHash = Hash(content),
            BookPageFrom = bookPage,
            BookPageTo = bookPage,
            PdfPageFrom = bookPage,
            PdfPageTo = bookPage,
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
