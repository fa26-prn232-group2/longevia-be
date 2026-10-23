namespace Recommendation.Application.Abstractions;

/// <summary>
/// Kết quả một lần nạp seed. Khi <see cref="Skipped"/> là <c>true</c> thì không có
/// thao tác ghi nào được thực hiện.
/// </summary>
public sealed record SeedLoadResult
{
    /// <summary><c>true</c> khi corpus đã active đúng version và loader bỏ qua ghi.</summary>
    public bool Skipped { get; init; }

    /// <summary>Lý do bỏ qua (chỉ có nghĩa khi <see cref="Skipped"/> là <c>true</c>).</summary>
    public string? Reason { get; init; }

    /// <summary>Định danh corpus trong <c>kb.corpus</c>.</summary>
    public Guid CorpusId { get; init; }

    /// <summary>Khoá corpus.</summary>
    public string CorpusKey { get; init; } = string.Empty;

    /// <summary>Phiên bản corpus (hash nội dung).</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Số chương đã nạp (hoặc đang có khi bỏ qua).</summary>
    public int Chapters { get; init; }

    /// <summary>Số đoạn đã nạp (hoặc đang có khi bỏ qua).</summary>
    public int Chunks { get; init; }

    /// <summary>Số cờ chất lượng trang đã nạp (hoặc đang có khi bỏ qua).</summary>
    public int PageFlags { get; init; }

    /// <summary>Báo cáo verify khi <see cref="SeedLoadOptions.Verify"/> được bật.</summary>
    public SeedVerification? Verification { get; init; }
}
