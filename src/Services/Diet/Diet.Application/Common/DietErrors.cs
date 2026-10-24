using Longevia.Shared.Errors;

namespace Diet.Application.Common;

public static class DietErrors
{
    public static AppException MissingTarget() =>
        AppException.NotFound("Bạn chưa thiết lập mục tiêu dinh dưỡng.", "diet.target_not_found");
    public static AppException MissingFood() =>
        AppException.NotFound("Có thực phẩm không tồn tại hoặc không được phép sử dụng.", "diet.food_not_found");
}
