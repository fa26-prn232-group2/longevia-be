namespace Recommendation.Domain.Constants;

/// <summary>
/// Mã cảnh báo ổn định của hai RPC plan (data-model §7). Caller phân biệt phần
/// bị bỏ, phương án dự phòng, dữ liệu bị cắt hoặc giả định đã áp dụng qua mã này.
/// </summary>
public static class PlanWarningCodes
{
    /// <summary>Không truy xuất được chunk nào từ corpus.</summary>
    public const string CorpusNotLoaded = "corpus_not_loaded";

    /// <summary>Provider lỗi/timeout hoặc kết quả không hợp lệ nên đã dùng fallback.</summary>
    public const string AiFallbackUsed = "ai_fallback_used";

    /// <summary>Candidate không đủ để đạt ±10% ngân sách ngày.</summary>
    public const string TargetNotMet = "target_not_met";

    /// <summary>Candidate vượt 120 món nên đã bị cắt theo macro-fit.</summary>
    public const string CandidateFoodsTruncated = "candidate_foods_truncated";

    /// <summary>Candidate có <c>food_id</c> trùng và đã bị loại bản ghi lặp.</summary>
    public const string DuplicateCandidateFood = "duplicate_candidate_food";

    /// <summary>Locale không được hỗ trợ nên đã dùng <c>vi-VN</c>.</summary>
    public const string UnsupportedLocale = "unsupported_locale";

    /// <summary><c>consumed_date</c> nằm ngoài tuần nên rule 105% bị bỏ qua.</summary>
    public const string ConsumedDateOutsidePlan = "consumed_date_outside_plan";

    /// <summary>Ngày chạm trần nhưng không có căn cứ sách cho suggestion.</summary>
    public const string SuggestionSourceMissing = "suggestion_source_missing";

    /// <summary>Caller gửi <c>limitations</c> rỗng; service chỉ dùng hướng dẫn chung.</summary>
    public const string NoLimitationsAssumed = "no_limitations_assumed";

    /// <summary>Hoạt động ưa thích không có trong catalog hoặc không khả thi với thiết bị.</summary>
    public const string UnsupportedActivityPreference = "unsupported_activity_preference";

    /// <summary>Limitations/thiết bị loại hết hoạt động an toàn của một ngày.</summary>
    public const string NoSafeActivity = "no_safe_activity";

    /// <summary>Limitation không khớp bảng ánh xạ đóng nên đã bị bỏ qua.</summary>
    public const string UnsupportedLimitation = "unsupported_limitation";

    /// <summary>Không có câu chương 5 hỗ trợ cường độ nên <c>intensity</c> để rỗng.</summary>
    public const string IntensityUnverified = "intensity_unverified";

    /// <summary>Corpus rỗng nên hoạt động/thời lượng chỉ lấy từ dữ liệu caller gửi.</summary>
    public const string ActivitySourceUnverified = "activity_source_unverified";
}
