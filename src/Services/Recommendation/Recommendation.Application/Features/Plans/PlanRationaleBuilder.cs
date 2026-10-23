using System.Globalization;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Plans;

/// <summary>
/// Dựng <c>rationale</c> của meal/workout plan bằng code từ dữ liệu request,
/// món/hoạt động đã chọn và quote đã verify (D-06). Không nhận free text của mô
/// hình; quote chứa cụm từ y tế bị chặn bị bỏ khỏi rationale.
/// </summary>
public sealed class PlanRationaleBuilder
{
    private readonly SafetyGuard _safetyGuard;

    /// <summary>Tạo builder với guard dùng chung để lọc cụm từ y tế.</summary>
    public PlanRationaleBuilder(SafetyGuard safetyGuard)
    {
        ArgumentNullException.ThrowIfNull(safetyGuard);
        _safetyGuard = safetyGuard;
    }

    /// <summary>
    /// Rationale của một bữa: số món, tổng calo/protein theo khẩu phần caller đã
    /// khai và trích dẫn sách đã verify (nếu có).
    /// </summary>
    public string BuildMealRationale(
        MealSlot slot,
        IReadOnlyList<CandidateFood> foods,
        IReadOnlyList<Citation> citations)
    {
        ArgumentNullException.ThrowIfNull(foods);
        ArgumentNullException.ThrowIfNull(citations);

        var calories = foods.Sum(food => food.Calories);
        var protein = foods.Sum(food => food.ProteinGrams);
        var slotLabel = MealSlotLabels.Vietnamese(slot);
        var sentenceStart = slotLabel.Length == 0
            ? slotLabel
            : char.ToUpperInvariant(slotLabel[0]) + slotLabel[1..];

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{sentenceStart} chọn {foods.Count} món trong danh mục caller gửi, "
            + $"cung cấp {protein:0.#} g protein và {calories} kcal theo khẩu phần đã khai.");

        return AppendCitations(text, citations);
    }

    /// <summary>
    /// Rationale của một buổi tập: activity key, thời lượng caller gửi, giới hạn
    /// caller khai và trích dẫn sách đã verify (nếu có).
    /// </summary>
    public string BuildWorkoutRationale(
        string activityKey,
        int durationMinutes,
        IReadOnlyList<string> limitations,
        IReadOnlyList<Citation> citations)
    {
        ArgumentNullException.ThrowIfNull(activityKey);
        ArgumentNullException.ThrowIfNull(limitations);
        ArgumentNullException.ThrowIfNull(citations);

        var limitationText = limitations.Count == 0
            ? "chưa khai giới hạn sức khỏe nên chỉ dùng hướng dẫn chung"
            : $"phù hợp với giới hạn đã khai ({string.Join(", ", limitations)})";

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"Buổi tập {activityKey} kéo dài {durationMinutes} phút, {limitationText}.");

        return AppendCitations(text, citations);
    }

    private string AppendCitations(string text, IReadOnlyList<Citation> citations)
    {
        if (citations.Count == 0)
        {
            return text;
        }

        var parts = new List<string>(citations.Count);
        foreach (var citation in citations)
        {
            var source = string.Create(
                CultureInfo.InvariantCulture,
                $"chương {citation.ChapterNumber}, trang {citation.BookPage}");

            var quote = citation.Quote.Trim();
            parts.Add(quote.Length > 0 && _safetyGuard.FindRefusal(quote) is null
                ? $"Theo sách ({source}): “{quote}”"
                : $"Theo sách ({source}).");
        }

        return $"{text} {string.Join(' ', parts)}";
    }
}
