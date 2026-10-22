namespace Recommendation.Domain.Constants;

/// <summary>
/// Mẹo tĩnh dùng khi kho tri thức không trả về đoạn phù hợp (fallback của quyết
/// định "longevity_tip lấy từ RAG, không có thì dùng tip tĩnh").
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
}
