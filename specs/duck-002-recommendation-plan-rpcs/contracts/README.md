# Hợp đồng gRPC bổ sung — Recommendation plan RPCs

**Feature**: `duck/002/recommendation-plan-rpcs` | **Date**: 2026-10-23

## 1. Trạng thái

Đây là **delta thiết kế**, không phải file compile trực tiếp.

- File đích: `contracts/recommendation/v1/recommendation.proto`.
- File này: `specs/duck-002-recommendation-plan-rpcs/contracts/recommendation-plan.proto`.
- Thay đổi là **additive**: thêm 2 RPC và các message mới; không đổi 5 RPC,
  message, field number hay tên field hiện có.
- Không tạo service mới; hai RPC nằm trong `RecommendationService` hiện có.
- Đây là service **nội bộ, gRPC-only**; không expose REST/Nginx.

## 2. Hai RPC cần merge vào service hiện có

```proto
service RecommendationService {
  // ... 5 RPC hiện có giữ nguyên ...

  // Sinh kế hoạch ăn uống theo tuần; chỉ chọn trong candidate_foods, lọc
  // allergen/dietary/excluded bằng code, fallback xác định khi provider lỗi.
  rpc GenerateMealPlan    (GenerateMealPlanRequest)    returns (GenerateMealPlanResponse);

  // Sinh kế hoạch tập luyện theo tuần; hoạt động/thời lượng/cường độ chỉ từ
  // chương 5 và giới hạn caller, fallback skeleton khi provider lỗi.
  rpc GenerateWorkoutPlan (GenerateWorkoutPlanRequest) returns (GenerateWorkoutPlanResponse);
}
```

## 3. Bất biến hợp đồng

| # | Bất biến | Nơi kiểm chứng |
| --- | --- | --- |
| C1 | `GenerateMealPlanRequest.target_*` là mục tiêu **ngày** | API validation + use case |
| C2 | `consumed_date` bắt buộc khi `consumed_calories > 0` | API validation |
| C3 | `consumed_date` chỉ áp rule 105% khi nằm trong tuần | Use case |
| C4 | `MealPlanDay.meals[].foods[].food_id` thuộc `candidate_foods` | Parser + fallback |
| C5 | `MealPlanDay.meals` rỗng khi `calorie_cap_reached = true` | Day builder |
| C6 | `PlanSuggestion` không có `food_id` | Contract + mapper |
| C7 | `days` đúng bằng request `days`; ngày liên tiếp | Use case |
| C8 | `WorkoutPlanDay.duration_minutes = minutes_per_session` khi không nghỉ | Use case |
| C9 | `WorkoutPlanDay.citations` rỗng khi `rest_day = true` | Use case |
| C10 | Mọi response thành công có `disclaimer` | Use case + mapper |
| C11 | Lỗi nghiệp vụ dùng trailer `code` với mã `recommendation.*` | `GrpcExceptionInterceptor` |
| C12 | Không có field nào lưu dữ liệu người dùng | Không có entity/migration |
| C13 | `candidate_foods` > 120 bị cắt theo macro-fit, không phải lỗi | Filter |
| C14 | Response có `week_start_date` + `days_requested`; meal có thêm `weekly_target_*` | Mapper |
| C15 | `sessions_per_week = 0` → `min(3, days)` | Validation |

## 4. Mã lỗi (trailer `code`)

| Mã | gRPC status | Khi nào |
| --- | --- | --- |
| `recommendation.missing_targets` | `InvalidArgument` | Thiếu target calo/macro |
| `recommendation.invalid_targets` | `InvalidArgument` | Target âm/không hợp lệ |
| `recommendation.invalid_consumed_calories` | `InvalidArgument` | `consumed_calories` âm |
| `recommendation.no_candidate_food` | `FailedPrecondition` | `candidate_foods` rỗng |
| `recommendation.invalid_week_start` | `InvalidArgument` | Không phải thứ Hai/sai ISO |
| `recommendation.invalid_days` | `InvalidArgument` | `days` ngoài 1..7 |
| `recommendation.invalid_meals_per_day` | `InvalidArgument` | Ngoài 1..4 |
| `recommendation.invalid_profile` | `InvalidArgument` | Profile thiếu/sai |
| `recommendation.invalid_goal` | `InvalidArgument` | Goal không hợp lệ |
| `recommendation.invalid_candidate_food` | `InvalidArgument` | Món thiếu trường/dinh dưỡng âm |
| `recommendation.all_candidates_excluded` | `FailedPrecondition` | Lọc hết món an toàn |
| `recommendation.invalid_workout_params` | `InvalidArgument` | Sessions/minutes sai hoặc sessions > days |
| `recommendation.missing_consumed_date` | `InvalidArgument` | Có calo đã ăn nhưng thiếu ngày |
| `recommendation.invalid_consumed_date` | `InvalidArgument` | Ngày sai ISO |

## 5. Mã warning (trong `PlanWarning.code`)

`corpus_not_loaded`, `ai_fallback_used`, `target_not_met`, `fiber_below_target`,
`candidate_foods_truncated`, `duplicate_candidate_food`, `unsupported_locale`,
`consumed_date_outside_plan`, `suggestion_source_missing`,
`no_limitations_assumed`, `unsupported_activity_preference`,
`unsupported_limitation`, `intensity_unverified`, `no_safe_activity`,
`activity_source_unverified`.

## 6. Quy trình merge

1. Mở `contracts/recommendation/v1/recommendation.proto`.
2. Thêm 2 dòng `rpc` vào `service RecommendationService` (giữ nguyên 5 RPC cũ).
3. Append toàn bộ message trong file delta này vào cuối file, sau các message
   hiện có.
4. Không đổi field number/tên của message cũ.
5. Chạy `make lint` để Grpc.Tools regenerate client/server không lỗi.
6. Chạy integration test hiện có của 5 RPC cũ để chứng minh không phá consumer.
7. Cập nhật `docs/api/api-spec.md` §5 và `docs/requirements/prd.md` §2.4.

## 7. Ghi chú tương thích

- `PlanWarning` và `PlanSuggestion` là message mới, không ảnh hưởng consumer cũ.
- `GenerateMealPlanRequest` dùng lại `CandidateFood` và `UserProfile`; không
  thêm field vào hai message này.
- `GenerateWorkoutPlanRequest` dùng lại `UserGoal` và `UserProfile`.
- Không có `oneof`, không có field `optional` của proto3; giá trị `0`/rỗng được
  quy ước là “không gửi” cho các trường tuỳ chọn đã nêu.
