using Recommendation.Api;

// `--migrate-only` dùng cho bước deploy `kb-schema`: tạo schema `kb` bằng đúng
// migration của service mà không mở cổng phục vụ và không cần cấu hình provider.
var migrateOnly = args.Contains("--migrate-only", StringComparer.Ordinal);

var builder = WebApplication.CreateBuilder(args);

RecommendationStartup.Configure(builder, migrateOnly);

var app = builder.Build();

if (migrateOnly)
{
    await RecommendationStartup.MigrateOnlyAsync(app);
    return;
}

await RecommendationStartup.RunAsync(app);

app.Run();
