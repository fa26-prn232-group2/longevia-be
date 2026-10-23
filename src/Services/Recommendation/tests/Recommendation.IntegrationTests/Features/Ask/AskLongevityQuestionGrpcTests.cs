using Longevia.Recommendation.Contracts.V1;
using Recommendation.IntegrationTests.Features.Meals;
using Recommendation.IntegrationTests.Features.Shared;

namespace Recommendation.IntegrationTests.Features.Ask;

/// <summary>
/// T081 — FR-015..FR-019: host thật + PostgreSQL thật, nạp 3 đoạn corpus thật
/// (chương 4) rồi hỏi qua gRPC. Câu có lời giải đích phải trả trích dẫn đúng
/// trang và <c>quote</c> nguyên văn từ chunk; câu ngoài sách phải abstain.
/// </summary>
public sealed class AskLongevityQuestionGrpcTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>, IAsyncLifetime
{
    private static readonly SeedChunk ProteinChunk = new()
    {
        Content =
            "Protein cơ bản được cấu thành từ 20 axit amin, trình tự sắp xếp của chúng "
            + "xác định chức năng cụ thể của mỗi loại protein. Ví dụ, một miếng bít tết 85 gram "
            + "chứa khoảng 25 gram protein.",
        BookPageFrom = 70,
        PdfPageFrom = 62,
        Verified = true,
    };

    private static readonly SeedChunk FatChunk = new()
    {
        Content =
            "Chất béo có thể ở dạng bão hòa (khi mỗi nguyên tử carbon liên kết với số lượng tối đa "
            + "nguyên tử hydro) hoặc không bão hòa (khi carbon liên kết với ít hơn số nguyên tử hydro "
            + "tối đa). Các chất béo không bão hòa có thể được chia thành chất béo không bão hòa đơn "
            + "(ví dụ như axit oleic trong dầu ô liu) hoặc chất béo không bão hòa đa (chẳng hạn như "
            + "trong cá hồi và dầu ngô).",
        BookPageFrom = 72,
        PdfPageFrom = 64,
        Verified = false,
        NeedsReview = true,
    };

    private static readonly SeedChunk VitaminChunk = new()
    {
        Content =
            "Vitamin D, kẽm và sắt rất quan trọng đối với chức năng miễn dịch. Canxi và vitamin D "
            + "rất cần thiết trong việc duy trì mật độ khoáng xương bình thường.",
        BookPageFrom = 73,
        PdfPageFrom = 65,
        Verified = true,
    };

    public Task InitializeAsync() =>
        CorpusSeeder.SeedAsync(fixture.Services, [ProteinChunk, FatChunk, VitaminChunk], CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnswersFromSeededChunkWithExactPageAndVerbatimQuote()
    {
        var response = await fixture.Client.AskLongevityQuestionAsync(new AskLongevityQuestionRequest
        {
            UserId = "user-integration",
            Question = "Chất béo không bão hòa đơn có trong dầu ô liu không?",
        });

        Assert.False(response.Abstained);
        Assert.True(string.IsNullOrEmpty(response.AbstainReason));

        var citation = Assert.Single(response.Citations);
        Assert.Equal(4, citation.ChapterNumber);
        Assert.Equal(FatChunk.BookPageFrom, citation.BookPage);
        Assert.Equal(FatChunk.PdfPageFrom, citation.PdfPage);
        Assert.Contains(citation.Quote, FatChunk.Content, StringComparison.Ordinal);
        Assert.False(citation.Verified);
        Assert.False(string.IsNullOrWhiteSpace(citation.SourceFile));

        Assert.False(string.IsNullOrWhiteSpace(response.Answer));
        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
    }

    [Fact]
    public async Task AbstainsWithoutCitationWhenQuestionIsOutsideBook()
    {
        var response = await fixture.Client.AskLongevityQuestionAsync(new AskLongevityQuestionRequest
        {
            UserId = "user-integration",
            Question = "Giá vàng hôm nay là bao nhiêu?",
        });

        Assert.True(response.Abstained);
        Assert.False(string.IsNullOrWhiteSpace(response.AbstainReason));
        Assert.Empty(response.Citations);
        Assert.False(string.IsNullOrWhiteSpace(response.Disclaimer.Text));
    }
}
