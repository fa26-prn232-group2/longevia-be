using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Features.Ask;
using Recommendation.Infrastructure.Adapters.GoogleAi;
using Recommendation.Infrastructure.Adapters.Knowledge;
using Recommendation.Infrastructure.Adapters.Time;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence;

namespace Recommendation.Infrastructure.DependencyInjection;

/// <summary>
/// Đăng ký knowledge store và các adapter RAG của Recommendation. Composition
/// root (Program.cs) chỉ gọi một lần, tường minh theo interface, không dùng
/// service locator.
/// </summary>
public static class KnowledgeStoreRegistration
{
    /// <summary>Đăng ký <see cref="KnowledgeDbContext"/> và các adapter truy xuất/sinh câu trả lời.</summary>
    public static IServiceCollection AddKnowledgeStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<KnowledgeDbContext>((sp, options) =>
        {
            var knowledgeDb = sp.GetRequiredService<IOptions<KnowledgeDbOptions>>().Value;
            options.UseNpgsql(knowledgeDb.ConnectionString, npgsql => npgsql.UseVector());
        });

        services.AddSingleton<IEmbeddingProvider, GoogleAiEmbeddingProvider>();
        services.AddSingleton<IChatModel, GoogleAiChatModel>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IKnowledgeStore, PgKnowledgeStore>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<CitationResolver>();

        return services;
    }

    /// <summary>Áp dụng migration của <c>recommendation_db</c> trước khi phục vụ request.</summary>
    public static async Task MigrateKnowledgeStoreAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var knowledgeDbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        await knowledgeDbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
