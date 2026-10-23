namespace Recommendation.Application.Abstractions;

/// <summary>
/// Kết quả đối chiếu kho tri thức với metadata của seed sau khi nạp.
/// Không chứa dữ liệu người dùng — chỉ số liệu của corpus công khai.
/// </summary>
public sealed record SeedVerification
{
    /// <summary><c>true</c> khi mọi kiểm tra đều đạt.</summary>
    public bool Ok { get; init; }

    /// <summary>Khoá corpus đang active.</summary>
    public string CorpusKey { get; init; } = string.Empty;

    /// <summary>Phiên bản corpus đang active.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Số chiều embedding khai báo trong corpus.</summary>
    public int EmbeddingDim { get; init; }

    /// <summary>Số chương thực tế trong database.</summary>
    public int Chapters { get; init; }

    /// <summary>Số đoạn thực tế trong database.</summary>
    public int Chunks { get; init; }

    /// <summary>Số cờ chất lượng trang thực tế trong database.</summary>
    public int PageFlags { get; init; }

    /// <summary>Số đoạn cần rà soát.</summary>
    public int NeedsReview { get; init; }

    /// <summary>Số đoạn đã đối chiếu bản in.</summary>
    public int Verified { get; init; }

    /// <summary>Danh sách kiểm tra không đạt; rỗng khi <see cref="Ok"/> là <c>true</c>.</summary>
    public IReadOnlyList<string> Failures { get; init; } = [];
}
