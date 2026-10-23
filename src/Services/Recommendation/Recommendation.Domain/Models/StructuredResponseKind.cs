namespace Recommendation.Domain.Models;

/// <summary>
/// Loại phản hồi có cấu trúc mà <c>IChatModel</c> phải sinh theo JSON schema
/// tương ứng. Schema cụ thể do tầng Infrastructure dựng.
/// </summary>
public enum StructuredResponseKind
{
    /// <summary>Câu trả lời có căn cứ: nội dung trả lời + danh sách trích dẫn.</summary>
    GroundedAnswer = 0,

    /// <summary>
    /// Chọn món cho một bữa: chỉ số món trong danh mục ứng viên + lý do + trích dẫn.
    /// Mô hình không được sinh món mới hay số liệu dinh dưỡng.
    /// </summary>
    MealSelection = 1,

    /// <summary>
    /// Giải thích khoảng thiếu vi chất do hệ thống tính sẵn + thực phẩm gợi ý lấy
    /// từ sách + trích dẫn. Mô hình không được tính lại hay bịa số liệu.
    /// </summary>
    NutrientGapAnalysis = 2,

    /// <summary>
    /// Chọn món cho kế hoạch nhiều ngày: chỉ số món trong danh mục ứng viên +
    /// trích dẫn cho từng bữa. Không có rationale hay số liệu dinh dưỡng; code
    /// dựng các phần đó từ dữ liệu request và candidate.
    /// </summary>
    MealPlanSelection = 3,

    /// <summary>
    /// Chọn hoạt động cho các buổi tập trong skeleton: chỉ số hoạt động trong
    /// catalog đóng + trích dẫn. Không có thời lượng/cường độ/bài tập mới.
    /// </summary>
    WorkoutPlanSelection = 4,
}
