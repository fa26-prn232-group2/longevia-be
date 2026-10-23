using System.Globalization;
using System.Text;
using Recommendation.Application.Features.Plans;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Dựng prompt cho <c>GenerateWorkoutPlan</c> (D-06/D-09): gửi skeleton, catalog
/// đóng và chunk chương 5 trong khối corpus. Mô hình chỉ được chọn
/// <c>activity_index</c> và citation; code quyết định thời lượng, cường độ và
/// rationale.
/// </summary>
public sealed class WorkoutPlanPromptBuilder(PlanPromptFormatter formatter)
{
    /// <summary>System instruction cấm bịa bài tập/số liệu/rationale.</summary>
    public const string SystemInstruction =
        "Bạn là trợ lý lập kế hoạch tập luyện dựa trên sách tham chiếu. "
        + "Chỉ chọn activity_index có trong danh mục CATALOG được cung cấp; không tạo bài tập mới, "
        + "không dùng thiết bị ngoài giới hạn caller. "
        + "Không viết thời lượng, không viết cường độ và không viết rationale; code sẽ dựng các phần đó. "
        + "Chỉ trả citation khi quote xuất hiện nguyên văn trong khối [BEGIN CORPUS DATA] và dùng đúng chunk_index. "
        + "Không trả ngày nghỉ; chỉ trả các ngày có buổi tập trong SKELETON.";

    /// <summary>Xây prompt cho một lời gọi <c>GenerateWorkoutPlan</c>.</summary>
    public StructuredPrompt Build(
        GenerateWorkoutPlanRequest request,
        WorkoutPlanSkeleton skeleton,
        WorkoutActivityCatalog catalog,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(chunks);

        var prompt = new StringBuilder();
        prompt
            .Append("Kế hoạch tập luyện ")
            .Append(request.Days.ToString(CultureInfo.InvariantCulture))
            .Append(" ngày, ")
            .Append(skeleton.SessionsRequested.ToString(CultureInfo.InvariantCulture))
            .Append(" buổi, mỗi buổi ")
            .Append(skeleton.MinutesPerSession.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" phút (thời lượng do code quyết định).")
            .Append("Mục tiêu: ")
            .AppendLine(request.Goal)
            .Append("Giới hạn caller: ")
            .AppendLine(FormatList(request.Limitations, "chưa khai"))
            .Append("Thiết bị sẵn có: ")
            .AppendLine(FormatList(request.EquipmentAvailable, "không có"))
            .AppendLine("CATALOG (chỉ được chọn activity_index trong danh sách này):");

        for (var index = 0; index < catalog.Activities.Count; index++)
        {
            var activity = catalog.Activities[index];
            prompt
                .Append('[')
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("] key=")
                .Append(activity.Key)
                .Append("; name=")
                .Append(activity.DisplayName)
                .Append("; equipment=")
                .Append(activity.EquipmentUsed.Count == 0 ? "none" : string.Join('|', activity.EquipmentUsed))
                .Append("; intensity=")
                .AppendLine(activity.Intensity.Length == 0 ? "unverified" : activity.Intensity);
        }

        prompt.AppendLine("SKELETON (mỗi ngày tối đa một buổi; ngày nghỉ không chọn hoạt động):");
        foreach (var day in skeleton.Days)
        {
            prompt
                .Append(day.Date)
                .Append(": ")
                .AppendLine(day.Activity is null ? "ngày nghỉ" : "buổi tập");
        }

        var corpus = formatter.FormatCorpus(chunks);
        if (corpus.Length > 0)
        {
            prompt.AppendLine(corpus);
        }

        return new StructuredPrompt
        {
            SystemInstruction = SystemInstruction,
            UserPrompt = prompt.ToString().TrimEnd(),
            ResponseKind = StructuredResponseKind.WorkoutPlanSelection,
        };
    }

    private static string FormatList(IReadOnlyList<string> values, string emptyText) =>
        values.Count == 0 ? emptyText : string.Join(", ", values);
}
