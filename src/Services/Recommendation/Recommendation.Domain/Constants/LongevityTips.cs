namespace Recommendation.Domain.Constants;

/// <summary>
/// Mẹo tĩnh dùng khi kho tri thức không trả về đoạn phù hợp (fallback của quyết
/// định "longevity_tip lấy từ RAG, không có thì dùng tip tĩnh"), gồm cả gợi ý
/// hành động cho từng nhánh của rubric thói quen trường thọ.
/// </summary>
public static class LongevityTips
{
    /// <summary>Mẹo khi người dùng đã chạm trần calo: chỉ còn đồ uống 0 calo và thư giãn.</summary>
    public const string Relaxation =
        "Bạn đã chạm trần calo hôm nay: hãy chọn nước lọc hoặc trà thảo mộc không đường, "
        + "đi bộ nhẹ 10 phút và giữ giấc ngủ đủ 7 giờ để cơ thể phục hồi thay vì ăn thêm.";

    /// <summary>Mẹo mặc định cho bữa ăn chưa có căn cứ trong sách.</summary>
    public const string Default =
        "Ưu tiên rau xanh, các loại đậu và cá nhỏ; ăn chậm, dừng khi vừa đủ, "
        + "và giữ nhịp ngủ đều để hỗ trợ quá trình trao đổi chất.";

    /// <summary>Gợi ý tĩnh khi nhánh nhịn ăn chưa đạt ngưỡng và corpus không có căn cứ.</summary>
    public const string Fasting =
        "Tăng dần thời gian nhịn ăn hướng tới 16 giờ mỗi ngày nếu sức khỏe cho phép; "
        + "giữ khung giờ ăn đều đặn và tham vấn bác sĩ nếu có bệnh nền.";

    /// <summary>Gợi ý tĩnh khi nhánh giấc ngủ chưa đạt ngưỡng và corpus không có căn cứ.</summary>
    public const string Sleep =
        "Đặt mục tiêu ngủ đủ 7 giờ mỗi đêm, đi ngủ và thức dậy cùng giờ, "
        + "và hạn chế màn hình trong khoảng một giờ trước khi ngủ.";

    /// <summary>Gợi ý tĩnh khi nhánh uống nước chưa đạt ngưỡng và corpus không có căn cứ.</summary>
    public const string Hydration =
        "Uống đủ nước rải đều trong ngày và đặt mục tiêu đạt mục tiêu nước 7/7 ngày trong tuần.";

    /// <summary>Gợi ý tĩnh khi nhánh dinh dưỡng chưa đạt ngưỡng và corpus không có căn cứ.</summary>
    public const string Nutrition =
        "Hướng tới đạt mục tiêu dinh dưỡng ít nhất 90% số ngày trong tuần; "
        + "ưu tiên rau xanh, các loại đậu và ngũ cốc nguyên hạt.";
}
