using System.Diagnostics;
using System.Text.Json;
using Longevia.Recommendation.Contracts.V1;
using Microsoft.Extensions.DependencyInjection;
using Recommendation.Application.Abstractions;
using Recommendation.Domain.Models;
using Recommendation.IntegrationTests.Features.Meals;
using Recommendation.IntegrationTests.Features.Shared;

namespace Recommendation.IntegrationTests.Performance;

/// <summary>
/// T111 - FR-021/SC-005: đo budget độ trễ trên PostgreSQL thật. Bước truy xuất
/// thuần (vector + keyword + RRF, không gọi provider thật) phải có p95 dưới
/// 150ms nên được <b>assert</b>; p95 end-to-end của một lời gọi gRPC chỉ được
/// <b>ghi</b> ra artifact vì phụ thuộc provider/mạng của máy chạy test.
/// </summary>
public sealed class LatencyBudgetTests(RecommendationApiFixture fixture)
    : IClassFixture<RecommendationApiFixture>, IAsyncLifetime
{
    private const int RetrievalWarmup = 5;
    private const int RetrievalIterations = 30;
    private const int EndToEndWarmup = 2;
    private const int EndToEndIterations = 20;
    private const double RetrievalP95BudgetMs = 150;

    private const string RetrievalQuery = "nhịn ăn giấc ngủ nước chế độ ăn trường thọ";
    private const string EndToEndQuestion = "Nhịn ăn và giấc ngủ ảnh hưởng thế nào tới tuổi thọ?";

    private static readonly SeedChunk[] SeedChunks =
    [
        new()
        {
            Content =
                "Chế độ ăn Trường thọ ưu tiên thực phẩm có nguồn gốc thực vật, thêm một ít cá và "
                + "giới hạn protein ở mức vừa đủ.",
            BookPageFrom = 79,
            PdfPageFrom = 71,
            Verified = true,
        },
        new()
        {
            Content =
                "Nhịn ăn gián đoạn giúp cơ thể chuyển sang trạng thái đốt mỡ; giấc ngủ đủ và uống "
                + "đủ nước là hai thói quen nền tảng đi kèm.",
            BookPageFrom = 84,
            PdfPageFrom = 76,
            Verified = true,
        },
        new()
        {
            Content =
                "Uống đủ nước mỗi ngày hỗ trợ chuyển hóa và giữ cho giấc ngủ sâu hơn; nước là "
                + "thành phần thiết yếu của chế độ ăn trường thọ.",
            BookPageFrom = 81,
            PdfPageFrom = 73,
            Verified = true,
        },
        new()
        {
            Content =
                "Ăn hai bữa chính cộng một bữa nhẹ giúp giữ vòng eo khỏe mạnh và ổn định đường huyết.",
            BookPageFrom = 82,
            PdfPageFrom = 74,
            Verified = true,
        },
        new()
        {
            Content =
                "Protein ít nhưng đủ: tiêu thụ khoảng 0,31-0,36 gram protein trên mỗi pound cân nặng.",
            BookPageFrom = 79,
            PdfPageFrom = 71,
            Verified = true,
        },
        new()
        {
            Content =
                "Thực hành nhịn ăn kéo dài định kỳ và duy trì giấc ngủ đều đặn là hai trụ cột được "
                + "nhắc lại trong chế độ ăn trường thọ.",
            BookPageFrom = 84,
            PdfPageFrom = 76,
            Verified = true,
        },
    ];

    public Task InitializeAsync() =>
        CorpusSeeder.SeedAsync(fixture.Services, SeedChunks, CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RetrievalP95StaysUnderBudgetAndEndToEndIsReported()
    {
        var retrievalMilliseconds = await MeasureRetrievalAsync();
        var endToEndMilliseconds = await MeasureEndToEndAsync();

        var retrievalP95 = Percentile(retrievalMilliseconds, 0.95);
        var endToEndP95 = Percentile(endToEndMilliseconds, 0.95);

        await WriteReportAsync(retrievalMilliseconds, retrievalP95, endToEndMilliseconds, endToEndP95);

        Assert.True(
            retrievalP95 < RetrievalP95BudgetMs,
            $"p95 truy xuất {retrievalP95:0.##}ms vượt budget {RetrievalP95BudgetMs}ms.");
    }

    private async Task<List<double>> MeasureRetrievalAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
        var query = new KnowledgeQuery { Query = RetrievalQuery };

        for (var index = 0; index < RetrievalWarmup; index++)
        {
            var warmup = await store.SearchAsync(query, CancellationToken.None);
            Assert.NotEmpty(warmup);
        }

        var samples = new List<double>(RetrievalIterations);
        for (var index = 0; index < RetrievalIterations; index++)
        {
            var start = Stopwatch.GetTimestamp();
            var results = await store.SearchAsync(query, CancellationToken.None);
            samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Assert.NotEmpty(results);
        }

        return samples;
    }

    private async Task<List<double>> MeasureEndToEndAsync()
    {
        var request = new AskLongevityQuestionRequest
        {
            UserId = "user-latency",
            Question = EndToEndQuestion,
        };

        for (var index = 0; index < EndToEndWarmup; index++)
        {
            await fixture.Client.AskLongevityQuestionAsync(request, deadline: Deadline());
        }

        var samples = new List<double>(EndToEndIterations);
        for (var index = 0; index < EndToEndIterations; index++)
        {
            var start = Stopwatch.GetTimestamp();
            await fixture.Client.AskLongevityQuestionAsync(request, deadline: Deadline());
            samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        return samples;
    }

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(10);

    private static double Percentile(List<double> samples, double percentile)
    {
        var ordered = samples.OrderBy(value => value).ToList();
        var index = (int)Math.Ceiling(percentile * ordered.Count) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Count - 1)];
    }

    private static async Task WriteReportAsync(
        List<double> retrieval,
        double retrievalP95,
        List<double> endToEnd,
        double endToEndP95)
    {
        var report = new
        {
            generated_at = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            retrieval = new
            {
                metric = "IKnowledgeStore.SearchAsync (vector + keyword + RRF)",
                iterations = retrieval.Count,
                p50_ms = Math.Round(Percentile(retrieval, 0.50), 2),
                p95_ms = Math.Round(retrievalP95, 2),
                budget_ms = RetrievalP95BudgetMs,
                asserted = true,
                passed = retrievalP95 < RetrievalP95BudgetMs,
            },
            end_to_end = new
            {
                metric = "gRPC AskLongevityQuestion (provider double)",
                iterations = endToEnd.Count,
                p50_ms = Math.Round(Percentile(endToEnd, 0.50), 2),
                p95_ms = Math.Round(endToEndP95, 2),
                target_ms = 3000,
                asserted = false,
            },
        };

        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "artifacts", "book-kb", "latency-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "longevia-be.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc chứa longevia-be.slnx.");
    }
}
