using Longevia.Shared.Errors;

namespace Progress.Application.Common;

public static class ProgressErrors
{
    public static AppException ActiveFastingSessionExists() =>
        AppException.Conflict("Bạn đang có một phiên nhịn ăn chưa kết thúc.", "progress.fasting_active_conflict");

    public static AppException NoActiveFastingSession() =>
        AppException.NotFound("Không tìm thấy phiên nhịn ăn đang hoạt động.", "progress.fasting_no_active");

    public static AppException FastingSessionNotFound() =>
        AppException.NotFound("Phiên nhịn ăn không tồn tại.", "progress.fasting_not_found");

    public static AppException FastingEndConflict() =>
        AppException.Conflict("Phiên nhịn ăn đã kết thúc với thời điểm khác.", "progress.fasting_end_conflict");

    public static AppException FastingEndedBeforeStarted() =>
        AppException.BadRequest("Thời điểm kết thúc không được trước thời điểm bắt đầu.", "progress.fasting_invalid_time");

    public static AppException FutureTimestampNotAllowed() =>
        AppException.BadRequest("Thời gian không được ở tương lai.", "progress.invalid_timestamp");

    public static AppException InvalidProtocol(string protocol) =>
        AppException.BadRequest($"Giao thức nhịn ăn '{protocol}' không hợp lệ.", "progress.fasting_invalid_protocol");

    public static AppException TargetMismatch(string protocol, int requiredHours) =>
        AppException.BadRequest($"Giao thức {protocol} yêu cầu targetHours là {requiredHours}.", "progress.fasting_target_mismatch");

    public static AppException CustomTargetOutOfBounds() =>
        AppException.BadRequest("Giao thức Custom yêu cầu targetHours từ 1 đến 24 giờ.", "progress.fasting_custom_target_out_of_bounds");

    public static AppException WaterAmountInvalid() =>
        AppException.BadRequest("Lượng nước phải từ 1 đến 5000 ml.", "progress.water_amount_invalid");

    public static AppException SleepWakeBeforeSleep() =>
        AppException.BadRequest("Thời điểm thức dậy phải sau thời điểm đi ngủ.", "progress.sleep_invalid_time");

    public static AppException SleepDurationExceeded() =>
        AppException.BadRequest("Thời gian ngủ không được vượt quá 24 giờ.", "progress.sleep_duration_exceeded");

    public static AppException SleepOverlap() =>
        AppException.Conflict("Thời gian ngủ bị trùng lặp với bản ghi khác.", "progress.sleep_overlap");

    public static AppException SleepQualityInvalid() =>
        AppException.BadRequest("Chất lượng giấc ngủ không hợp lệ.", "progress.sleep_quality_invalid");

    public static AppException WeightOutOfBounds() =>
        AppException.BadRequest("Cân nặng phải từ 25.0 đến 300.0 kg.", "progress.weight_out_of_bounds");

    public static AppException BiomarkerOutOfBounds(string field) =>
        AppException.BadRequest($"Chỉ số {field} nằm ngoài giới hạn cho phép.", "progress.biomarker_out_of_bounds");

    public static AppException BloodPressurePairRequired() =>
        AppException.BadRequest("Huyết áp tâm thu và tâm trương phải được ghi nhận cùng lúc.", "progress.blood_pressure_pair_required");

    public static AppException SystolicMustExceedDiastolic() =>
        AppException.BadRequest("Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.", "progress.systolic_must_exceed_diastolic");

    public static AppException HeightUnavailable() =>
        AppException.Unprocessable("Không thể tính BMI do chưa có thông tin chiều cao trong hồ sơ.", "progress.height_unavailable");

    public static AppException IdentityServiceUnavailable() =>
        AppException.ServiceUnavailable("Dịch vụ hồ sơ người dùng (Identity) hiện không khả dụng để xác thực chiều cao.", "progress.identity_unavailable");

    public static AppException ProfileNotFound() =>
        AppException.NotFound("Không tìm thấy hồ sơ người dùng trong hệ thống Identity.", "progress.profile_not_found");
}
