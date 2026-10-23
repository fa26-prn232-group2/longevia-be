# Nghiên cứu kỹ thuật — Kế hoạch ăn uống và tập luyện theo tuần

**Feature**: `duck/002/recommendation-plan-rpcs` | **Date**: 2026-10-23
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Tài liệu này chốt các quyết định kỹ thuật cho Phase 0. Không có mục
`NEEDS CLARIFICATION` còn lại: các điểm mơ hồ của spec đã được làm rõ ở
`Clarifications` hoặc được chốt dưới đây kèm lý do.

---

## D-01 — Hợp đồng additive: 2 RPC mới, `PlanWarning`, `PlanSuggestion`, `consumed_date`

**Decision**: Bổ sung vào `contracts/recommendation/v1/recommendation.proto`:

- `GenerateMealPlan(GenerateMealPlanRequest) returns (GenerateMealPlanResponse)`
- `GenerateWorkoutPlan(GenerateWorkoutPlanRequest) returns (GenerateWorkoutPlanResponse)`
- Các message mới `GenerateMealPlanRequest/Response`, `MealPlanDay`,
  `PlannedMeal`, `PlannedFood`, `PlanSuggestion`, `PlanWarning`,
  `GenerateWorkoutPlanRequest/Response`, `WorkoutPlanDay`.
- `target_calories` và target macro của meal plan là **mục tiêu ngày**.
- `consumed_calories` đi kèm `consumed_date`; rule 105% chỉ áp cho đúng ngày
  đó nếu ngày đó nằm trong tuần.
- Khi ngày chạm trần, `meals` rỗng và nội dung thay thế nằm ở `suggestions`
  (không mang `food_id` giả).

**Rationale**: Thay đổi additive không phá 5 RPC hiện có. Tách `suggestions`
khỏi `meals` giữ được bất biến “mọi món trong `meals` phải có `food_id` thuộc
`candidate_foods`” (FR-004). `consumed_date` tránh áp sai rule 105% khi caller
lập kế hoạch giữa tuần.

**Alternatives considered**:

- Tái sử dụng `MealRecommendationResponse`: không biểu diễn được nhiều ngày,
  nhiều bữa, tổng ngày và warning.
- Dùng `RecommendedMeal` với `food_id` rỗng cho đồ uống 0 calo: vi phạm FR-004
  và làm caller không phân biệt được món thật với gợi ý.
- Luôn áp `consumed_calories` cho ngày đầu: sai khi hôm nay không phải ngày đầu
  tuần.

---

## D-02 — Deadline theo từng RPC

**Decision**: Thêm `PlanDeadlineSeconds` (mặc định **90**, hợp lệ 5–120) vào
`RecommendationOptions`. Tạo `RpcDeadlinePolicy` (thuần, không I/O) ánh xạ tên
method gRPC sang deadline:

- `/longevia.recommendation.v1.RecommendationService/GenerateMealPlan` → plan
  deadline.
- `/.../GenerateWorkoutPlan` → plan deadline.
- Các method còn lại → `DeadlineSeconds` (30).

`ServerDeadlineInterceptor` nhận `RpcDeadlinePolicy` thay vì một `TimeSpan` cố
định. Cấu hình mới đọc từ `RECOMMENDATION__PLAN_DEADLINE_SECONDS`.

**Rationale**: Constitution §II yêu cầu deadline tường minh. Plan có thể cần
nhiều thời gian hơn RPC một bữa, nhưng không được nới deadline của 5 RPC cũ.
`RpcDeadlinePolicy` tách được để unit test mà không cần dựng gRPC server.

**Alternatives considered**:

- Một deadline chung 90s: làm RPC cũ chờ lâu hơn cần thiết, trái yêu cầu giữ
  30s.
- Hai interceptor toàn cục: thứ tự interceptor và việc cả hai cùng chạy khó
  kiểm soát.
- Hard-code trong interceptor: không cấu hình được qua env, trái quy ước.

---

## D-03 — Lọc allergen cho plan: chuẩn hoá + khớp chính xác

**Decision**: Tạo `PlanAllergenFilter` riêng cho hai RPC plan:

- Chuẩn hoá cả `profile.allergies` và `CandidateFood.allergens` bằng `Trim()`,
  `ToLowerInvariant()`, Unicode **NFC**.
- So khớp **chính xác từng token** với `CandidateFood.allergens`.
- Không dùng alias, không so chuỗi con, không suy diễn từ tên món.
- `CandidateFood.allergens` rỗng = caller khai món không có allergen đã biết.
- `AllergenGuard` hiện có của `GetMealRecommendations` **không đổi** để không
  phá hành vi/test của `duck-001`.

**Rationale**: Đây là câu trả lời đã chốt ở `Clarifications`; đồng thời tránh
thay đổi ngữ nghĩa của RPC cũ đang dùng alias/substring. Caller (Diet) chịu
trách nhiệm dùng cùng bộ từ vựng.

**Alternatives considered**:

- Dùng lại `AllergenGuard` alias/substring: trái quyết định clarify, có thể
  loại oan hoặc bỏ sót tùy dữ liệu.
- Thêm `allergen_data_complete` vào contract: làm contract phức tạp, chưa cần.
- Bỏ diacritics khi so khớp: có thể gây khớp sai giữa các từ gần giống; clarify
  chỉ yêu cầu trim/lowercase/NFC.

---

## D-04 — Chia ngân sách ngày và bữa

**Decision**: Code chia mục tiêu **ngày** (calo, protein, carbs, fat) theo tỷ lệ
bữa cố định:

| `meals_per_day` | Bữa và tỷ lệ |
| --- | --- |
| 1 | Trưa 100% |
| 2 | Trưa 40%, Tối 60% |
| 3 | Sáng 30%, Trưa 40%, Tối 30% |
| 4 | Sáng 25%, Trưa 35%, Tối 30%, Phụ 10% |

- Tổng ngân sách các ngày = mục tiêu ngày × `days`.
- Tổng ngân sách các bữa trong ngày = ngân sách ngày.
- Mục tiêu fiber (nếu caller gửi > 0) chia cùng tỷ lệ; fiber được báo cáo và
  cảnh báo nếu thiếu, **không** nằm trong gate cứng ±10% (gate cứng gồm calo,
  protein, carbs và fat).
- Làm tròn calo về `int` bằng `MidpointRounding.AwayFromZero`; phần dư/thiếu do
  làm tròn được bù vào bữa cuối để tổng khớp ngân sách ngày.

**Rationale**: Tỷ lệ cố định, dễ kiểm thử, không cần thêm trường contract.
Mapping bữa rõ ràng khi caller chỉ yêu cầu 1–3 bữa.

**Alternatives considered**:

- Chia đều: không phản ánh bữa sáng/trưa/tối thực tế.
- Caller gửi tỷ lệ: thêm trường và trách nhiệm mới cho caller, ngoài yêu cầu.
- Chia theo calo nhưng macro chia đều: lệch mục tiêu macro.

---

## D-05 — Cắt `candidate_foods` về tối đa 120

**Decision**: Khi `candidate_foods` > 120, trước khi lọc/gọi AI (đây là cắt
theo FR-025, **không phải lỗi validation**; validation chỉ yêu cầu >= 1):

1. Loại món trùng `food_id` (giữ bản ghi đầu tiên), ghi warning
   `duplicate_candidate_food`.
2. Tính `MacroFitScore` cho từng món so với ngân sách một bữa trung bình:
   - Sai lệch calo tương đối + sai lệch tỷ lệ protein/carbs/fat so với mục tiêu
     ngày (mẫu số bằng 0 thì bỏ thành phần đó).
3. Sắp xếp tăng dần, tie-break bằng `food_id` ordinal; giữ 120 món đầu.
4. Ghi warning `candidate_foods_truncated` kèm số lượng đã cắt.

**Rationale**: Giới hạn prompt/token và chi phí, vẫn ưu tiên món khớp macro.
Tie-break ordinal bảo đảm deterministic (SC-014).

**Alternatives considered**:

- Cắt theo thứ tự caller gửi: có thể bỏ mất món khớp macro.
- Cắt ngẫu nhiên: không deterministic.
- Từ chối request > 120: trái yêu cầu “cắt theo độ khớp macro”.

---

## D-06 — AI chỉ chọn chỉ số món; code viết `rationale`

**Decision**:

- Prompt meal plan cung cấp danh sách candidate đã lọc (đánh số), ngân sách
  từng ngày/bữa và chunk chương 4 + chunk chương 12 thuộc Phụ lục A
  (`book_page_from >= 238`).
- Schema phản hồi chỉ có `days[].meals[].food_indices[]` và
  `days[].meals[].citations[]`; **không** có rationale tự do, không có số liệu
  dinh dưỡng.
- Code dựng `rationale` từ dữ liệu request và món đã chọn, ví dụ:
  “Bữa trưa chọn 2 món trong danh mục caller gửi, cung cấp X g protein và Y kcal
  theo khẩu phần đã khai.”
- Nếu có citation đã verify, rationale được bổ sung phần trích dẫn sách; không
  có citation thì rationale chỉ dựa trên request.
- Loại bỏ hoàn toàn rationale do mô hình sinh để không có nội dung không kiểm
  chứng được.

**Rationale**: Spec cấm diễn giải dinh dưỡng tự do ngoài hai nguồn. Không thể
kiểm chứng ngữ nghĩa của rationale tự do bằng code; dựng rationale từ dữ liệu
request là cách duy nhất bảo đảm 100% có nguồn.

**Alternatives considered**:

- Giữ rationale AI nếu “có vẻ” dựa trên request: không kiểm chứng được.
- Bắt AI trả rationale kèm citation cho từng câu: vẫn có thể bịa phần không
  citation; khó chặn.
- Không có rationale: trái yêu cầu spec.

---

## D-07 — Fallback meal plan xác định

**Decision**: Khi AI lỗi, timeout, trả món ngoài danh mục, sai số ngày/bữa, quá
5 món/bữa, hoặc lệch calo/macro > ±10%, chạy `MealPlanFallbackSelector`:

1. Với từng ngày/bữa, duyệt candidate an toàn theo `MacroFitScore` tăng dần.
2. Thêm món nếu không trùng món đã dùng **trong cùng ngày** (bắt buộc); ưu tiên
   không trùng trong tuần khi còn lựa chọn; tie-break lặp tuần = ít lần dùng
   nhất, rồi `food_id` ordinal; tối đa 5 món.
3. Ưu tiên tổ hợp đưa tổng bữa gần ngân sách bữa nhất; nếu không thể đạt ±10%
   ngày, trả tổ hợp gần nhất.
4. Nếu không đạt ±10% vì candidate không đủ, ghi `target_not_met`; không bịa
   món/số liệu.
5. Ghi `ai_fallback_used` ở cấp plan.

**Rationale**: SC-004 yêu cầu provider-down vẫn có kế hoạch hợp lệ. Thuật toán
greedy có tie-break cố định nên deterministic (SC-014).

**Alternatives considered**:

- Trả kế hoạch rỗng: trái SC-004.
- Chọn ngẫu nhiên: không deterministic.
- Nới bộ lọc allergen để đủ món: vi phạm BR-03.

---

## D-08 — Verify citation lồng trong plan

**Decision**: Mở rộng `CitationResolver` bằng `ResolveOptional(JsonObject?,
IReadOnlyList<RetrievedChunk>)`:

- Dùng lại `NormalizeForMatch` hiện có (gộp whitespace, bỏ dấu, lowercase).
- Trả danh sách citation đã verify; **không** trả `Abstained` khi rỗng.
- Parser gọi resolver cho từng bữa/buổi tập; citation không khớp bị loại.
- Citation cấp plan là hợp nhất (distinct) các citation đã verify.
- Với workout, nếu một hoạt động không có citation verify thì không dùng hoạt
  động đó; ngày đó thành ngày nghỉ + warning `no_safe_activity`. Ngoại lệ:
  degraded mode khi corpus rỗng theo D-09 mục 7.

**Rationale**: Hành vi cũ của `Resolve` (abstain khi rỗng) vẫn cần cho RPC hỏi
sách. Plan cho phép citation rỗng khi corpus rỗng, nên cần biến thể không
abstain. Dùng lại normalize tránh hai chuẩn so khớp khác nhau.

**Alternatives considered**:

- Viết resolver mới: lặp logic normalize, dễ lệch.
- Tin `quote` do AI trả: vi phạm FR-003/SC-006.
- Bắt buộc citation cho meal plan: corpus rỗng vẫn phải trả kế hoạch, nên không
  thể bắt buộc.

---

## D-09 — Workout plan: catalog đóng từ chương 5 + skeleton xác định

**Decision**:

1. Truy xuất top-K chunk chương 5 bằng query từ `goal`,
   `activity_preference`, `equipment_available`, `limitations`.
2. `WorkoutActivityCatalog` là danh mục đóng, khai báo trong Domain:
   `walking`, `cycling`, `running`, `swimming`, `strength`, `tai_chi`,
   `dancing`, `gardening` (kèm alias tiếng Việt/Anh).
   - Mỗi hoạt động có `Intensity` khai báo từ câu hướng dẫn chương 5 tương ứng;
     nếu chunk không hỗ trợ cường độ, `intensity` để rỗng và ghi warning
     `intensity_unverified`.
   - Chỉ đưa vào catalog hoạt động có alias xuất hiện trong chunk chương 5 đã
     truy xuất. Khi corpus rỗng, seed catalog từ `WorkoutActivities` để degraded
     mode vẫn dùng được preference caller.
   - Nếu `activity_preference` không rỗng, chỉ giữ hoạt động khớp preference.
   - Loại hoạt động cần thiết bị mà `equipment_available` không có.
   - Loại hoạt động xung đột với `limitations` theo bảng ánh xạ đóng trong
     `WorkoutLimitationRules`; token không khớp bị bỏ qua + warning
     `unsupported_limitation`, không suy diễn y tế.
3. Code rải đúng `sessions_per_week` buổi (sau khi áp mặc định
   `min(3, days)`) trên `days` ngày, mỗi ngày tối đa một buổi, dùng công thức
   chia đều xác định. Nếu `sessions_per_week > days` → lỗi
   `recommendation.invalid_workout_params`.
4. AI (nếu gọi) chỉ được chọn một hoạt động trong catalog cho mỗi buổi và trả
   citation; code validate và viết rationale từ hoạt động + giới hạn caller +
   quote đã verify.
5. AI lỗi/không hợp lệ → code chọn hoạt động theo thứ tự catalog ổn định, vẫn
   trả skeleton + `ai_fallback_used`.
6. Không còn hoạt động an toàn → ngày đó là `rest_day` + `no_safe_activity`.
   Đây là ngoại lệ của “đúng số buổi”: an toàn thắng số lượng, theo FR-031.
7. Corpus rỗng:
   - Nếu caller có `activity_preference`, seed catalog từ `WorkoutActivities`,
     dùng đúng tên hoạt động caller gửi, thời lượng caller gửi, không tự thêm
     cường độ, citation rỗng, warning `corpus_not_loaded` +
     `activity_source_unverified`.
   - Nếu không có preference, trả ngày nghỉ + warning; không bịa bài tập.

**Rationale**: Đóng danh mục hoạt động vào chương 5 và giới hạn caller là cách
duy nhất để chứng minh “không tự thêm bài tập”. Skeleton xác định bảo đảm
provider-down vẫn có kế hoạch.

**Alternatives considered**:

- Để AI tự sinh bài tập: vi phạm FR-008/FR-009.
- Chỉ dùng `activity_preference` mà không đối chiếu sách: có thể thêm hoạt động
  ngoài corpus.
- Luôn gọi AI cho workout: provider-down sẽ không có skeleton.

---

## D-10 — Hành vi khi provider lỗi hoặc corpus rỗng

**Decision**:

| Tình huống | Meal plan | Workout plan |
| --- | --- | --- |
| Provider lỗi/timeout | Fallback deterministic từ candidate + `ai_fallback_used` | Skeleton deterministic từ catalog/sách + `ai_fallback_used` |
| AI trả ngoài danh mục/sai cấu trúc | Fallback + warning | Fallback + warning |
| Corpus rỗng | Vẫn trả kế hoạch từ request; `citations` rỗng; `corpus_not_loaded` | Theo D-09 mục 7; `citations` rỗng; `corpus_not_loaded` |
| Không đủ candidate để đạt ±10% | Kế hoạch gần nhất + `target_not_met` | Không áp dụng |
| Không còn hoạt động an toàn | Không áp dụng | Ngày nghỉ + `no_safe_activity` |

Không bao giờ trả kế hoạch rỗng cho request hợp lệ. Lỗi nghiệp vụ chỉ dùng cho
input thiếu/sai hoặc mọi candidate bị loại.

**Rationale**: Khớp FR-020, FR-032, FR-033 và SC-004, SC-013.

**Alternatives considered**:

- Abstain như RPC hỏi sách: trái yêu cầu “provider-down vẫn có kế hoạch”.
- Trả lỗi 503 khi provider lỗi: caller không có kế hoạch dự phòng.

---

## D-11 — Mã lỗi và mapping gRPC

**Decision**: Thêm factory vào `RecommendationErrors`:

| Mã | HTTP nội bộ | gRPC status |
| --- | --- | --- |
| `recommendation.missing_targets` | 400 | InvalidArgument |
| `recommendation.invalid_targets` | 400 | InvalidArgument |
| `recommendation.invalid_consumed_calories` | 400 | InvalidArgument |
| `recommendation.no_candidate_food` | 422 | FailedPrecondition |
| `recommendation.invalid_week_start` | 400 | InvalidArgument |
| `recommendation.invalid_days` | 400 | InvalidArgument |
| `recommendation.invalid_meals_per_day` | 400 | InvalidArgument |
| `recommendation.invalid_profile` | 400 | InvalidArgument |
| `recommendation.invalid_goal` | 400 | InvalidArgument |
| `recommendation.invalid_candidate_food` | 400 | InvalidArgument |
| `recommendation.all_candidates_excluded` | 422 | FailedPrecondition |
| `recommendation.invalid_workout_params` | 400 | InvalidArgument |
| `recommendation.missing_consumed_date` | 400 | InvalidArgument |
| `recommendation.invalid_consumed_date` | 400 | InvalidArgument |

`GrpcExceptionInterceptor` hiện có đã map 400 → `InvalidArgument` và 422 →
`FailedPrecondition`, đồng thời gắn mã vào trailer `code`. Không cần sửa
interceptor.

**Rationale**: Nhất quán với `recommendation.no_candidate_food` hiện có; caller
phân biệt lỗi input với lỗi nghiệp vụ.

**Alternatives considered**:

- Dùng `Internal` cho mọi lỗi: caller không xử lý được.
- Đổi interceptor để map mã riêng: không cần, đã có cơ chế trailer.

---

## D-12 — Không lưu dữ liệu, không migration

**Decision**:

- Không thêm entity, bảng, cột hay migration.
- `recommendation_db`/`kb.*` chỉ được đọc qua `IKnowledgeStore`.
- Không ghi log hồ sơ, dị ứng, limitations, nội dung kế hoạch hoặc `user_id`.
- Integration test gọi cả hai RPC rồi assert số dòng `kb.chunk` không đổi và
  không có bảng dữ liệu người dùng mới.

**Rationale**: Constitution §I và ADR 002. Feature chỉ trả kế hoạch, caller lưu.

**Alternatives considered**:

- Lưu cache kế hoạch để tăng tốc: vi phạm “không lưu dữ liệu người dùng”.
- Lưu lịch sử để cá nhân hoá: ngoài phạm vi và vi phạm hiến pháp.

---

## D-13 — Locale

**Decision**: Chỉ hỗ trợ `locale = "vi-VN"`. `locale` rỗng hoặc khác `vi-VN`
→ dùng `vi-VN`, ghi warning `unsupported_locale`, không đổi số liệu hay cấu
trúc kế hoạch. Toàn bộ text do code dựng (rationale, warning, disclaimer) là
tiếng Việt.

**Rationale**: Spec không yêu cầu đa ngôn ngữ; tránh dịch máy làm sai nội dung.

**Alternatives considered**:

- Từ chối locale lạ: làm caller phải xử lý thêm lỗi không cần thiết.
- Gọi provider dịch: thêm chi phí, rủi ro sai nội dung.

---

## D-14 — Không thêm package/project

**Decision**: Dùng lại toàn bộ hạ tầng hiện có:

- `IChatModel`, `StructuredPrompt`, `StructuredResponseKind`, `SchemaFactory`.
- `IKnowledgeStore`, `RetrievedChunk`, `CitationResolver`.
- `DietaryPreferenceFilter`, `RecommendationErrors`, `AppException`.
- `RecommendationStartup`, `GrpcExceptionInterceptor`, `ServerDeadlineInterceptor`.

Chỉ thêm 2 giá trị `StructuredResponseKind` (`MealPlanSelection`,
`WorkoutPlanSelection`) và 2 nhánh schema tương ứng.

**Rationale**: Không cần package mới; giảm rủi ro CPM/audit và giữ cấu trúc 4
project hiện có.

**Alternatives considered**:

- Thêm thư viện lập kế hoạch/tối ưu: không cần cho quy mô 7 ngày × 4 bữa.
- Tạo project `Recommendation.Plans`: tăng project, trái cấu trúc hiện có.

---

## Tóm tắt câu hỏi kỹ thuật đã giải quyết

| Câu hỏi | Quyết định |
| --- | --- |
| Contract biểu diễn kế hoạch tuần thế nào? | D-01: message riêng, `PlanWarning`, `suggestions`, `consumed_date` |
| Deadline 90s áp riêng 2 RPC thế nào? | D-02: `RpcDeadlinePolicy` + `PlanDeadlineSeconds` |
| Lọc allergen theo chuẩn nào? | D-03: exact sau trim/lowercase/NFC, filter riêng |
| Chia calo/macro cho bữa thế nào? | D-04: tỷ lệ cố định theo `meals_per_day` |
| Cắt 120 candidate thế nào? | D-05: macro-fit score + tie-break `food_id` |
| AI được viết gì? | D-06: chỉ chọn chỉ số món + citation; code viết rationale |
| Provider lỗi thì sao? | D-07/D-10: fallback deterministic, không rỗng |
| Citation lồng được verify thế nào? | D-08: `ResolveOptional`, verify từng quote |
| Workout lấy bài tập từ đâu? | D-09: catalog đóng từ chương 5 + giới hạn caller |
| Lỗi trả mã gì? | D-11: bảng mã + mapping gRPC hiện có |
| Có lưu gì không? | D-12: không, không migration |
| Locale xử lý sao? | D-13: mặc định vi-VN + warning |
| Có thêm package/project? | D-14: không |
