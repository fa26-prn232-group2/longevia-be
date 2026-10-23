# Mô hình dữ liệu — Kế hoạch ăn uống và tập luyện theo tuần

**Feature**: `duck/002/recommendation-plan-rpcs` | **Date**: 2026-10-23
**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

## 1. Phạm vi lưu trữ

Feature này **không tạo bảng, không tạo migration và không lưu dữ liệu người
dùng**. Toàn bộ cấu trúc dưới đây là DTO trong request/response hoặc model nội
bộ tồn tại trong một lời gọi.

`recommendation_db` giữ nguyên schema `kb.*` của `duck-001-recommendation-rag`;
service chỉ đọc corpus qua `IKnowledgeStore`. Không có entity/table nào cho
`MealPlan`, `WorkoutPlan`, profile hay lịch sử.

```text
Request gRPC ──► Api mapper ──► Application use case ──► Domain helpers
                                      │
                                      ├── IKnowledgeStore (chỉ đọc kb.*)
                                      ├── IChatModel (chọn chỉ số/citation)
                                      └── Response DTO ──► Api mapper ──► gRPC
```

---

## 2. Request DTO — `GenerateMealPlan`

Nguồn: [contracts/recommendation-plan.proto](./contracts/recommendation-plan.proto)
và FR-038/FR-039.

| Trường | Kiểu | Bắt buộc | Quy tắc |
| --- | --- | --- | --- |
| `profile` | `UserProfile` | Có | `age` 1..120; `sex` khác `SEX_UNSPECIFIED`; `height_cm` > 0; `weight_kg` > 0; `activity_level` thuộc tập chuỗi `Sedentary/LightlyActive/ModeratelyActive/VeryActive`; `dietary_preference` thuộc tập chuỗi `None/Keto/Vegan/Mediterranean/LowCarb`; `allergies` có thể rỗng |
| `target_calories` | `int32` | Có | > 0; là **mục tiêu ngày** |
| `target_protein_grams` | `double` | Có | >= 0; là **mục tiêu ngày** |
| `target_carbs_grams` | `double` | Có | >= 0; là **mục tiêu ngày** |
| `target_fat_grams` | `double` | Có | >= 0; là **mục tiêu ngày** |
| `target_fiber_grams` | `double` | Không | 0 = không gửi; > 0 là mục tiêu ngày; âm → `invalid_targets` |
| `week_start_date` | `string` | Có | ISO `YYYY-MM-DD`, phải là thứ Hai |
| `days` | `int32` | Có | 1..7 |
| `meals_per_day` | `int32` | Có | 1..4 |
| `candidate_foods` | `repeated CandidateFood` | Có | >= 1; nếu > 120 thì cắt theo FR-025, không phải lỗi; mỗi món có `food_id`/`name` khác rỗng; calo/macro/fiber >= 0 |
| `excluded_food_ids` | `repeated string` | Không | Món trùng bị loại khỏi mọi ngày |
| `consumed_calories` | `int32` | Không | >= 0; > 0 thì bắt buộc có `consumed_date`; âm → `invalid_consumed_calories` |
| `consumed_date` | `string` | Không | ISO `YYYY-MM-DD`; nếu có phải hợp lệ; rule 105% chỉ áp nếu nằm trong tuần |
| `locale` | `string` | Không | Rỗng → `vi-VN`; không hỗ trợ → `vi-VN` + warning |

**Ràng buộc chéo**:

- `target_protein_grams`, `target_carbs_grams`, `target_fat_grams` không được
  đồng thời bằng 0.
- `consumed_calories > 0` mà thiếu `consumed_date` → lỗi
  `recommendation.missing_consumed_date`.
- `consumed_date` sai định dạng → lỗi `recommendation.invalid_consumed_date`.
- `consumed_date` ngoài `[week_start_date, week_start_date + days - 1]` → bỏ
  qua rule 105%, warning `consumed_date_outside_plan`.
- `target_fiber_grams` âm → lỗi `recommendation.invalid_targets`;
  `consumed_calories` âm → lỗi `recommendation.invalid_consumed_calories`.

**CandidateFood tái sử dụng**: `food_id`, `name`, `serving_size`,
`serving_unit`, `calories`, `protein_grams`, `carbs_grams`, `fat_grams`,
`fiber_grams`, `allergens` (danh sách đã chuẩn hoá lowercase theo contract hiện
có; service vẫn chuẩn hoá lại trim/lowercase/NFC trước khi so khớp). `allergens`
rỗng = caller khai không có allergen đã biết.

---

## 3. Request DTO — `GenerateWorkoutPlan`

Nguồn: FR-040.

| Trường | Kiểu | Bắt buộc | Quy tắc |
| --- | --- | --- | --- |
| `profile` | `UserProfile` | Có | Như mục 2 |
| `goal` | `UserGoal` | Có | Khác `GOAL_UNSPECIFIED` |
| `week_start_date` | `string` | Có | ISO `YYYY-MM-DD`, thứ Hai |
| `days` | `int32` | Có | 1..7 |
| `sessions_per_week` | `int32` | Có | 0 → `min(3, days)`; giá trị khác 1..7; không lớn hơn `days` |
| `minutes_per_session` | `int32` | Có | 0 → mặc định 30; giá trị khác 10..120 |
| `activity_preference` | `repeated string` | Có (có thể rỗng) | Chuẩn hoá lowercase; giá trị lạ bị bỏ + warning |
| `equipment_available` | `repeated string` | Có (có thể rỗng) | Rỗng = không có thiết bị; chỉ dùng hoạt động không cần thiết bị |
| `limitations` | `repeated string` | Không | Rỗng = caller chưa cung cấp; không giả định khỏe mạnh/chấn thương |
| `locale` | `string` | Không | Như mục 2 |

**Ràng buộc chéo**:

- `sessions_per_week > days` → `recommendation.invalid_workout_params`.
- `sessions_per_week` ngoài 0..7 hoặc `minutes_per_session` khác 0 và ngoài
  `10..120` → cùng mã lỗi trên.
- `activity_preference` rỗng là hợp lệ: service dùng hướng dẫn chung chương 5.
- `limitations` rỗng là hợp lệ: chỉ dùng hướng dẫn chung + warning
  `no_limitations_assumed`.

---

## 4. Response DTO — `GenerateMealPlanResponse`

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `days` | `repeated MealPlanDay` | Đúng `days` ngày, ngày liên tiếp từ `week_start_date` |
| `week_start_date` | `string` | Ngày bắt đầu tuần (ISO) |
| `days_requested` | `int32` | Số ngày caller yêu cầu |
| `weekly_target_calories` | `int32` | Tổng mục tiêu calo tuần = target ngày × `days` |
| `weekly_target_protein_grams` | `double` | Tổng mục tiêu protein tuần |
| `weekly_target_carbs_grams` | `double` | Tổng mục tiêu carbs tuần |
| `weekly_target_fat_grams` | `double` | Tổng mục tiêu fat tuần |
| `weekly_target_fiber_grams` | `double` | Tổng mục tiêu fiber tuần; 0 nếu không gửi |
| `citations` | `repeated Citation` | Hợp nhất citation đã verify từ chương 4 + chương 12 có `book_page_from >= 238` (Phụ lục A); `suggestions` cap-day có thể thêm chương 11 cho đoạn nước/trà thảo mộc không đường; rỗng nếu corpus rỗng |
| `warnings` | `repeated PlanWarning` | Cảnh báo cấp plan/ngày |
| `disclaimer` | `Disclaimer` | Luôn có |
| `used_fallback` | `bool` | `true` nếu đã dùng fallback xác định |

### 4.1 `MealPlanDay`

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `date` | `string` | ISO `YYYY-MM-DD` |
| `target_calories` | `int32` | Ngân sách calo ngày |
| `target_protein_grams` | `double` | Ngân sách protein ngày |
| `target_carbs_grams` | `double` | Ngân sách carbs ngày |
| `target_fat_grams` | `double` | Ngân sách fat ngày |
| `target_fiber_grams` | `double` | Ngân sách fiber ngày; 0 nếu không gửi |
| `total_calories` | `int32` | Tổng từ các món đã chọn |
| `total_protein_grams` | `double` | Tổng từ các món |
| `total_carbs_grams` | `double` | Tổng từ các món |
| `total_fat_grams` | `double` | Tổng từ các món |
| `total_fiber_grams` | `double` | Tổng từ các món |
| `meals` | `repeated PlannedMeal` | Rỗng nếu ngày chạm trần 105% |
| `suggestions` | `repeated PlanSuggestion` | Đồ uống 0 calo/thói quen khi chạm trần; tối đa 3 |
| `calorie_cap_reached` | `bool` | `true` nếu ngày bị rule 105% |

### 4.2 `PlannedMeal`

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `meal_type` | `MealType` | `BREAKFAST/LUNCH/DINNER/SNACK` |
| `target_calories` | `int32` | Ngân sách calo bữa |
| `foods` | `repeated PlannedFood` | Tối đa 5 món, đều thuộc candidate |
| `rationale` | `string` | Do code dựng từ request + citation; không phải AI tự do |
| `citations` | `repeated Citation` | Citation đã verify cho rationale; có thể rỗng |

### 4.3 `PlannedFood`

Sao chép **nguyên giá trị** từ `CandidateFood`: `food_id`, `name`,
`serving_size`, `serving_unit`, `calories`, `protein_grams`, `carbs_grams`,
`fat_grams`, `fiber_grams`, `allergens`.

### 4.4 `PlanSuggestion`

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `text` | `string` | Nội dung gợi ý đồ uống 0 calo/thói quen |
| `citations` | `repeated Citation` | Căn cứ sách; rỗng nếu không có nguồn |

`PlanSuggestion` **không** có `food_id`; đây không phải món trong danh mục.

### 4.5 `PlanWarning`

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `code` | `string` | Mã ổn định (xem §7) |
| `message` | `string` | Mô tả tiếng Việt ngắn gọn |
| `scope` | `string` | `plan` hoặc ngày ISO `YYYY-MM-DD` |

---

## 5. Response DTO — `GenerateWorkoutPlanResponse`

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `days` | `repeated WorkoutPlanDay` | Đúng `days` ngày, liên tiếp từ `week_start_date` |
| `week_start_date` | `string` | Ngày bắt đầu tuần (ISO) |
| `days_requested` | `int32` | Số ngày caller yêu cầu |
| `sessions_scheduled` | `int32` | Số buổi thực tế sau khi áp mặc định/giới hạn an toàn |
| `minutes_per_session` | `int32` | Thời lượng mỗi buổi |
| `citations` | `repeated Citation` | Hợp nhất citation đã verify từ chương 5; rỗng nếu corpus rỗng |
| `warnings` | `repeated PlanWarning` | Cảnh báo cấp plan/ngày |
| `disclaimer` | `Disclaimer` | Luôn có |
| `used_fallback` | `bool` | `true` nếu đã dùng skeleton/fallback |

### 5.1 `WorkoutPlanDay`

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `date` | `string` | ISO `YYYY-MM-DD` |
| `rest_day` | `bool` | `true` = ngày nghỉ |
| `activity` | `string` | Key ổn định từ catalog (`walking`, `cycling`, ...), không phải tên hiển thị; rỗng nếu `rest_day` |
| `duration_minutes` | `int32` | Bằng `minutes_per_session`; 0 nếu `rest_day` |
| `intensity` | `string` | Cường độ có căn cứ chương 5; rỗng nếu không có căn cứ |
| `equipment_used` | `repeated string` | Thiết bị đã dùng; rỗng nếu không cần |
| `rationale` | `string` | Do code dựng từ request + citation |
| `citations` | `repeated Citation` | Citation đã verify; rỗng nếu `rest_day`/corpus rỗng |

**Bất biến**:

- `rest_day = true` → `activity` rỗng, `duration_minutes = 0`,
  `equipment_used` rỗng, `citations` rỗng.
- `rest_day = false` → `activity` khác rỗng, `duration_minutes` bằng
  `minutes_per_session`, có ít nhất một citation **trừ** chế độ degraded khi
  corpus rỗng (khi đó warning `activity_source_unverified`).

---

## 6. Model nội bộ (Application/Domain)

| Model | Vai trò | Có lưu? |
| --- | --- | --- |
| `GenerateMealPlanRequest` | DTO Application của `GenerateMealPlan` | Không |
| `GenerateMealPlanResult` | Kết quả use case | Không |
| `MealPlanDay` | Ngày + ngân sách + bữa + suggestion | Không |
| `PlannedMeal` | Bữa + món + rationale + citation | Không |
| `PlannedFood` | Món sao chép từ candidate | Không |
| `PlanSuggestion` | Gợi ý thay thế 0 calo/thói quen | Không |
| `PlanWarning` | Mã + message + scope | Không |
| `WeeklyTargets` | Mục tiêu ngày + tổng tuần đã tính | Không |
| `WeeklyBudget` | Ngân sách calo/macro/fiber đã chia cho từng ngày và bữa (chứa nested `DayBudget`/`MealBudget`) | Không |
| `DayBudget` | Ngân sách một ngày + danh sách `MealBudget` (nested trong `WeeklyBudget`) | Không |
| `MealBudget` | Ngân sách một bữa + loại bữa (nested trong `WeeklyBudget`) | Không |
| `GenerateWorkoutPlanRequest` | DTO Application của `GenerateWorkoutPlan` | Không |
| `GenerateWorkoutPlanResult` | Kết quả use case | Không |
| `WorkoutPlanDay` | Ngày tập/nghỉ | Không |
| `WorkoutActivity` | Hoạt động trong catalog đóng | Không (constant/catalog) |
| `Citation` | Trích dẫn đã verify | Không (response) |
| `UserProfile`, `CandidateFood` | Dữ liệu caller | Không |

---

## 7. Mã warning ổn định

| Mã | Khi nào | Scope |
| --- | --- | --- |
| `corpus_not_loaded` | Không truy xuất được chunk nào | `plan` |
| `ai_fallback_used` | AI lỗi, timeout hoặc kết quả không hợp lệ | `plan` |
| `target_not_met` | Candidate không đủ để đạt ±10% | `plan`/ngày |
| `fiber_below_target` | Tổng fiber ngày thấp hơn mục tiêu fiber (target > 0) | ngày |
| `candidate_foods_truncated` | Cắt candidate > 120 | `plan` |
| `duplicate_candidate_food` | Trùng `food_id` trong candidate | `plan` |
| `unsupported_locale` | Locale không hỗ trợ | `plan` |
| `consumed_date_outside_plan` | `consumed_date` ngoài tuần | `plan` |
| `suggestion_source_missing` | Chạm trần nhưng không có căn cứ sách cho suggestion | ngày |
| `no_limitations_assumed` | `limitations` rỗng | `plan` |
| `unsupported_activity_preference` | Preference không có trong catalog/không khả thi | `plan` |
| `no_safe_activity` | Limitations/thiết bị loại hết hoạt động của một ngày | ngày |
| `unsupported_limitation` | Limitation không khớp bảng ánh xạ đóng | `plan` |
| `intensity_unverified` | Không có câu chương 5 hỗ trợ cường độ | ngày |
| `activity_source_unverified` | Corpus rỗng, dùng hoạt động caller gửi | `plan`/ngày |

---

## 8. Mã lỗi nghiệp vụ

Chi tiết mapping gRPC ở [research.md](./research.md) D-11.

| Mã | Điều kiện |
| --- | --- |
| `recommendation.missing_targets` | Thiếu `target_calories` hoặc thiếu target protein/carbs/fat |
| `recommendation.invalid_targets` | Target calo/macro/fiber âm hoặc không hợp lệ |
| `recommendation.invalid_consumed_calories` | `consumed_calories` âm |
| `recommendation.no_candidate_food` | `candidate_foods` rỗng |
| `recommendation.invalid_week_start` | Sai định dạng hoặc không phải thứ Hai |
| `recommendation.invalid_days` | `days` ngoài 1..7 |
| `recommendation.invalid_meals_per_day` | `meals_per_day` ngoài 1..4 |
| `recommendation.invalid_profile` | Profile thiếu/sai trường bắt buộc |
| `recommendation.invalid_goal` | `goal = GOAL_UNSPECIFIED`/không hợp lệ |
| `recommendation.invalid_candidate_food` | Món thiếu `food_id`/`name` hoặc dinh dưỡng âm |
| `recommendation.all_candidates_excluded` | Sau lọc allergen/dietary/excluded không còn món |
| `recommendation.invalid_workout_params` | Sessions/minutes ngoài khoảng hoặc sessions > days |
| `recommendation.missing_consumed_date` | `consumed_calories > 0` nhưng thiếu `consumed_date` |
| `recommendation.invalid_consumed_date` | `consumed_date` sai định dạng ISO |

---

## 9. Quy tắc nghiệp vụ trên dữ liệu

| # | Quy tắc | Nơi thực thi |
| --- | --- | --- |
| R1 | Mọi `PlannedFood.food_id` thuộc `candidate_foods` | Parser + fallback |
| R2 | Calo/macro/fiber/allergen sao chép nguyên từ candidate | Parser + mapper |
| R3 | Lọc allergen 100% trước khi gọi AI | `PlanAllergenFilter` |
| R4 | Lọc dietary preference trước khi gọi AI | `DietaryPreferenceFilter` hiện có |
| R5 | Loại `excluded_food_ids` khỏi mọi ngày | `MealPlanCandidateFilter` |
| R6 | Tổng ngân sách ngày = mục tiêu ngày; tổng bữa = ngân sách ngày | `WeeklyBudgetSplitter` |
| R7 | Ngày chạm trần 105% có `meals` rỗng, `suggestions` riêng | `GenerateMealPlanUseCase` + `MealPlanSuggestionBuilder` |
| R8 | Ngày thường có đúng `meals_per_day` bữa, mỗi bữa <= 5 món | Parser + fallback |
| R9 | Calo/macro ngày trong ±10% ngân sách ngày khi khả thi | `MealPlanValidator` |
| R10 | Workout có đúng `sessions_per_week` buổi khi còn hoạt động an toàn; nếu không, rest day + warning | `WorkoutPlanSkeletonBuilder` |
| R11 | Workout activity/duration/intensity chỉ từ catalog chương 5 + caller | `WorkoutActivityCatalog`, parser |
| R12 | Citation `quote` phải khớp chunk nguồn | `CitationResolver.ResolveOptional` |
| R13 | Plan-level citation là hợp nhất citation đã verify | Use case |
| R14 | Không ghi DB, không log PII | Toàn bộ use case |

---

## 10. Dung lượng và giới hạn

| Thành phần | Giới hạn |
| --- | --- |
| `days` | 1..7 |
| `meals_per_day` | 1..4 |
| Món/bữa | 0..5 |
| `candidate_foods` | >= 1; nếu > 120 thì cắt theo macro-fit |
| `sessions_per_week` | 0 → `min(3, days)`; giá trị khác 1..7, <= `days` |
| `minutes_per_session` | 0 → 30; giá trị khác 10..120 |
| Citation/response | Hợp nhất, không giới hạn cứng; thực tế <= số chunk truy xuất |
| Corpus | 188 chunk hiện có; không đổi |

---

## 11. Migration

**Không có migration.** Feature không thay đổi `KnowledgeDbContext`, không thêm
entity, không sửa `Migrations/**`. `recommendation_db` tiếp tục chỉ chứa
`kb.corpus`, `kb.chapter`, `kb.chunk`, `kb.page_flag`.
