using System.Globalization;
using System.Text;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Plans;

/// <summary>
/// Đóng gói các đoạn corpus thành một khối dữ liệu rõ ràng cho prompt plan
/// (FR-001/D-06): mỗi đoạn có chỉ số, chương, trang và trạng thái đối chiếu để
/// mô hình chỉ được trích dẫn trong khối này.
/// </summary>
public sealed class PlanPromptFormatter
{
    /// <summary>Mốc mở khối corpus-as-data.</summary>
    public const string CorpusBeginMarker = "[BEGIN CORPUS DATA]";

    /// <summary>Mốc đóng khối corpus-as-data.</summary>
    public const string CorpusEndMarker = "[END CORPUS DATA]";

    /// <summary>Trạng thái của đoạn đã đối chiếu bản in.</summary>
    public const string VerifiedStatus = "VERIFIED";

    /// <summary>Trạng thái của đoạn còn cần người rà soát.</summary>
    public const string NeedsReviewStatus = "NEEDS_REVIEW";

    /// <summary>
    /// Trả khối corpus; mỗi đoạn gồm dòng metadata và nội dung nguyên văn.
    /// Corpus rỗng trả chuỗi rỗng để prompt không có khối dữ liệu giả.
    /// </summary>
    public string FormatCorpus(IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine(CorpusBeginMarker);

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index].Chunk;
            builder
                .Append('[')
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("] chapter=")
                .Append(chunk.ChapterNumber.ToString(CultureInfo.InvariantCulture))
                .Append("; book_page=")
                .Append(chunk.BookPageFrom.ToString(CultureInfo.InvariantCulture))
                .Append("; status=")
                .AppendLine(chunk.Verified ? VerifiedStatus : NeedsReviewStatus)
                .AppendLine(chunk.Content.Trim());
        }

        builder.Append(CorpusEndMarker);
        return builder.ToString();
    }
}
