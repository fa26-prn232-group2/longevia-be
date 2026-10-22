using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Recommendation.Infrastructure.Options;

namespace Recommendation.Infrastructure.Persistence;

/// <summary>
/// Factory design-time cho <see cref="KnowledgeDbContext"/>: dùng cho
/// <c>dotnet ef migrations</c> khi không có host ASP.NET Core chạy thật.
/// Nguồn connection string: biến môi trường <c>ConnectionStrings__Recommendation</c>,
/// nếu thiếu thì đọc từ file <c>.env</c> ở gốc repository (file máy-local, gitignored).
/// </summary>
public sealed class KnowledgeDbContextFactory : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Recommendation";

    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        var knowledgeDbOptions = Microsoft.Extensions.Options.Options.Create(new KnowledgeDbOptions
        {
            ConnectionString = connectionString,
        });

        return new KnowledgeDbContext(options, knowledgeDbOptions);
    }

    private static string ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var fromDotEnv = ReadFromDotEnv();
        if (!string.IsNullOrWhiteSpace(fromDotEnv))
        {
            return fromDotEnv;
        }

        throw new InvalidOperationException(
            $"Không tìm thấy connection string '{ConnectionStringVariable}'. " +
            "Đặt biến môi trường này hoặc thêm vào file .env ở gốc repository.");
    }

    private static string? ReadFromDotEnv()
    {
        foreach (var directory in EnumerateCandidateDirectories())
        {
            var envFile = Path.Combine(directory, ".env");
            if (!File.Exists(envFile))
            {
                continue;
            }

            foreach (var line in File.ReadLines(envFile))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = trimmed[..separator].Trim();
                if (!string.Equals(key, ConnectionStringVariable, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = trimmed[(separator + 1)..].Trim().Trim('"').Trim('\'');
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateDirectories()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }
}
