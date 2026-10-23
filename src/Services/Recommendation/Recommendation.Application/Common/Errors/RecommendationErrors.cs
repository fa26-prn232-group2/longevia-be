using Longevia.Shared.Errors;

namespace Recommendation.Application.Common.Errors;

/// <summary>
/// Factory lỗi nghiệp vụ của Recommendation. Mã lỗi ổn định dạng
/// <c>recommendation.*</c>, không đổi sau khi đã phát hành.
/// </summary>
public static class RecommendationErrors
{
    /// <summary>Danh mục ứng viên không còn món nào an toàn sau khi lọc.</summary>
    public static AppException NoCandidateFood() =>
        new(
            "Không còn món nào an toàn trong danh mục ứng viên.",
            422,
            "Unprocessable request.",
            "recommendation.no_candidate_food");

    /// <summary>Kho tri thức không truy cập được.</summary>
    public static AppException KnowledgeUnavailable() =>
        AppException.ServiceUnavailable("Kho tri thức tạm thời không khả dụng.")
            .WithCode("recommendation.knowledge_unavailable");

    /// <summary>Provider AI (embedding/chat) không khả dụng.</summary>
    public static AppException ProviderUnavailable() =>
        AppException.ServiceUnavailable("Dịch vụ AI tạm thời không khả dụng.")
            .WithCode("recommendation.provider_unavailable");

    /// <summary>Câu hỏi rỗng hoặc vượt giới hạn độ dài.</summary>
    public static AppException InvalidQuestion() =>
        AppException.BadRequest("Câu hỏi không hợp lệ.")
            .WithCode("recommendation.invalid_question");

    // ---- Lỗi nghiệp vụ của hai RPC plan (data-model §8, D-11) ----

    /// <summary>Thiếu <c>target_calories</c> hoặc target protein/carbs/fat.</summary>
    public static AppException MissingTargets() =>
        PlanBadRequest("Thiếu mục tiêu calo hoặc macro bắt buộc.", "recommendation.missing_targets");

    /// <summary>Target calo/macro/fiber âm hoặc không hợp lệ.</summary>
    public static AppException InvalidTargets() =>
        PlanBadRequest("Mục tiêu calo, macro hoặc fiber không hợp lệ.", "recommendation.invalid_targets");

    /// <summary><c>consumed_calories</c> âm.</summary>
    public static AppException InvalidConsumedCalories() =>
        PlanBadRequest("Calo đã ăn không hợp lệ.", "recommendation.invalid_consumed_calories");

    /// <summary><c>week_start_date</c> sai định dạng ISO hoặc không phải thứ Hai.</summary>
    public static AppException InvalidWeekStart() =>
        PlanBadRequest(
            "Ngày bắt đầu tuần không hợp lệ; phải là thứ Hai theo định dạng ISO YYYY-MM-DD.",
            "recommendation.invalid_week_start");

    /// <summary><c>days</c> ngoài khoảng 1..7.</summary>
    public static AppException InvalidDays() =>
        PlanBadRequest("Số ngày phải nằm trong khoảng 1..7.", "recommendation.invalid_days");

    /// <summary><c>meals_per_day</c> ngoài khoảng 1..4.</summary>
    public static AppException InvalidMealsPerDay() =>
        PlanBadRequest("Số bữa mỗi ngày phải nằm trong khoảng 1..4.", "recommendation.invalid_meals_per_day");

    /// <summary>Profile thiếu trường bắt buộc hoặc giá trị không thuộc tập cho phép.</summary>
    public static AppException InvalidProfile() =>
        PlanBadRequest("Hồ sơ người dùng không hợp lệ.", "recommendation.invalid_profile");

    /// <summary><c>goal</c> không hợp lệ hoặc còn <c>GOAL_UNSPECIFIED</c>.</summary>
    public static AppException InvalidGoal() =>
        PlanBadRequest("Mục tiêu tập luyện không hợp lệ.", "recommendation.invalid_goal");

    /// <summary>Món ứng viên thiếu <c>food_id</c>/tên hoặc có dinh dưỡng âm.</summary>
    public static AppException InvalidCandidateFood() =>
        PlanBadRequest(
            "Món ứng viên thiếu trường bắt buộc hoặc có dinh dưỡng âm.",
            "recommendation.invalid_candidate_food");

    /// <summary>Sau khi lọc allergen/dietary/excluded không còn món nào.</summary>
    public static AppException AllCandidatesExcluded() =>
        new(
            "Không còn món nào an toàn sau khi lọc dị ứng, sở thích và danh sách loại trừ.",
            422,
            "Unprocessable request.",
            "recommendation.all_candidates_excluded");

    /// <summary>Sessions/minutes ngoài khoảng hoặc <c>sessions_per_week > days</c>.</summary>
    public static AppException InvalidWorkoutParams() =>
        PlanBadRequest("Tham số tập luyện không hợp lệ.", "recommendation.invalid_workout_params");

    /// <summary>Có <c>consumed_calories > 0</c> nhưng thiếu <c>consumed_date</c>.</summary>
    public static AppException MissingConsumedDate() =>
        PlanBadRequest("Thiếu ngày đã ăn calo khi có calo đã ăn.", "recommendation.missing_consumed_date");

    /// <summary><c>consumed_date</c> sai định dạng ISO.</summary>
    public static AppException InvalidConsumedDate() =>
        PlanBadRequest(
            "Ngày đã ăn calo không hợp lệ; phải theo định dạng ISO YYYY-MM-DD.",
            "recommendation.invalid_consumed_date");

    // Trailer gRPC 'title' chỉ nhận ASCII; chi tiết tiếng Việt vẫn nằm trong
    // Status.Detail. Mã ổn định đi qua trailer 'code' theo D-11.
    private static AppException PlanBadRequest(string detail, string code) =>
        new(detail, 400, "Invalid request.", code);
}
