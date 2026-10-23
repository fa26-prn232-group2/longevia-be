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
        AppException.Unprocessable("Không còn món nào an toàn trong danh mục ứng viên.")
            .WithCode("recommendation.no_candidate_food");

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
        AppException.BadRequest("Thiếu mục tiêu calo hoặc macro bắt buộc.")
            .WithCode("recommendation.missing_targets");

    /// <summary>Target calo/macro/fiber âm hoặc không hợp lệ.</summary>
    public static AppException InvalidTargets() =>
        AppException.BadRequest("Mục tiêu calo, macro hoặc fiber không hợp lệ.")
            .WithCode("recommendation.invalid_targets");

    /// <summary><c>consumed_calories</c> âm.</summary>
    public static AppException InvalidConsumedCalories() =>
        AppException.BadRequest("Calo đã ăn không hợp lệ.")
            .WithCode("recommendation.invalid_consumed_calories");

    /// <summary><c>week_start_date</c> sai định dạng ISO hoặc không phải thứ Hai.</summary>
    public static AppException InvalidWeekStart() =>
        AppException.BadRequest("Ngày bắt đầu tuần không hợp lệ; phải là thứ Hai theo định dạng ISO YYYY-MM-DD.")
            .WithCode("recommendation.invalid_week_start");

    /// <summary><c>days</c> ngoài khoảng 1..7.</summary>
    public static AppException InvalidDays() =>
        AppException.BadRequest("Số ngày phải nằm trong khoảng 1..7.")
            .WithCode("recommendation.invalid_days");

    /// <summary><c>meals_per_day</c> ngoài khoảng 1..4.</summary>
    public static AppException InvalidMealsPerDay() =>
        AppException.BadRequest("Số bữa mỗi ngày phải nằm trong khoảng 1..4.")
            .WithCode("recommendation.invalid_meals_per_day");

    /// <summary>Profile thiếu trường bắt buộc hoặc giá trị không thuộc tập cho phép.</summary>
    public static AppException InvalidProfile() =>
        AppException.BadRequest("Hồ sơ người dùng không hợp lệ.")
            .WithCode("recommendation.invalid_profile");

    /// <summary><c>goal</c> không hợp lệ hoặc còn <c>GOAL_UNSPECIFIED</c>.</summary>
    public static AppException InvalidGoal() =>
        AppException.BadRequest("Mục tiêu tập luyện không hợp lệ.")
            .WithCode("recommendation.invalid_goal");

    /// <summary>Món ứng viên thiếu <c>food_id</c>/tên hoặc có dinh dưỡng âm.</summary>
    public static AppException InvalidCandidateFood() =>
        AppException.BadRequest("Món ứng viên thiếu trường bắt buộc hoặc có dinh dưỡng âm.")
            .WithCode("recommendation.invalid_candidate_food");

    /// <summary>Sau khi lọc allergen/dietary/excluded không còn món nào.</summary>
    public static AppException AllCandidatesExcluded() =>
        AppException.Unprocessable("Không còn món nào an toàn sau khi lọc dị ứng, sở thích và danh sách loại trừ.")
            .WithCode("recommendation.all_candidates_excluded");

    /// <summary>Sessions/minutes ngoài khoảng hoặc <c>sessions_per_week > days</c>.</summary>
    public static AppException InvalidWorkoutParams() =>
        AppException.BadRequest("Tham số tập luyện không hợp lệ.")
            .WithCode("recommendation.invalid_workout_params");

    /// <summary>Có <c>consumed_calories > 0</c> nhưng thiếu <c>consumed_date</c>.</summary>
    public static AppException MissingConsumedDate() =>
        AppException.BadRequest("Thiếu ngày đã ăn calo khi có calo đã ăn.")
            .WithCode("recommendation.missing_consumed_date");

    /// <summary><c>consumed_date</c> sai định dạng ISO.</summary>
    public static AppException InvalidConsumedDate() =>
        AppException.BadRequest("Ngày đã ăn calo không hợp lệ; phải theo định dạng ISO YYYY-MM-DD.")
            .WithCode("recommendation.invalid_consumed_date");
}
