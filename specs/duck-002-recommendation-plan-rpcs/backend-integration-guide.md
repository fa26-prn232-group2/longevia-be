# Backend Integration Guide — Recommendation plan RPCs

## 1. Summary

- Feature/spec: `specs/duck-002-recommendation-plan-rpcs/spec.md`
- Requirement: `docs/requirements/` (BR/PRD của Recommendation)
- Handoff status: **READY_FOR_BACKEND**
- Canonical contract:
  [`contracts/recommendation/v1/recommendation.proto`](../../contracts/recommendation/v1/recommendation.proto)
  — package `longevia.recommendation.v1`, version v1.
- Counts: **7 RPC · 30 message · 204 field · 3 enum/13 value · 14 error code · 15 warning code**.

Guide này chỉ ghi cách tích hợp và delta của feature; contract canonical vẫn là
file `.proto` và `docs/api/api-spec.md` §5. Recommendation là service nội bộ,
gRPC-only, không route qua Nginx.

## 2. Consumers and transport

| Producer | Consumer | Transport | Canonical contract | Sync/async | Network boundary |
|---|---|---|---|---|---|
| Recommendation | Diet | gRPC | `recommendation.proto` | sync | `internal_net`, cổng `50051` |
| Recommendation | Progress | gRPC | `recommendation.proto` | sync | `internal_net`, cổng `50051` |
| Recommendation | service nội bộ khác (nếu có) | gRPC | `recommendation.proto` | sync | `internal_net`, cổng `50051` |

Recommendation **không** publish cổng ra host và **không** đi qua Nginx. Nếu gọi
từ máy dev, dùng service chạy ngoài compose hoặc một container cùng network
(`internal_net` / `--network container:longevia-recommendation-1`).

## 3. gRPC

### 3.1 Method matrix

| Service | Method | Request | Response | Status | Caller hiện tại | Deadline |
|---|---|---|---|---|---|---|
| `RecommendationService` | `GetMealRecommendations` | `MealRecommendationRequest` | `MealRecommendationResponse` | existing | Diet (theo `docs/api/api-spec.md` §3.7) | 30s |
| `RecommendationService` | `AnalyzeNutrientGaps` | `NutrientGapRequest` | `NutrientGapResponse` | existing | chưa có caller trong code hiện tại | 30s |
| `RecommendationService` | `EvaluateLongevityHabits` | `LongevityHabitRequest` | `LongevityHabitResponse` | existing | chưa có caller trong code hiện tại | 30s |
| `RecommendationService` | `AskLongevityQuestion` | `AskLongevityQuestionRequest` | `AskLongevityQuestionResponse` | existing | chưa có caller trong code hiện tại | 30s |
| `RecommendationService` | `SearchLongevityKnowledge` | `SearchKnowledgeRequest` | `SearchKnowledgeResponse` | existing | chưa có caller trong code hiện tại | 30s |
| `RecommendationService` | `GenerateMealPlan` | `GenerateMealPlanRequest` | `GenerateMealPlanResponse` | new (duck-002) | Diet | 90s |
| `RecommendationService` | `GenerateWorkoutPlan` | `GenerateWorkoutPlanRequest` | `GenerateWorkoutPlanResponse` | new (duck-002) | Progress | 90s |

### 3.2 Call behavior

- Address trong compose: `http://recommendation:50051` (HTTP/2 h2c).
- Auth: không xác thực caller ở tầng gRPC; bảo vệ bằng network nội bộ và
  policy của caller (ADR 001 §2.2).
- Deadline server: 30s cho 5 RPC cũ, 90s cho 2 RPC plan. Caller vẫn phải đặt
  deadline phía client thấp hơn hoặc bằng ngân sách của mình.
- Retry: chỉ retry lỗi transport `UNAVAILABLE`/`DEADLINE_EXCEEDED` có kiểm soát;
  không retry mù `INVALID_ARGUMENT`/`FAILED_PRECONDITION`.
- Idempotency: 2 RPC plan là read-only với corpus, không lưu kế hoạch; gọi lại
  cùng request tạo lại kế hoạch, không có side effect DB.
- Reflection: **disabled**. Client C# dùng generated client từ `Grpc.Tools`;
  `grpcurl` phải truyền `-proto` và `-import-path`.

### 3.3 Errors and metadata

Lỗi trả qua `RpcException`; mã ổn định nằm ở trailer `code`, mô tả tiếng Việt ở
`Status.Detail`. Hai RPC plan dùng các mã sau:

| Code | gRPC status | Điều kiện |
|---|---|---|
| `recommendation.missing_targets` | `INVALID_ARGUMENT` | Thiếu target calo hoặc protein/carbs/fat |
| `recommendation.invalid_targets` | `INVALID_ARGUMENT` | Target calo/macro/fiber âm hoặc không hợp lệ |
| `recommendation.invalid_consumed_calories` | `INVALID_ARGUMENT` | `consumed_calories` âm |
| `recommendation.invalid_week_start` | `INVALID_ARGUMENT` | `week_start_date` sai ISO hoặc không phải thứ Hai |
| `recommendation.invalid_days` | `INVALID_ARGUMENT` | `days` ngoài 1..7 |
| `recommendation.invalid_meals_per_day` | `INVALID_ARGUMENT` | `meals_per_day` ngoài 1..4 |
| `recommendation.invalid_profile` | `INVALID_ARGUMENT` | Profile thiếu/sai trường bắt buộc |
| `recommendation.invalid_goal` | `INVALID_ARGUMENT` | `goal = GOAL_UNSPECIFIED` hoặc không hợp lệ |
| `recommendation.invalid_candidate_food` | `INVALID_ARGUMENT` | Món thiếu `food_id`/`name` hoặc dinh dưỡng âm |
| `recommendation.invalid_workout_params` | `INVALID_ARGUMENT` | `sessions_per_week`/`minutes_per_session` ngoài khoảng hoặc `sessions_per_week > days` |
| `recommendation.missing_consumed_date` | `INVALID_ARGUMENT` | `consumed_calories > 0` nhưng thiếu `consumed_date` |
| `recommendation.invalid_consumed_date` | `INVALID_ARGUMENT` | `consumed_date` sai ISO |
| `recommendation.no_candidate_food` | `FAILED_PRECONDITION` | `candidate_foods` rỗng |
| `recommendation.all_candidates_excluded` | `FAILED_PRECONDITION` | Sau lọc allergen/dietary/`excluded_food_ids` không còn món |

Metadata response plan:

- `GenerateMealPlanResponse`: `week_start_date`, `days_requested`,
  `weekly_target_calories`, `weekly_target_protein_grams`,
  `weekly_target_carbs_grams`, `weekly_target_fat_grams`,
  `weekly_target_fiber_grams`, `used_fallback`, `citations`, `warnings`,
  `disclaimer`.
- `GenerateWorkoutPlanResponse`: `week_start_date`, `days_requested`,
  `sessions_scheduled`, `minutes_per_session`, `used_fallback`, `citations`,
  `warnings`, `disclaimer`.

Warning plan (`PlanWarning.code`), không làm RPC thất bại:

| Code | Scope | Khi nào |
|---|---|---|
| `corpus_not_loaded` | plan | Không truy xuất được chunk nào |
| `ai_fallback_used` | plan | AI lỗi/timeout/kết quả không hợp lệ nên dùng fallback |
| `target_not_met` | plan/ngày | Candidate không đủ để đạt ±10% ngân sách ngày |
| `fiber_below_target` | ngày | Tổng fiber ngày thấp hơn target fiber caller gửi |
| `candidate_foods_truncated` | plan | Cắt `candidate_foods` > 120 |
| `duplicate_candidate_food` | plan | Trùng `food_id` trong `candidate_foods` |
| `unsupported_locale` | plan | `locale` khác `vi-VN` |
| `consumed_date_outside_plan` | plan | `consumed_date` ngoài tuần nên bỏ rule 105% |
| `suggestion_source_missing` | ngày | Ngày chạm trần nhưng không có căn cứ sách cho `suggestions` |
| `no_limitations_assumed` | plan | `limitations` rỗng; chỉ dùng hướng dẫn chung |
| `unsupported_activity_preference` | plan | Preference không có trong catalog hoặc không khả thi |
| `no_safe_activity` | ngày | Limitations/thiết bị loại hết hoạt động an toàn |
| `unsupported_limitation` | plan | Limitation không khớp bảng ánh xạ nên bị bỏ qua |
| `intensity_unverified` | ngày | Không có câu chương 5 hỗ trợ cường độ nên `intensity` rỗng |
| `activity_source_unverified` | plan/ngày | Corpus rỗng; hoạt động/thời lượng chỉ lấy từ request |

Chi tiết đầy đủ cho 5 RPC cũ: `docs/api/api-spec.md` §5.

## 4. Events

Feature này **không** thêm/sửa/xóa event payload hay topology RabbitMQ.
`N/A` cho phạm vi duck-002.

## 5. Internal HTTP

`N/A`. Recommendation không expose REST; các service không gọi nhau bằng HTTP
trực tiếp (AGENTS.md §3).

## 6. Compatibility and rollout

- Contract v1 giữ nguyên 5 RPC cũ; chỉ thêm 2 RPC và message mới.
- Field number đã phát hành không đổi; field mới dùng số lớn hơn cuối cùng.
- Không có migration/bảng mới; corpus chỉ đọc.
- Rollout: deploy Recommendation trước, sau đó Diet/Progress bật caller mới.
  Caller cũ không bị ảnh hưởng.

## 7. Test evidence

- Contract/integration: `src/Services/Recommendation/tests/Recommendation.IntegrationTests`
  — gRPC server thật + PostgreSQL Testcontainers; `GenerateMealPlanGrpcTests`,
  `GenerateWorkoutPlanGrpcTests`, `MealPlanCitationGrpcTests`.
- Unit: `src/Services/Recommendation/tests/Recommendation.UnitTests` — use case,
  validator, fallback, suggestion/citation.
- Kết quả gần nhất: 372 unit + 64 integration pass; `make lint` và
  `make check` xanh.
- Manual smoke (không dùng reflection):

```powershell
# Chạy từ repo root, service Recommendation đang chạy ngoài compose.
grpcurl -plaintext -import-path . `
  -proto contracts/recommendation/v1/recommendation.proto `
  describe longevia.recommendation.v1.RecommendationService
```

Nếu service chạy trong compose (cổng 50051 không publish ra host), dùng container
cùng network namespace:

```powershell
@'
{
  "goal": "LONGEVITY",
  "weekStartDate": "2026-10-26",
  "days": 3,
  "sessionsPerWeek": 2,
  "minutesPerSession": 30,
  "activityPreference": ["walking"],
  "equipmentAvailable": [],
  "limitations": [],
  "profile": {
    "age": 35,
    "sex": "FEMALE",
    "heightCm": 165,
    "weightKg": 60,
    "activityLevel": "LightlyActive",
    "dietaryPreference": "None",
    "allergies": []
  },
  "locale": "vi-VN"
}
'@ | docker run --rm -i `
  --network container:longevia-recommendation-1 `
  -v "${PWD}:/work:ro" -w /work `
  fullstorydev/grpcurl `
  -plaintext -import-path . `
  -proto contracts/recommendation/v1/recommendation.proto `
  -d @ localhost:50051 `
  longevia.recommendation.v1.RecommendationService/GenerateWorkoutPlan
```

Call plan RPC cần `GOOGLE_AI__API_KEY` trong service để gọi provider; nếu provider
lỗi, response vẫn trả kế hoạch fallback kèm `warnings` và `used_fallback = true`.

Đã smoke bằng `grpcurl` với `-proto` (không reflection): `describe` trả đủ 7 RPC;
gọi `GenerateWorkoutPlan` với `{}` trả `InvalidArgument`, trailer
`code: recommendation.invalid_profile`, xác nhận network namespace và contract
load đúng.

### Ví dụ `GenerateMealPlan` (1 ngày, 2 bữa)

```json
{
  "profile": {
    "age": 35,
    "sex": "FEMALE",
    "heightCm": 165,
    "weightKg": 60,
    "activityLevel": "LightlyActive",
    "dietaryPreference": "None",
    "allergies": []
  },
  "targetCalories": 1800,
  "targetProteinGrams": 90,
  "targetCarbsGrams": 220,
  "targetFatGrams": 60,
  "targetFiberGrams": 25,
  "weekStartDate": "2026-10-26",
  "days": 1,
  "mealsPerDay": 2,
  "candidateFoods": [
    {
      "foodId": "f-oats",
      "name": "Yến mạch",
      "servingSize": "50",
      "servingUnit": "g",
      "calories": 190,
      "proteinGrams": 6,
      "carbsGrams": 34,
      "fatGrams": 4,
      "fiberGrams": 5,
      "allergens": []
    },
    {
      "foodId": "f-salmon",
      "name": "Cá hồi",
      "servingSize": "120",
      "servingUnit": "g",
      "calories": 250,
      "proteinGrams": 26,
      "carbsGrams": 0,
      "fatGrams": 15,
      "fiberGrams": 0,
      "allergens": ["fish"]
    }
  ],
  "locale": "vi-VN"
}
```

## 8. Sync checklist (fill counts before closing the feature)

- [x] Every RPC in the canonical proto appears above with request/response
      (count: 7)
- [x] Every documented message field and enum value compared against the
      canonical proto in both directions (count: 30 messages, 204 fields,
      3 enums/13 values)
- [x] Every error code/trailer and deadline compared against implementation
      (count: 14 error codes; 30s/90s)
- [x] Every event, routing key, queue and producer/consumer pair compared
      against canonical contract/topology (count: 0 — feature không đổi event)
- [x] Every claimed test evidence was actually run and its result recorded
      (count: 3 integration test classes + 372 unit/64 integration pass)
