using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recommendation.Infrastructure.Adapters.Knowledge;
using Recommendation.Infrastructure.Options;
using Recommendation.Infrastructure.Persistence.Configurations;
using Recommendation.Infrastructure.Persistence.Entities;

namespace Recommendation.Infrastructure.Persistence;

/// <summary>
/// DbContext của kho tri thức của Recommendation (<c>recommendation_db</c>, schema <c>kb</c>).
/// Theo Constitution §I, database này CHỈ chứa corpus tri thức đã nhúng —
/// không có bảng nào chứa dữ liệu người dùng, và không được trỏ tới database
/// của service khác.
/// </summary>
public sealed class KnowledgeDbContext : DbContext
{
    private readonly KnowledgeDbOptions _knowledgeDbOptions;

    public KnowledgeDbContext(
        DbContextOptions<KnowledgeDbContext> options,
        IOptions<KnowledgeDbOptions> knowledgeDbOptions)
        : base(options)
    {
        _knowledgeDbOptions = knowledgeDbOptions.Value;
    }

    /// <summary>Các phiên bản corpus.</summary>
    public DbSet<Corpus> Corpus => Set<Corpus>();

    /// <summary>Các chương của corpus.</summary>
    public DbSet<Chapter> Chapter => Set<Chapter>();

    /// <summary>Các đoạn truy xuất của corpus.</summary>
    public DbSet<Chunk> Chunk => Set<Chunk>();

    /// <summary>Các cờ chất lượng theo trang.</summary>
    public DbSet<PageFlag> PageFlag => Set<PageFlag>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // `UseVector()` phải được bật ở MỌI đường cấu hình để mapping kiểu `vector`
        // của pgvector được nạp. Đường DI đã cấu hình sẵn; nhánh dưới đây dành cho
        // design-time (dotnet ef) hoặc khi ai đó dựng context thủ công.
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_knowledgeDbOptions.ConnectionString, npgsql => npgsql.UseVector());
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tên schema cố định của kho tri thức, khớp `KnowledgeDbOptions.Schema`.
        modelBuilder.HasDefaultSchema("kb");

        // Extension `vector` được tạo bằng migration (Code First) — `CREATE EXTENSION`.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfiguration(new CorpusConfiguration());
        modelBuilder.ApplyConfiguration(new ChapterConfiguration());
        modelBuilder.ApplyConfiguration(new ChunkConfiguration());
        modelBuilder.ApplyConfiguration(new PageFlagConfiguration());

        // Keyless entity cho truy vấn hybrid RRF bằng FromSqlRaw. Không map vào
        // bảng/view thật vì SQL tự cung cấp projection (T041, D-14).
        modelBuilder.Entity<ChunkSearchResult>().HasNoKey().ToView(null);

        base.OnModelCreating(modelBuilder);
    }
}
