# Tasks: Kế hoạch ăn uống và tập luyện theo tuần

**Input**: Design documents from `specs/duck-002-recommendation-plan-rpcs/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Bắt buộc theo Constitution §V và plan.md. Viết test trước, xác nhận
test **đỏ vì đúng lý do**, rồi mới implement.

**Organization**: Task nhóm theo user story để mỗi story có thể implement và
kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa xong)
- **[Story]**: `[US1]` = meal plan (P1), `[US2]` = workout plan (P2)
- Mọi task đều có đường dẫn file cụ thể (tương đối từ repo root)

## Path Conventions

- Service: `src/Services/Recommendation/`
- Unit test: `src/Services/Recommendation/tests/Recommendation.UnitTests/`
- Integration test: `src/Services/Recommendation/tests/Recommendation.IntegrationTests/`
- Contract: `contracts/recommendation/v1/recommendation.proto`
- Tài liệu feature: `specs/duck-002-recommendation-plan-rpcs/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Hợp đồng additive, cấu hình deadline, option, lỗi và schema dùng
chung cho cả hai story.

- [x] T001 Merge delta proto vào `contracts/recommendation/v1/recommendation.proto`: thêm 2 RPC `GenerateMealPlan` / `GenerateWorkoutPlan` và các message trong `specs/duck-002-recommendation-plan-rpcs/contracts/recommendation-plan.proto`; giữ nguyên 5 RPC/message/field number cũ.
- [x] T002 [P] Thêm `PlanDeadlineSeconds` (mặc định 90, hợp lệ 5..120) vào `src/Services/Recommendation/Recommendation.Infrastructure/Options/RecommendationOptions.cs`; giữ `DeadlineSeconds` mặc định 30 và thêm validate 5..120 trong `RecommendationStartup.cs`.
- [x] T003 [P] Viết test `RpcDeadlinePolicyTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/RpcDeadlinePolicyTests.cs`: 2 method plan → 90s, 5 method cũ → 30s; validate option 5..120; xác nhận đỏ trước T004.
- [x] T004 Tạo `RpcDeadlinePolicy` trong `src/Services/Recommendation/Recommendation.Api/Deadlines/RpcDeadlinePolicy.cs` ánh xạ method gRPC sang `PlanDeadlineSeconds`/`DeadlineSeconds`.
- [x] T005 Sửa `src/Services/Recommendation/Recommendation.Api/Middlewares/ServerDeadlineInterceptor.cs` để lấy deadline theo `context.Method` từ `RpcDeadlinePolicy`; giữ nguyên token/telemetry.
- [x] T006 [P] Thêm `RECOMMENDATION__PLAN_DEADLINE_SECONDS=90` (comment tiếng Anh) vào `.env.example` và truyền env trong `deployments/docker-compose.yml`.
- [x] T007 [P] Thêm `PlanWarningCodes` và `PlanDefaults` (tỷ lệ bữa, giới hạn 120 candidate, 5 món/bữa) vào `src/Services/Recommendation/Recommendation.Domain/Constants/PlanWarningCodes.cs` và `src/Services/Recommendation/Recommendation.Domain/Constants/PlanDefaults.cs`.
- [x] T008 [P] Thêm các factory lỗi plan vào `src/Services/Recommendation/Recommendation.Application/Common/Errors/RecommendationErrors.cs`: `missing_targets`, `invalid_targets`, `invalid_consumed_calories`, `invalid_week_start`, `invalid_days`, `invalid_meals_per_day`, `invalid_profile`, `invalid_goal`, `invalid_candidate_food`, `all_candidates_excluded`, `invalid_workout_params`, `missing_consumed_date`, `invalid_consumed_date`.
- [x] T009 [P] Thêm `MealPlanSelection` và `WorkoutPlanSelection` vào `src/Services/Recommendation/Recommendation.Domain/Models/StructuredResponseKind.cs`.
- [x] T010 Thêm schema JSON cho `MealPlanSelection` và `WorkoutPlanSelection` vào `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/GoogleAi/SchemaFactory.cs` (chỉ chỉ số món/hoạt động + citation; không rationale/số liệu).
- [x] T011 Chạy lại 5 RPC integration test cũ để chứng minh contract additive không phá consumer: `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Meals/GetMealRecommendationsGrpcTests.cs`, `.../Features/Ask/AskLongevityQuestionGrpcTests.cs`, `.../Features/NutrientGaps/AnalyzeNutrientGapsGrpcTests.cs`, `.../Features/LongevityHabits/EvaluateLongevityHabitsGrpcTests.cs`, `.../Features/Search/SearchLongevityKnowledgeGrpcTests.cs`.

**Checkpoint**: Contract + deadline + schema + lỗi dùng chung sẵn sàng.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Model/DTO dùng chung, validator ngày/target, prompt formatter,
citation resolver mở rộng và fixture test. **Không story nào bắt đầu trước khi
phase này xong.**

- [x] T012 [P] Thêm `PlanWarning` và `WeeklyBudget` (gồm nested `DayBudget`/`MealBudget`) vào `src/Services/Recommendation/Recommendation.Domain/Models/PlanWarning.cs` và `src/Services/Recommendation/Recommendation.Domain/Models/WeeklyBudget.cs`; tạo DTO skeleton request/result cho cả hai story trong `Recommendation.Application/Features/MealPlans/GenerateMealPlanRequest.cs`, `.../GenerateMealPlanResult.cs`, `.../Features/WorkoutPlans/GenerateWorkoutPlanRequest.cs`, `.../GenerateWorkoutPlanResult.cs` (T033/T054 sau đó hoàn thiện/kiểm tra field theo data-model).
- [x] T013 [P] Viết test `PlanRequestValidatorTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/PlanRequestValidatorTests.cs` cho: profile age 1..120, sex khác `SEX_UNSPECIFIED`, height/weight > 0, activity/dietary thuộc tập chuỗi, `goal` khác `GOAL_UNSPECIFIED`, target_calories > 0, macro >= 0 và không đồng thời 0, target/fiber âm → `invalid_targets`, week_start thứ Hai, days 1..7, meals_per_day 1..4, candidate >= 1 và >120 là cắt không phải lỗi, món thiếu `food_id`/`name` hoặc dinh dưỡng âm → `invalid_candidate_food`, sessions 0 → `min(3, days)`/1..7/<=days, minutes 0 → 30/10..120, consumed_calories >= 0 và consumed_date bắt buộc/đúng ISO, consumed âm → `invalid_consumed_calories`, locale chỉ `vi-VN`; xác nhận đỏ trước T015.
- [x] T014 [P] Viết test `PlanDateRangeTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/PlanDateRangeTests.cs` cho 1 ngày, 7 ngày liên tiếp, qua tháng/năm; xác nhận đỏ trước T016.
- [x] T015 Tạo `PlanRequestValidator` trong `src/Services/Recommendation/Recommendation.Application/Features/Plans/PlanRequestValidator.cs`, dùng `RecommendationErrors` và các ràng buộc đã liệt kê ở T013.
- [x] T016 Tạo `PlanDateRange` trong `src/Services/Recommendation/Recommendation.Application/Features/Plans/PlanDateRange.cs` trả danh sách ngày ISO liên tiếp từ `week_start_date`.
- [x] T017 [P] Viết `PlanPromptFormatterTests` (đỏ) trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/PlanPromptFormatterTests.cs`, rồi tạo `PlanPromptFormatter` trong `src/Services/Recommendation/Recommendation.Application/Features/Plans/PlanPromptFormatter.cs` để bọc chunk trong `[BEGIN CORPUS DATA]`/`[END CORPUS DATA]` và gắn VERIFIED/NEEDS_REVIEW.
- [x] T018 [P] Viết `PlanRationaleBuilderTests` và `PlanSafetyTests` (đỏ) trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/PlanRationaleBuilderTests.cs` và `.../PlanSafetyTests.cs`, rồi tạo `PlanRationaleBuilder` trong `src/Services/Recommendation/Recommendation.Application/Features/Plans/PlanRationaleBuilder.cs`: rationale chỉ từ dữ liệu request + món/hoạt động + quote đã verify; không nhận free text AI và không có medical advice.
- [x] T019 [P] Bổ sung case `ResolveOptional` vào `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Ask/CitationResolverTests.cs`: quote khớp giữ citation, quote sai bị loại, rỗng không abstain; xác nhận đỏ trước T020.
- [x] T020 Thêm `ResolveOptional` vào `src/Services/Recommendation/Recommendation.Application/Features/Ask/CitationResolver.cs`, dùng lại `NormalizeForMatch`, trả danh sách rỗng thay vì `Abstained`.
- [x] T021 [P] Cập nhật `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Meals/RecommendationApiFixture.cs`: double tất định trả `MealPlanSelection` và `WorkoutPlanSelection`, giữ nguyên hành vi 5 kind cũ.
- [x] T022 Đăng ký `RpcDeadlinePolicy`, `PlanRequestValidator`, `PlanDateRange`, `PlanPromptFormatter`, `PlanRationaleBuilder` trong `src/Services/Recommendation/Recommendation.Api/RecommendationStartup.cs`; validate `PlanDeadlineSeconds` 5..120.

**Checkpoint**: Foundation ready — US1 và US2 có thể bắt đầu.

---

## Phase 3: User Story 1 - Diet nhận kế hoạch bữa ăn 7 ngày an toàn (Priority: P1) MVP

**Goal**: `GenerateMealPlan` trả 1–7 ngày, tối đa 4 bữa/ngày, tối đa 5 món/bữa;
100% món thuộc `candidate_foods`, không allergen, tổng calo/macro trong ±10%
khi khả thi; rule 105%; fallback deterministic khi AI lỗi; citation verify;
mọi response có disclaimer.

**Independent Test**: Gọi `GenerateMealPlan` với profile/target/candidate/week
start thứ Hai; kiểm tra đủ ngày/bữa, không allergen, mọi `food_id` thuộc
candidate, calo/macro từ candidate, ngày trong ±10%, `disclaimer` có mặt,
`used_fallback` + `ai_fallback_used` khi provider lỗi.

> **Test-first note**: tạo DTO/result skeleton không logic (T033) trước khi viết
> test để test đỏ vì hành vi, không phải vì compile error.

### Tests for User Story 1

- [ ] T023 [P] [US1] Viết `PlanAllergenFilterTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/PlanAllergenFilterTests.cs`: trim/lowercase/NFC, rỗng = không allergen, không khớp substring, exact token.
- [ ] T024 [P] [US1] Viết `WeeklyBudgetSplitterTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/WeeklyBudgetSplitterTests.cs`: mapping 1–4 bữa, tổng ngày = target ngày × days, tổng bữa = ngân sách ngày, làm tròn bù vào bữa cuối.
- [ ] T025 [P] [US1] Viết `MealPlanCandidateFilterTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/MealPlanCandidateFilterTests.cs`: dedupe `food_id`, cắt >120 theo macro-fit + tie-break `food_id`, lọc dietary, loại `excluded_food_ids`.
- [ ] T026 [P] [US1] Viết `MealPlanValidatorTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/MealPlanValidatorTests.cs`: đúng số ngày/bữa, <=5 món/bữa, mọi `food_id` thuộc candidate, không allergen, calo/macro lệch >10% bị từ chối.
- [ ] T027 [P] [US1] Viết `MealPlanFallbackSelectorTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/MealPlanFallbackSelectorTests.cs`: provider-down vẫn có plan, không lặp khi còn lựa chọn, `target_not_met` khi candidate không đủ, deterministic.
- [ ] T028 [P] [US1] Viết `MealPlanSuggestionBuilderTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/MealPlanSuggestionBuilderTests.cs`: cap day có `meals` rỗng, tối đa 3 suggestions, suggestion chỉ từ quote verify thuộc chương 4 hoặc chunk chương 12 có `book_page_from >= 238` (Phụ lục A), không có nguồn → rỗng + `suggestion_source_missing`, không có `food_id`.
- [ ] T029 [P] [US1] Viết `MealPlanPromptTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/MealPlans/MealPlanPromptTests.cs`: prompt chứa candidate đã lọc, ngân sách ngày/bữa, corpus chương 4 + chunk Phụ lục A; không yêu cầu AI sinh rationale/số liệu.
- [ ] T030 [P] [US1] Viết `GenerateMealPlanUseCaseTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/MealPlans/GenerateMealPlanUseCaseTests.cs`: happy path, provider abstain, provider throws transport exception → fallback, corpus rỗng, target thiếu/âm, all candidates excluded, cap 105%, consumed_date ngoài tuần, locale không hỗ trợ → warning, response metadata, không medical advice.
- [ ] T031 [P] [US1] Viết `GenerateMealPlanGrpcTests` trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Plans/GenerateMealPlanGrpcTests.cs`: 7 ngày, 4 bữa, allergen, food_id, dinh dưỡng, cap, fallback, corpus rỗng, lỗi nghiệp vụ (`invalid_targets`, `invalid_consumed_calories`, `invalid_candidate_food`, locale warning), response metadata, không medical advice.
- [ ] T032 [P] [US1] Viết `MealPlanCitationGrpcTests` trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Plans/MealPlanCitationGrpcTests.cs`: quote khớp chunk được giữ, quote sai bị loại, citation chỉ thuộc chương 4 hoặc chương 12 có `book_page_from >= 238` (Phụ lục A), corpus rỗng → citations rỗng + `corpus_not_loaded`.

### Implementation for User Story 1

- [ ] T033 [US1] Tạo `GenerateMealPlanRequest` và `GenerateMealPlanResult` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/GenerateMealPlanRequest.cs` và `.../GenerateMealPlanResult.cs` với các trường theo `specs/duck-002-recommendation-plan-rpcs/data-model.md` §2/§4, gồm `week_start_date`, `days_requested`, `weekly_target_*`.
- [ ] T034 [P] [US1] Tạo `PlanAllergenFilter` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/PlanAllergenFilter.cs`: trim + lowercase + NFC, so khớp chính xác `CandidateFood.allergens`, rỗng = không allergen.
- [ ] T035 [P] [US1] Tạo `MealPlanCandidateFilter` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/MealPlanCandidateFilter.cs`: dedupe, cắt 120 theo macro-fit, gọi `PlanAllergenFilter` + `DietaryPreferenceFilter`, loại `excluded_food_ids`.
- [ ] T036 [P] [US1] Tạo `WeeklyBudgetSplitter` trong `src/Services/Recommendation/Recommendation.Application/Features/Plans/WeeklyBudgetSplitter.cs`: target ngày → bữa theo tỷ lệ D-04, bù sai số làm tròn vào bữa cuối.
- [ ] T037 [P] [US1] Tạo `MealPlanPromptBuilder` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/MealPlanPromptBuilder.cs`: chỉ dùng `PlanPromptFormatter`, candidate đã lọc, ngân sách, chunk chương 4 + chunk chương 12 có `book_page_from >= 238` (Phụ lục A); system instruction cấm bịa món/số liệu/rationale.
- [ ] T038 [US1] Tạo `MealPlanSelectionParser` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/MealPlanSelectionParser.cs`: đọc `days[].meals[].food_indices[]`, validate chỉ số/không trùng/<=5, lấy dinh dưỡng từ candidate, citation qua `CitationResolver.ResolveOptional`.
- [ ] T039 [P] [US1] Tạo `MealPlanValidator` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/MealPlanValidator.cs`: đúng ngày/bữa, <=5 món, `food_id` thuộc candidate, không allergen; validator MUST từ chối kết quả AI lệch >±10% calo/protein/carbs/fat; cờ nội bộ `targetNotMet` chỉ dùng cho nhánh fallback để use case ghi `target_not_met`; fiber chỉ báo cáo.
- [ ] T040 [P] [US1] Tạo `MealPlanFallbackSelector` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/MealPlanFallbackSelector.cs`: greedy theo macro-fit, không lặp trong cùng ngày (bắt buộc), hạn chế lặp trong tuần (tie-break: ít lần dùng nhất, rồi `food_id` ordinal), deterministic, trả cờ nội bộ để use case ghi `target_not_met`.
- [ ] T041 [P] [US1] Tạo `MealPlanSuggestionBuilder` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/MealPlanSuggestionBuilder.cs`: chỉ dựng tối đa 3 suggestion từ quote đã verify thuộc chương 4 hoặc chương 12 có `book_page_from >= 238` (Phụ lục A); không có nguồn → rỗng + `suggestion_source_missing`.
- [ ] T042 [US1] Tạo `GenerateMealPlanUseCase` trong `src/Services/Recommendation/Recommendation.Application/Features/MealPlans/GenerateMealPlanUseCase.cs`: pipeline validate → filter/trim → split budget → retrieve chapter 4 + chunk chương 12 có `book_page_from >= 238` (Phụ lục A) → cap day → AI selection → parse/validate → fallback → suggestions/disclaimer; map response metadata `week_start_date`/`days_requested`/`weekly_target_*`, locale warning, safety.
- [ ] T043 [US1] Tạo `MealPlanMapper` trong `src/Services/Recommendation/Recommendation.Api/Mapping/MealPlanMapper.cs`: map request gRPC → DTO Application và result → `GenerateMealPlanResponse`, gồm `week_start_date`, `days_requested`, `weekly_target_*`, `PlanWarning`, `Citation`, `Disclaimer`.
- [ ] T044 [US1] Thêm override `GenerateMealPlan` vào `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs`, inject `GenerateMealPlanUseCase`, map request/response.
- [ ] T045 [US1] Đăng ký `GenerateMealPlanUseCase`, `PlanAllergenFilter`, `MealPlanCandidateFilter`, `WeeklyBudgetSplitter`, `MealPlanPromptBuilder`, `MealPlanSelectionParser`, `MealPlanValidator`, `MealPlanFallbackSelector`, `MealPlanSuggestionBuilder` trong `src/Services/Recommendation/Recommendation.Api/RecommendationStartup.cs`.
- [ ] T046 [US1] Chạy `make test PROJECTS=src/Services/Recommendation` cho phần US1, sửa lỗi và ghi evidence vào `specs/duck-002-recommendation-plan-rpcs/tasks.md`.

**Checkpoint**: US1 chạy và kiểm thử độc lập; MVP có thể demo.

---

## Phase 4: User Story 2 - Progress nhận kế hoạch tập luyện 7 ngày trong giới hạn (Priority: P2)

**Goal**: `GenerateWorkoutPlan` trả 1–7 ngày, mỗi ngày là buổi tập hoặc
`rest_day`; đúng `sessions_per_week` khi còn hoạt động an toàn; hoạt động/thời
lượng/cường độ chỉ từ chương 5 + giới hạn caller; limitations rỗng → warning;
provider lỗi → skeleton deterministic; corpus rỗng → degraded mode.

**Independent Test**: Gọi `GenerateWorkoutPlan` với 3 buổi/tuần, 30 phút, walking,
không thiết bị, limitations rỗng; kiểm tra 7 ngày, 3 buổi + 4 nghỉ, duration 30,
không thiết bị, warning `no_limitations_assumed`, citations chương 5 hoặc
`corpus_not_loaded`.

> **Test-first note**: tạo DTO/result skeleton không logic (T054) trước khi viết
> test để test đỏ vì hành vi, không phải vì compile error.

### Tests for User Story 2

- [ ] T047 [P] [US2] Viết `WorkoutActivityCatalogTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/WorkoutActivityCatalogTests.cs`: alias chương 5, preference, equipment rỗng, limitations, intensity chỉ khi có căn cứ, degraded seed từ `WorkoutActivities` khi corpus rỗng.
- [ ] T048 [P] [US2] Viết `WorkoutPlanSkeletonBuilderTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/WorkoutPlanSkeletonBuilderTests.cs`: rải đúng sessions, sessions > days lỗi, mỗi ngày một buổi, không đủ hoạt động → rest day + `no_safe_activity`, sessions = 0 → `min(3, days)`.
- [ ] T049 [P] [US2] Viết `WorkoutPlanValidatorTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/WorkoutPlanValidatorTests.cs`: activity ngoài catalog bị loại, duration = caller, citation bắt buộc khi corpus có (trừ degraded), `rest_day` bất biến.
- [ ] T050 [P] [US2] Viết `WorkoutPromptTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/WorkoutPlans/WorkoutPromptTests.cs`: prompt chứa skeleton/catalog/chương 5; AI chỉ chọn `activity_index` + citation.
- [ ] T051 [P] [US2] Viết `GenerateWorkoutPlanUseCaseTests` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/WorkoutPlans/GenerateWorkoutPlanUseCaseTests.cs`: happy path, provider abstain, provider throws transport exception → skeleton, limitations rỗng/có, corpus rỗng, sessions > days, sessions = 0 với days < 3, locale không hỗ trợ → warning, response metadata, không medical advice.
- [ ] T052 [P] [US2] Viết `GenerateWorkoutPlanGrpcTests` trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Plans/GenerateWorkoutPlanGrpcTests.cs`: 7 ngày, đúng sessions, rest day, limitations, fallback, corpus rỗng, lỗi nghiệp vụ (`invalid_goal`, `invalid_workout_params`), warning `no_limitations_assumed`/`unsupported_activity_preference`/`intensity_unverified`/`unsupported_locale`, response metadata, không medical advice.
- [ ] T053 [P] [US2] Viết `WorkoutPlanCitationGrpcTests` trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Plans/WorkoutPlanCitationGrpcTests.cs`: activity chỉ dùng khi có citation verify thuộc chương 5; quote sai bị loại; corpus rỗng → degraded + `activity_source_unverified`; limitation lạ → `unsupported_limitation`; không có căn cứ cường độ → `intensity_unverified`.

### Implementation for User Story 2

- [ ] T054 [US2] Tạo `GenerateWorkoutPlanRequest` và `GenerateWorkoutPlanResult` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/GenerateWorkoutPlanRequest.cs` và `.../GenerateWorkoutPlanResult.cs` theo `data-model.md` §3/§5, gồm `week_start_date`, `days_requested`, `sessions_scheduled`, `minutes_per_session`.
- [ ] T055 [P] [US2] Tạo `WorkoutActivities` (key, alias tiếng Việt/Anh, thiết bị, intensity) trong `src/Services/Recommendation/Recommendation.Domain/Constants/WorkoutActivities.cs`.
- [ ] T056 [P] [US2] Tạo `WorkoutActivityCatalog` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/WorkoutActivityCatalog.cs`.
- [ ] T057 [P] [US2] Tạo `WorkoutActivityCatalogBuilder` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/WorkoutActivityCatalogBuilder.cs`: chỉ giữ hoạt động có alias trong chunk chương 5; khi corpus rỗng seed từ `WorkoutActivities`; lọc preference/equipment/limitations.
- [ ] T058 [P] [US2] Viết `WorkoutLimitationRulesTests` (đỏ) trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Plans/WorkoutLimitationRulesTests.cs`, rồi tạo `WorkoutLimitationRules` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/WorkoutLimitationRules.cs`: bảng ánh xạ đóng limitation → activity bị loại; token lạ → `unsupported_limitation`; không suy ra chấn thương ngoài request.
- [ ] T059 [US2] Tạo `WorkoutPlanSkeletonBuilder` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/WorkoutPlanSkeletonBuilder.cs`: rải sessions xác định (sau `min(3, days)`), mỗi ngày một buổi, sessions > days lỗi, không đủ hoạt động → rest day + warning.
- [ ] T060 [P] [US2] Tạo `WorkoutPlanPromptBuilder` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/WorkoutPlanPromptBuilder.cs`: prompt gồm skeleton/catalog/chương 5; AI chỉ chọn activity index + citation.
- [ ] T061 [US2] Tạo `WorkoutPlanSelectionParser` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/WorkoutPlanSelectionParser.cs`: đọc `activity_index`/citation, validate với catalog, duration = caller.
- [ ] T062 [P] [US2] Tạo `WorkoutPlanValidator` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/WorkoutPlanValidator.cs`: activity thuộc catalog, duration đúng, citation verify khi corpus có (trừ degraded), intensity rỗng + `intensity_unverified` khi thiếu căn cứ, rest-day invariants.
- [ ] T063 [US2] Tạo `GenerateWorkoutPlanUseCase` trong `src/Services/Recommendation/Recommendation.Application/Features/WorkoutPlans/GenerateWorkoutPlanUseCase.cs`: validate → retrieve chapter 5 → catalog → skeleton → AI chọn → fallback → rationale/warnings/disclaimer; dùng chung `PlanRationaleBuilder`, emit `no_limitations_assumed`/`unsupported_activity_preference`/`intensity_unverified`, response metadata, locale warning, safety.
- [ ] T064 [US2] Tạo `WorkoutPlanMapper` trong `src/Services/Recommendation/Recommendation.Api/Mapping/WorkoutPlanMapper.cs`: map request/response, `week_start_date`, `days_requested`, `sessions_scheduled`, `minutes_per_session`, `PlanWarning`, `Citation`, `Disclaimer`.
- [ ] T065 [US2] Thêm override `GenerateWorkoutPlan` vào `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs`, inject `GenerateWorkoutPlanUseCase`.
- [ ] T066 [US2] Đăng ký `GenerateWorkoutPlanUseCase`, `WorkoutActivityCatalogBuilder`, `WorkoutPlanSkeletonBuilder`, `WorkoutPlanPromptBuilder`, `WorkoutPlanSelectionParser`, `WorkoutPlanValidator` trong `src/Services/Recommendation/Recommendation.Api/RecommendationStartup.cs`.
- [ ] T067 [US2] Chạy `make test PROJECTS=src/Services/Recommendation` cho phần US2, sửa lỗi và ghi evidence vào `specs/duck-002-recommendation-plan-rpcs/tasks.md`.

**Checkpoint**: US1 và US2 đều chạy độc lập; US2 không phá US1.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Kiểm chứng riêng tư, tài liệu, negative control, gate và bàn giao.

- [ ] T068 [P] Viết `PlanNoUserDataTests` trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Plans/PlanNoUserDataTests.cs`: gọi cả hai RPC rồi assert `kb.chunk` không đổi, không bảng mới; capture log và assert không có `user_id`/profile/allergen/limitations/plan content.
- [ ] T069 [P] Cập nhật `docs/api/api-spec.md` §5: 2 RPC, message, mã lỗi (gồm `invalid_targets`, `invalid_consumed_calories`), warning (gồm `unsupported_limitation`), deadline 90s/30s, response metadata.
- [ ] T070 [P] Cập nhật `docs/requirements/prd.md` §2.4: US-REC-07 (meal plan) và US-REC-08 (workout plan) hoặc ghi chú 2 RPC.
- [ ] T071 Chạy negative control cho các guard trọng yếu (allergen, fallback, ±10%, citation, workout catalog, limitation/rationale, medical safety, deadline), xác nhận đỏ đúng lý do rồi xanh; ghi cả hai màu vào `specs/duck-002-recommendation-plan-rpcs/tasks.md`.
- [ ] T072 Chạy các kịch bản trong `specs/duck-002-recommendation-plan-rpcs/quickstart.md` (meal happy path, cap, fallback, corpus rỗng, workout, limitations, lỗi) và ghi kết quả.
- [ ] T073 [P] Chạy `make lint` (target trong `Makefile`: format-check + build warnings-as-errors) và sửa mọi lỗi.
- [ ] T074 [P] Chạy `make test PROJECTS=src/Services/Recommendation` (target trong `Makefile`: unit + integration + E2E Postgres thật) và sửa mọi lỗi.
- [ ] T075 Chạy `make check` (target trong `Makefile`: close-out gate) trước khi push/tag; đính kèm kết quả.
- [ ] T076 Chạy `pwsh scripts/speckit/team-feature.ps1 check` và xác nhận layout/meta nhất quán.
- [ ] T077 **Frontend handoff decision**: ghi vào `specs/duck-002-recommendation-plan-rpcs/tasks.md` rằng feature **không** thêm/sửa/xóa HTTP endpoint, event payload hay DTO client-facing; hai RPC là nội bộ gRPC; **không tạo** `frontend-guide.md`.
- [ ] T078 **Deferred work**: ghi vào `specs/duck-002-recommendation-plan-rpcs/tasks.md` rằng không có finding ngoài phạm vi mới; các mục storage/REST/monthly rollup/reminder/wearable/payment và tích hợp Diet/Progress vẫn là out-of-scope trong `spec.md` Assumptions; không tạo `deferred.md`.
- [ ] T079 **Final review**: kiểm tra lần cuối không có migration/bảng mới, không có `frontend-guide.md`, `recommendation_db` chỉ `kb.*`, không expose REST/Nginx, và cập nhật `specs/duck-002-recommendation-plan-rpcs/tasks.md` với trạng thái hoàn tất.
- [ ] T080 [P] Cập nhật `specs/duck-002-recommendation-plan-rpcs/meta.json`: giữ `services: ["recommendation"]` và `status: "open"` đến khi PR merge; sau merge đổi `status: "closed"` (không đổi trước merge).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bắt đầu ngay; T001 (contract) và T002–T010 có thể chạy song song theo nhóm.
- **Foundational (Phase 2)**: Phụ thuộc Setup; **blocks** US1/US2.
- **User Stories (Phase 3–4)**: Sau Foundational.
  - US1 và US2 có thể chạy song song phần lớn, **trừ** hai file dùng chung
    `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs`
    (T044, T065) và `src/Services/Recommendation/Recommendation.Api/RecommendationStartup.cs`
    (T022, T045, T066). Hai task này phải tuần tự hoặc merge cẩn thận.
- **Polish (Phase 5)**: Sau khi US1 + US2 xong.

### User Story Dependencies

- **US1 (P1)**: Sau Foundational; không phụ thuộc US2.
- **US2 (P2)**: Sau Foundational; không phụ thuộc US1, nhưng dùng chung
  `PlanPromptFormatter`, `PlanRationaleBuilder`, `CitationResolver.ResolveOptional`,
  `PlanWarning`, `RecommendationApiFixture`.

### Within Each User Story

- Tests trước implementation; xác nhận đỏ rồi xanh.
- DTO/model → filter/catalog/splitter → prompt/parser/validator → use case →
  mapper → gRPC handler → DI → integration test.
- Không chuyển sang story tiếp theo khi story hiện tại chưa qua scoped gate.

### Parallel Opportunities

- Setup: T002, T003, T006, T007, T008, T009 có thể chạy song song.
- Foundational: T012, T013, T014, T017, T018, T019, T021 có thể chạy song song.
- US1: T023–T032 (tests) song song; sau DTO T033, các file T034–T037/T039–T041
  song song; T038/T042/T043/T044/T045 tuần tự.
- US2: T047–T053 (tests) song song; sau DTO T054, các file T055–T058/T060/T062
  song song; T059/T061/T063/T064/T065/T066 tuần tự.

---

## Parallel Example: User Story 1

```text
# Tests (khác file, chạy song song):
T023 PlanAllergenFilterTests
T024 WeeklyBudgetSplitterTests
T025 MealPlanCandidateFilterTests
T026 MealPlanValidatorTests
T027 MealPlanFallbackSelectorTests
T028 MealPlanSuggestionBuilderTests
T029 MealPlanPromptTests
T030 GenerateMealPlanUseCaseTests
T031 GenerateMealPlanGrpcTests
T032 MealPlanCitationGrpcTests

# Implementation độc lập sau T033:
T034 PlanAllergenFilter
T035 MealPlanCandidateFilter
T036 WeeklyBudgetSplitter
T037 MealPlanPromptBuilder
T039 MealPlanValidator
T040 MealPlanFallbackSelector
T041 MealPlanSuggestionBuilder
```

## Parallel Example: User Story 2

```text
# Tests:
T047 WorkoutActivityCatalogTests
T048 WorkoutPlanSkeletonBuilderTests
T049 WorkoutPlanValidatorTests
T050 WorkoutPromptTests
T051 GenerateWorkoutPlanUseCaseTests
T052 GenerateWorkoutPlanGrpcTests
T053 WorkoutPlanCitationGrpcTests

# Implementation độc lập sau T054:
T055 WorkoutActivities
T056 WorkoutActivityCatalog
T057 WorkoutActivityCatalogBuilder
T058 WorkoutLimitationRules
T060 WorkoutPlanPromptBuilder
T062 WorkoutPlanValidator
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Hoàn tất Phase 1 + Phase 2.
2. Hoàn tất Phase 3 (US1).
3. **STOP và VALIDATE**: chạy `make test PROJECTS=src/Services/Recommendation`
   cho US1 + integration test meal plan.
4. Demo kế hoạch ăn uống 7 ngày nếu gate xanh.

### Incremental Delivery

1. Setup + Foundational → contract, deadline, validator, citation optional.
2. US1 → test độc lập → demo meal plan (MVP).
3. US2 → test độc lập → demo workout plan; US1 vẫn xanh.
4. Polish → no-user-data, docs, negative control, `make check`.

### Parallel Team Strategy

1. Team làm Setup + Foundational cùng nhau.
2. Sau Foundational:
   - Dev A: US1 (meal plan).
   - Dev B: US2 (workout plan).
3. Hai dev thống nhất thứ tự sửa `RecommendationGrpcService.cs` và
   `RecommendationStartup.cs` để tránh conflict; các file còn lại độc lập.

---

## Frontend Handoff Decision

Feature **không thay đổi client surface**:

- Không thêm/sửa/xóa HTTP endpoint.
- Không thay đổi event payload.
- Không thay đổi DTO client-facing (chỉ thêm message gRPC nội bộ).
- Không expose qua Nginx.

→ Không tạo `frontend-guide.md`. Kết luận được ghi bằng T077.

---

## Deferred Work

Không có finding ngoài phạm vi mới trong lúc lập tasks. Các mục sau vẫn là
out-of-scope theo `spec.md` Assumptions và **không** trở thành task chưa tick:

- Lưu trữ kế hoạch, REST API, rollup tháng, nhắc lịch/thông báo, thiết bị đeo,
  thanh toán.
- Tích hợp thực tế phía Diet/Progress (owner khác).

Nếu trong implement phát hiện việc thật sự ngoài feature, ghi vào
`specs/duck-002-recommendation-plan-rpcs/deferred.md` theo deferred template
(vấn đề, ranh giới, điều kiện gỡ, task đã tick bị ảnh hưởng) — không để thành
task chưa tick của feature.

---

## Notes

- `[P]` = khác file, không phụ thuộc task chưa xong.
- `[US1]`/`[US2]` maps trực tiếp tới user story trong `spec.md`.
- Test phải đỏ trước khi implement; ghi cả đỏ và xanh cho negative control.
- Không sửa `KnowledgeDbContext`, `Migrations/**`, `src/Services/Diet/**`,
  `src/Services/Progress/**`.
- Không tạo `frontend-guide.md`; không tạo bảng/migration.
- `meta.json` giữ `status: open` cho đến khi PR được merge; sau merge mới đổi
  `closed`.
- Chạy `make check` trước push/tag; không tự push/PR khi chưa được dev cho phép.
