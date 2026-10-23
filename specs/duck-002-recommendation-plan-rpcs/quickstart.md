# Hướng dẫn kiểm chứng — Kế hoạch ăn uống và tập luyện theo tuần

**Feature**: `duck/002/recommendation-plan-rpcs` | **Date**: 2026-10-23

Hướng dẫn **chạy và kiểm chứng** feature từ đầu đến cuối. Không chứa mã nguồn
triển khai — phần đó thuộc `tasks.md` và giai đoạn implement.

Tham chiếu: [spec.md](./spec.md) · [plan.md](./plan.md) ·
[data-model.md](./data-model.md) ·
[contracts/recommendation-plan.proto](./contracts/recommendation-plan.proto).

---

## 0. Điều kiện tiên quyết

| Thứ | Yêu cầu |
| --- | --- |
| .NET SDK 10 | `dotnet --version` → 10.x |
| Docker + Docker Compose | PostgreSQL `pgvector/pgvector:pg17` |
| Corpus | `make kb-ensure` nạp seed đã commit; không cần `GOOGLE_AI__API_KEY` |
| Provider AI | `GOOGLE_AI__API_KEY` cho happy path có gọi Gemini; test double không cần key |
| `grpcurl` | gọi gRPC nội bộ `localhost:50051` (nếu chạy ngoài compose) |

```powershell
Copy-Item .env.example .env    # điền GOOGLE_AI__API_KEY nếu muốn gọi provider thật
make kb-ensure                 # nạp corpus vào recommendation_db
```

Nếu chạy service ngoài compose, đặt tối thiểu:

```powershell
$env:ConnectionStrings__Recommendation = "Host=localhost;Port=5432;Database=recommendation_db;Username=recommendation_user;Password=..."
$env:GOOGLE_AI__API_KEY = "..."
$env:KNOWLEDGE_DB__CORPUS_KEY = "che-do-an-truong-tho"
$env:KNOWLEDGE_DB__SCHEMA = "kb"
dotnet run --project src/Services/Recommendation/Recommendation.Api
```

Cấu hình deadline:

```powershell
$env:RECOMMENDATION__DEADLINE_SECONDS = "30"       # 5 RPC cũ
$env:RECOMMENDATION__PLAN_DEADLINE_SECONDS = "90"  # 2 RPC plan
```

---

## 1. Gate nhanh

```powershell
make lint
make test PROJECTS=src/Services/Recommendation
```

**Kỳ vọng**:

- `make lint` xanh (format-check + build warnings-as-errors).
- Unit test mới cho splitter, allergen filter, candidate trimmer, fallback,
  citation resolver, workout catalog, deadline policy đều xanh.
- Integration test gọi server thật qua `Grpc.Net.Client` + PostgreSQL
  Testcontainers xanh.

---

## 2. `GenerateMealPlan` — happy path 7 ngày

Gửi request với `week_start_date = 2026-10-26` (thứ Hai), `days = 7`,
`meals_per_day = 4`, target ngày 2000 kcal / 100 g protein / 250 g carbs /
67 g fat; candidate có món chứa đậu phộng và món không.

```powershell
@'
{
  "profile": {
    "age": 35,
    "sex": "FEMALE",
    "heightCm": 165,
    "weightKg": 60,
    "activityLevel": "ModeratelyActive",
    "dietaryPreference": "None",
    "allergies": ["đậu phộng"]
  },
  "targetCalories": 2000,
  "targetProteinGrams": 100,
  "targetCarbsGrams": 250,
  "targetFatGrams": 67,
  "weekStartDate": "2026-10-26",
  "days": 7,
  "mealsPerDay": 4,
  "candidateFoods": [
    {
      "foodId": "f-peanut",
      "name": "Chè đậu phộng",
      "servingSize": "1",
      "servingUnit": "chén",
      "calories": 320,
      "proteinGrams": 9,
      "carbsGrams": 48,
      "fatGrams": 11,
      "fiberGrams": 4,
      "allergens": ["đậu phộng"]
    },
    {
      "foodId": "f-rice",
      "name": "Cơm gạo lứt",
      "servingSize": "1",
      "servingUnit": "chén",
      "calories": 230,
      "proteinGrams": 5,
      "carbsGrams": 45,
      "fatGrams": 2,
      "fiberGrams": 4
    },
    {
      "foodId": "f-tofu",
      "name": "Đậu hũ sốt cà chua",
      "servingSize": "1",
      "servingUnit": "đĩa",
      "calories": 180,
      "proteinGrams": 14,
      "carbsGrams": 8,
      "fatGrams": 9,
      "fiberGrams": 2
    },
    {
      "foodId": "f-salmon",
      "name": "Cá hồi áp chảo",
      "servingSize": "120",
      "servingUnit": "g",
      "calories": 250,
      "proteinGrams": 26,
      "carbsGrams": 0,
      "fatGrams": 15,
      "fiberGrams": 0
    }
  ]
}
'@ | grpcurl -plaintext -d @ localhost:50051 `
  longevia.recommendation.v1.RecommendationService/GenerateMealPlan
```

**Kỳ vọng**:

- `days` có đúng **7** phần tử, ngày từ `2026-10-26` đến `2026-11-01`.
- Mỗi ngày có đúng **4** bữa (trừ ngày chạm trần), mỗi bữa **<= 5 món**.
- Không món nào có `food_id = f-peanut` hoặc allergen `đậu phộng`.
- Mọi `foods[].foodId` thuộc 4 candidate gửi lên.
- Calo/macro của từng món bằng đúng candidate (không bị AI làm tròn).
- Với candidate đủ đa dạng, tổng calo/macro mỗi ngày trong **±10%** ngân sách
  ngày.
- `disclaimer.text` khác rỗng.
- `citations` có thể rỗng nếu corpus chưa nạp; khi đó có warning
  `corpus_not_loaded`.

---

## 3. `GenerateMealPlan` — chạm trần 105%

Dùng lại request trên, thêm:

```json
"consumedCalories": 2100,
"consumedDate": "2026-10-28"
```

**Kỳ vọng**:

- Ngày `2026-10-28` có `calorieCapReached = true`.
- `meals` của ngày đó **rỗng**; `totalCalories = 0`.
- `suggestions` chứa đồ uống 0 calo/thói quen **nếu** corpus có căn cứ; nếu
  không, `suggestions` rỗng + warning `suggestion_source_missing`.
- Không có `foodId` rỗng trong `meals`.
- Các ngày khác vẫn đủ bữa và bám mục tiêu.

Trường hợp `consumedDate` ngoài tuần, ví dụ `2026-11-10`:

- Rule 105% không áp; có warning `consumed_date_outside_plan`.

Trường hợp `consumedCalories > 0` nhưng thiếu `consumedDate`:

- Lỗi `recommendation.missing_consumed_date`.

---

## 4. `GenerateMealPlan` — provider lỗi/timeout

Chạy integration test với `IChatModel` trả `Abstained = true` (hoặc timeout):

```powershell
dotnet test src/Services/Recommendation/tests/Recommendation.IntegrationTests `
  --filter "FullyQualifiedName~GenerateMealPlan_ProviderFailure"
```

**Kỳ vọng**:

- Vẫn nhận đủ 7 ngày, đủ bữa.
- Mọi món thuộc candidate, không allergen.
- `usedFallback = true`; `warnings` có `ai_fallback_used`.
- Không có response rỗng.

---

## 5. `GenerateMealPlan` — corpus rỗng

Chạy integration test với database không nạp corpus (hoặc corpus trả 0 chunk):

```powershell
dotnet test src/Services/Recommendation/tests/Recommendation.IntegrationTests `
  --filter "FullyQualifiedName~GenerateMealPlan_EmptyCorpus"
```

**Kỳ vọng**:

- Vẫn trả kế hoạch từ request.
- `citations` rỗng; warning `corpus_not_loaded`.
- Không có nội dung sách bị bịa.

---

## 6. `GenerateMealPlan` — lỗi nghiệp vụ

| Request | Mã mong đợi |
| --- | --- |
| Thiếu `targetCalories`/cả 3 macro bằng 0 | `recommendation.missing_targets` |
| `candidateFoods` rỗng | `recommendation.no_candidate_food` |
| `weekStartDate = "2026-10-27"` (thứ Ba) | `recommendation.invalid_week_start` |
| `days = 8` | `recommendation.invalid_days` |
| `mealsPerDay = 5` | `recommendation.invalid_meals_per_day` |
| Tất cả candidate chứa đậu phộng | `recommendation.all_candidates_excluded` |
| Món thiếu `foodId` hoặc calo âm | `recommendation.invalid_candidate_food` |

Kiểm tra trailer:

```powershell
grpcurl -plaintext -v -d @ localhost:50051 `
  longevia.recommendation.v1.RecommendationService/GenerateMealPlan
```

**Kỳ vọng**: status `InvalidArgument`/`FailedPrecondition` và trailer `code`
đúng mã; response không chứa kế hoạch.

---

## 7. `GenerateWorkoutPlan` — happy path

```powershell
@'
{
  "profile": {
    "age": 35,
    "sex": "FEMALE",
    "heightCm": 165,
    "weightKg": 60,
    "activityLevel": "ModeratelyActive",
    "dietaryPreference": "None",
    "allergies": []
  },
  "goal": "LONGEVITY",
  "weekStartDate": "2026-10-26",
  "days": 7,
  "sessionsPerWeek": 3,
  "minutesPerSession": 30,
  "activityPreference": ["walking"],
  "equipmentAvailable": [],
  "limitations": []
}
'@ | grpcurl -plaintext -d @ localhost:50051 `
  longevia.recommendation.v1.RecommendationService/GenerateWorkoutPlan
```

**Kỳ vọng**:

- Đúng 7 ngày, ngày từ `2026-10-26` đến `2026-11-01`.
- Đúng **3** ngày `restDay = false`, **4** ngày `restDay = true`.
- Mỗi buổi có `durationMinutes = 30`.
- Hoạt động chỉ là `walking` (hoặc hoạt động không thiết bị có trong chương 5).
- `equipmentUsed` rỗng.
- `disclaimer.text` khác rỗng.
- `warnings` có `no_limitations_assumed` vì `limitations` rỗng.
- `citations` từ chương 5 nếu corpus có; nếu corpus rỗng, `citations` rỗng +
  `corpus_not_loaded` + `activity_source_unverified`.

---

## 8. `GenerateWorkoutPlan` — limitations và provider lỗi

**Limitations loại hoạt động**:

```json
"activityPreference": ["running"],
"limitations": ["đau khớp gối"]
```

**Kỳ vọng**:

- Không có buổi `running`.
- Nếu không còn hoạt động an toàn, ngày đó `restDay = true` + warning
  `no_safe_activity`; không tự thay bằng bài tập ngoài sách.

**Provider lỗi/timeout**:

```powershell
dotnet test src/Services/Recommendation/tests/Recommendation.IntegrationTests `
  --filter "FullyQualifiedName~GenerateWorkoutPlan_ProviderFailure"
```

**Kỳ vọng**:

- Vẫn có skeleton từ catalog chương 5 + giới hạn caller.
- `usedFallback = true`; `warnings` có `ai_fallback_used`.
- Không có kế hoạch rỗng.

---

## 9. `GenerateWorkoutPlan` — lỗi nghiệp vụ

| Request | Mã mong đợi |
| --- | --- |
| `sessionsPerWeek = 5`, `days = 3` | `recommendation.invalid_workout_params` |
| `minutesPerSession = 5` | `recommendation.invalid_workout_params` |
| `goal = GOAL_UNSPECIFIED` | `recommendation.invalid_goal` |
| `weekStartDate` không phải thứ Hai | `recommendation.invalid_week_start` |
| `days = 0` | `recommendation.invalid_days` |

---

## 10. Không lưu dữ liệu người dùng

Chạy test:

```powershell
dotnet test src/Services/Recommendation/tests/Recommendation.IntegrationTests `
  --filter "FullyQualifiedName~NoUserData"
```

**Kỳ vọng**:

- Số dòng `kb.chunk` trước và sau khi gọi 2 RPC plan bằng nhau.
- Không có bảng mới trong `recommendation_db` ngoài `kb.*`.
- Log không chứa họ tên/`user_id`/allergen/limitations.

---

## 11. Ma trận truy xuất yêu cầu → kiểm chứng

| Yêu cầu (spec) | Kịch bản |
| --- | --- |
| FR-001/FR-002 nguồn A+B, citation đủ trường | §2, §7 |
| FR-003 verify quote | §2, §7, test `CitationResolverTests` |
| FR-004/FR-005 món thuộc candidate, số liệu nguyên bản | §2 |
| FR-006 không tính BMR/TDEE, thiếu target → lỗi | §6 |
| FR-007/FR-008 không suy ra dị ứng/thiết bị/bài tập | §2, §7, §8 |
| FR-010 thiếu nguồn → bỏ + warning | §5, §8 |
| FR-011/FR-035 disclaimer | §2, §7 |
| FR-013..FR-025 meal plan | §2–§6 |
| FR-026..FR-037 workout plan | §7–§9 |
| FR-038..FR-050 validation | §3, §6, §9 |
| FR-051/FR-052 không lưu, không migration | §10 |
| FR-053/FR-054 gRPC-only, deadline | §0, `RpcDeadlinePolicyTests` |
| FR-055 contract additive | `contracts/README.md` §6 |
| FR-056 không frontend-guide | `tasks.md` ghi kết luận |
| SC-001..SC-014 | §1–§10 |
