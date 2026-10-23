# Implementation Plan: Kế hoạch ăn uống và tập luyện theo tuần

**Branch**: `feat/recommendation-plan-rpcs` | **Feature**: `specs/duck-002-recommendation-plan-rpcs` | **Date**: 2026-10-23 | **Spec**: [spec.md](./spec.md)

**Feature ID**: `duck/002/recommendation-plan-rpcs` | **Owner**: `duck`

**Input**: Feature specification from `specs/duck-002-recommendation-plan-rpcs/spec.md`

**Note**: Tài liệu trong `specs/` viết bằng tiếng Việt theo
`docs/development/agent-workflow.md` §IV.7. Đường dẫn trong tài liệu dùng dạng
tương đối.

---

## Summary

Mở rộng Recommendation Service bằng 2 RPC gRPC **additive**:

- `GenerateMealPlan` — Diet gọi, trả kế hoạch ăn uống 1–7 ngày; chỉ chọn món
  trong `candidate_foods`, lọc allergen/dietary/excluded bằng code, chia ngân
  sách ngày/bữa, áp rule 105%, kiểm tra ±10%, fallback xác định khi AI lỗi.
- `GenerateWorkoutPlan` — Progress gọi, trả kế hoạch tập luyện 1–7 ngày; hoạt
  động/thời lượng/cường độ chỉ từ chương 5 + giới hạn caller, fallback skeleton
  xác định khi AI lỗi.

Service **chỉ trả kế hoạch**, không lưu dữ liệu người dùng, không thêm bảng,
không migration, không REST/Nginx, không tạo `frontend-guide.md`. Caller
(Diet/Progress) chịu trách nhiệm tính target, gửi candidate và lưu kế hoạch;
“kế hoạch tháng” là 4–5 lần gọi theo tuần.

Thiết kế dùng lại hạ tầng `duck-001-recommendation-rag`: `IChatModel`,
`IKnowledgeStore`, `CitationResolver`, `DietaryPreferenceFilter`,
`AppException`, gRPC server/interceptor hiện có. Không thêm package/project.

---

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`); không dùng Python cho feature này.

**Primary Dependencies**:

- `Grpc.AspNetCore` 2.84.0 + `Grpc.Tools` 2.84.0 (server + codegen từ
  `contracts/recommendation/v1/recommendation.proto`).
- `Google.GenAI` 1.26.1 qua `IChatModel`/`GoogleAiChatModel`.
- `Polly` 8.8.0 (retry provider, đã có).
- EF Core 10.0.12 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 +
  `Pgvector.EntityFrameworkCore` 0.3.0 — chỉ đọc `recommendation_db`.
- Không thêm package mới; không sửa `Directory.Packages.props`.

**Storage**: PostgreSQL 17 (`pgvector/pgvector:pg17`), database
`recommendation_db`, schema `kb.*` của `duck-001`. Feature **không** thêm bảng,
cột, migration hay entity; chỉ đọc corpus qua `IKnowledgeStore`.

**Testing**:

- xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` cho unit test.
- `Testcontainers.PostgreSql` 4.15.0 + PostgreSQL thật cho integration test.
- `Grpc.Net.Client` gọi server thật trong tiến trình qua `RecommendationApiFixture`.
- `IChatModel` được thay bằng test double tất định; có double trả abstain/timeout
  để test fallback.
- Negative control bắt buộc cho các test bắt bug (allergen, fallback, validator,
  citation, workout catalog, deadline).

**Target Platform**: Linux container (Docker Compose), local == production;
gRPC nội bộ `:50051` trong `internal_net` + `data_net`; không join `edge`.

**Project Type**: service web/gRPC trong kiến trúc microservices; giữ 4 project
hiện có của Recommendation + 2 test project.

**Performance Goals**:

- Plan deadline cấu hình `RECOMMENDATION__PLAN_DEADLINE_SECONDS` mặc định 90s;
  5 RPC cũ giữ 30s.
- Một lời gọi `IChatModel` cho mỗi plan (meal hoặc workout); retrieval top-K 8
  cho mỗi truy vấn.
- 95% lượt gọi plan hoàn tất trong deadline cấu hình (SC-012). SC-012 là SLO
  vận hành: feature verify bằng deadline config + integration smoke, không xây
  load harness riêng trong phạm vi này.
- Fallback không gọi provider lần hai; tổng thời gian fallback < 1s sau khi
  provider lỗi/timeout.

**Constraints**:

- Contract additive; 5 RPC/message hiện có không đổi.
- Không lưu dữ liệu người dùng; không log PII; không migration.
- `candidate_foods` >= 1; nếu > 120 thì cắt theo macro-fit (không phải lỗi);
  `days` 1..7; `meals_per_day` 1..4; tối đa 5 món/bữa.
- `sessions_per_week` 0 → `min(3, days)`, giá trị khác 1..7 và <= `days`;
  `minutes_per_session` 0 → 30, giá trị khác 10..120.
- Không expose REST/Nginx; không tạo `frontend-guide.md`.
- Text do code dựng là tiếng Việt; locale khác fallback `vi-VN` + warning.

**Scale/Scope**:

- 2 RPC mới; tối đa 7 ngày × 4 bữa × 5 món = 140 `PlannedFood`/response.
- Tối đa 120 candidate/request; corpus hiện có 188 chunk.
- Không có state dài hạn; mỗi request độc lập.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Nguyên tắc | Yêu cầu | Trạng thái |
| --- | --- | --- |
| **I. Service Boundaries & Data Ownership** | Mỗi service một DB; Recommendation chỉ giữ corpus, không PII | ✅ PASS — không thêm bảng/migration; `UserProfile`/`CandidateFood`/plan chỉ tồn tại trong request/response; `recommendation_db` vẫn chỉ `kb.*` |
| **II. Synchronous Inter-Service Calls via gRPC** | Gọi service-service bằng gRPC, có deadline, xử lý lỗi transport | ✅ PASS — 2 RPC trong `RecommendationService`; deadline riêng 90s/30s; provider lỗi → fallback, không ném lỗi transport thô |
| **III. Asynchronous Communication via RabbitMQ** | Event qua RabbitMQ, outbox, idempotent | ✅ N/A — feature không publish/consume event |
| **IV. Contract-First Interfaces** | Contract trước code; additive; không rò rỉ entity | ✅ PASS — delta proto trong `specs/.../contracts/`; message mới chỉ là DTO; không đưa `KnowledgeChunk`/entity ra contract |
| **V. Test-First & Verified Gates** | Test trước/cùng lúc; negative control; E2E hạ tầng thật; gate xanh | ✅ PASS — unit + integration + E2E Postgres thật; negative control cho các guard; `make lint`/`make test`/`make check` |
| **Technology & Platform Constraints** | EF Core Code First; Recommendation internal; adapter provider; secret qua env | ✅ PASS — không đổi schema; `recommendation` chỉ `internal_net`+`data_net`; Google AI qua `IChatModel`; `RECOMMENDATION__PLAN_DEADLINE_SECONDS` qua env |
| **Development Workflow & Quality Gates** | Spec-kit, docs tiếng Việt, đường dẫn tương đối, không tự git | ✅ PASS — tài liệu này và các artifact đều tiếng Việt; không có thao tác git tự động |

**Kết luận: PASS — không có vi phạm nào cần ghi vào Complexity Tracking.**

---

## Project Structure

### Documentation (this feature)

```text
specs/duck-002-recommendation-plan-rpcs/
├── spec.md
├── plan.md                  # file này
├── research.md              # Phase 0 — quyết định kỹ thuật
├── data-model.md            # Phase 1 — DTO/validation, không bảng
├── quickstart.md            # Phase 1 — kịch bản kiểm chứng
├── contracts/
│   ├── README.md            # quy trình merge additive
│   └── recommendation-plan.proto
├── checklists/
│   └── requirements.md
└── tasks.md                 # Phase 2 — do speckit-tasks tạo
```

### Source Code (repository root)

```text
src/Services/Recommendation/
├── Recommendation.Api/
│   ├── Deadlines/
│   │   └── RpcDeadlinePolicy.cs                 # mới: method → deadline
│   ├── Endpoints/
│   │   └── RecommendationGrpcService.cs         # sửa: thêm 2 handler
│   ├── Mapping/
│   │   ├── MealPlanMapper.cs                    # mới
│   │   └── WorkoutPlanMapper.cs                 # mới
│   ├── Middlewares/
│   │   └── ServerDeadlineInterceptor.cs         # sửa: dùng policy
│   └── RecommendationStartup.cs                 # sửa: DI + options + health
├── Recommendation.Application/
│   ├── Common/Errors/
│   │   └── RecommendationErrors.cs              # sửa: thêm mã plan
│   ├── Features/Ask/
│   │   └── CitationResolver.cs                  # sửa: ResolveOptional
│   ├── Features/Plans/
│   │   ├── PlanRequestValidator.cs
│   │   ├── WeeklyBudgetSplitter.cs
│   │   ├── PlanDateRange.cs
│   │   ├── PlanPromptFormatter.cs
│   │   └── PlanRationaleBuilder.cs
│   ├── Features/MealPlans/
│   │   ├── GenerateMealPlanRequest.cs
│   │   ├── GenerateMealPlanResult.cs
│   │   ├── GenerateMealPlanUseCase.cs
│   │   ├── PlanAllergenFilter.cs
│   │   ├── MealPlanCandidateFilter.cs
│   │   ├── MealPlanPromptBuilder.cs
│   │   ├── MealPlanSelectionParser.cs
│   │   ├── MealPlanValidator.cs
│   │   ├── MealPlanFallbackSelector.cs
│   │   └── MealPlanSuggestionBuilder.cs
│   └── Features/WorkoutPlans/
│       ├── GenerateWorkoutPlanRequest.cs
│       ├── GenerateWorkoutPlanResult.cs
│       ├── GenerateWorkoutPlanUseCase.cs
│       ├── WorkoutActivityCatalog.cs
│       ├── WorkoutActivityCatalogBuilder.cs
│       ├── WorkoutPlanPromptBuilder.cs
│       ├── WorkoutPlanSkeletonBuilder.cs
│       ├── WorkoutPlanSelectionParser.cs
│       ├── WorkoutPlanValidator.cs
│       └── WorkoutLimitationRules.cs
├── Recommendation.Domain/
│   ├── Constants/
│   │   ├── PlanDefaults.cs                       # tỷ lệ bữa, giới hạn 120/5
│   │   ├── PlanWarningCodes.cs
│   │   └── WorkoutActivities.cs                  # key + alias + thiết bị
│   └── Models/
│       ├── StructuredResponseKind.cs              # sửa: thêm 2 kind
│       ├── PlanWarning.cs
│       └── WeeklyBudget.cs                        # ngân sách ngày/bữa đã chia
├── Recommendation.Infrastructure/
│   ├── Adapters/GoogleAi/
│   │   └── SchemaFactory.cs                       # sửa: thêm 2 schema
│   └── Options/
│       └── RecommendationOptions.cs               # sửa: PlanDeadlineSeconds
└── tests/
    ├── Recommendation.UnitTests/
    │   ├── Features/Plans/
    │   │   ├── PlanRequestValidatorTests.cs
    │   │   ├── PlanPromptFormatterTests.cs
    │   │   ├── PlanRationaleBuilderTests.cs
    │   │   ├── PlanSafetyTests.cs
    │   │   ├── WeeklyBudgetSplitterTests.cs
    │   │   ├── PlanDateRangeTests.cs
    │   │   ├── PlanAllergenFilterTests.cs
    │   │   ├── MealPlanCandidateFilterTests.cs
    │   │   ├── MealPlanValidatorTests.cs
    │   │   ├── MealPlanFallbackSelectorTests.cs
    │   │   ├── MealPlanSuggestionBuilderTests.cs
    │   │   ├── WorkoutActivityCatalogTests.cs
    │   │   ├── WorkoutLimitationRulesTests.cs
    │   │   ├── WorkoutPlanSkeletonBuilderTests.cs
    │   │   ├── WorkoutPlanValidatorTests.cs
    │   │   └── RpcDeadlinePolicyTests.cs
    │   ├── Features/MealPlans/
    │   │   ├── GenerateMealPlanUseCaseTests.cs
    │   │   └── MealPlanPromptTests.cs
    │   └── Features/WorkoutPlans/
    │       ├── GenerateWorkoutPlanUseCaseTests.cs
    │       └── WorkoutPromptTests.cs
    └── Recommendation.IntegrationTests/
        ├── Features/Meals/RecommendationApiFixture.cs   # sửa: double cho plan
        └── Features/Plans/
            ├── GenerateMealPlanGrpcTests.cs
            ├── MealPlanCitationGrpcTests.cs
            ├── GenerateWorkoutPlanGrpcTests.cs
            ├── WorkoutPlanCitationGrpcTests.cs
            └── PlanNoUserDataTests.cs
```

**Structure Decision**: Giữ nguyên 4 project + 2 test project hiện có. Hai RPC
mới là feature mới trong `Application/Features`, không tạo project/service mới.
`Recommendation.Api` chỉ map request → use case → response; `Application` giữ
toàn bộ nghiệp vụ; `Infrastructure` chỉ thêm option/schema. Không sửa
`KnowledgeDbContext`, `Migrations/**` hay `src/Shared/**`.

---

## Shared Files & Conflict Hotspots

> Required for team work. Feature này chỉ chạm Recommendation + tài liệu hợp
> đồng. Không có feature nào khác đang mở cùng service tại thời điểm lập plan.

| Path | Vì sao phải chạm | Owner / feature khác | Merge point |
| --- | --- | --- | --- |
| `contracts/recommendation/v1/recommendation.proto` | Thêm 2 RPC + message additive | Không (duck-001 đã merge) | Merge `main` trước, thêm sau, chạy lại 5 RPC test cũ |
| `docs/api/api-spec.md` | Ghi 2 RPC mới ở §5 | Không | Trước PR |
| `docs/requirements/prd.md` | Cập nhật §2.4 (US-REC mới) | Không | Trước PR |
| `src/Services/Recommendation/Recommendation.Infrastructure/Options/RecommendationOptions.cs` | Thêm `PlanDeadlineSeconds` | Không | Trước PR |
| `src/Services/Recommendation/Recommendation.Api/Middlewares/ServerDeadlineInterceptor.cs` | Chọn deadline theo method | Không | Trước PR |
| `src/Services/Recommendation/Recommendation.Api/RecommendationStartup.cs` | DI + validate option + đăng ký use case | Không | Trước PR |
| `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs` | Thêm handler cho 2 RPC | Không | Trước PR |
| `src/Services/Recommendation/Recommendation.Application/Features/Ask/CitationResolver.cs` | Thêm `ResolveOptional` (additive) | Không | Trước PR |
| `src/Services/Recommendation/Recommendation.Domain/Models/StructuredResponseKind.cs` | Thêm 2 enum value | Không | Trước PR |
| `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/GoogleAi/SchemaFactory.cs` | Thêm 2 schema | Không | Trước PR |
| `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Meals/RecommendationApiFixture.cs` | Double cho 2 kind mới | Không | Trước PR |
| `.env.example` | Thêm `RECOMMENDATION__PLAN_DEADLINE_SECONDS` | Không | Sớm, cùng PR |
| `deployments/docker-compose.yml` | Truyền env plan deadline | Không | Sớm, cùng PR |
| `resources/knowledge/**` | **Không sửa** | duck-001 | — |
| `src/Services/Recommendation/Recommendation.Infrastructure/Migrations/**` | **Không sửa** | — | — |
| `src/Services/Diet/**`, `src/Services/Progress/**` | **Không sửa** — tích hợp caller thuộc owner khác | Diet/Progress owner | — |

**Migration rule**: Feature này **không tạo migration**; không có feature nào
được tạo migration cho Recommendation trong lúc feature này mở. `recommendation_db`
giữ nguyên schema `kb.*`.

**Contract rule**: `contracts/` là file dùng chung. Delta nằm ở
`specs/duck-002-recommendation-plan-rpcs/contracts/`; chỉ merge vào file thật
sau khi `main` đã merge và 5 RPC cũ vẫn qua test. Không đổi số field/tên field
cũ.

---

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --- | --- | --- |
| (không có) | — | — |

Không có vi phạm hiến pháp. Các thay đổi `suggestions`, `PlanWarning`,
`consumed_date` và 2 `StructuredResponseKind` là additive, không đổi ranh giới
service hay lưu trữ.

---

## Phase 0 — Nghiên cứu

Đã hoàn tất trong [research.md](./research.md). Các quyết định chính:

| Mã | Quyết định |
| --- | --- |
| D-01 | Contract additive: 2 RPC, `PlanWarning`, `PlanSuggestion`, `consumed_date` |
| D-02 | `RpcDeadlinePolicy` + `PlanDeadlineSeconds` (90s), RPC cũ 30s |
| D-03 | `PlanAllergenFilter` exact sau trim/lowercase/NFC; không đổi `AllergenGuard` cũ |
| D-04 | Tỷ lệ bữa cố định theo `meals_per_day`; tổng ngày = target ngày × `days` |
| D-05 | Cắt 120 candidate bằng macro-fit + tie-break `food_id` |
| D-06 | AI chỉ chọn chỉ số món + citation; code dựng rationale |
| D-07 | Fallback meal plan greedy, deterministic, không bịa |
| D-08 | `CitationResolver.ResolveOptional` verify quote lồng, không abstain |
| D-09 | Workout catalog đóng từ chương 5 + skeleton xác định + AI chọn trong catalog |
| D-10 | Provider/corpus failure: meal fallback, workout skeleton, không rỗng |
| D-11 | Mã lỗi `recommendation.*` + mapping gRPC hiện có |
| D-12 | Không lưu, không migration, không log PII |
| D-13 | Locale mặc định `vi-VN` + warning |
| D-14 | Không thêm package/project |

Không còn `NEEDS CLARIFICATION` trong Technical Context.

---

## Phase 1 — Thiết kế

Đã hoàn tất:

- [data-model.md](./data-model.md): DTO request/response, validation matrix,
  warning/error codes, invariants, không bảng/migration.
- [contracts/recommendation-plan.proto](./contracts/recommendation-plan.proto):
  delta additive; [contracts/README.md](./contracts/README.md): quy trình merge.
- [quickstart.md](./quickstart.md): kịch bản kiểm chứng end-to-end.

**Constitution Check sau thiết kế**: PASS — không phát sinh vi phạm mới. Các
điểm cần giữ khi implement:

- `suggestions` không mang `food_id`, không đi vào `meals`.
- `recommendation_db` không có bảng/cột mới; không lưu plan/profile.
- `PlanWarning`/`Citation` là DTO, không rò rỉ entity `Chunk`/`Corpus`.
- Hai RPC vẫn nội bộ, không route Nginx.

---

## Thiết kế triển khai

### 1. Hợp đồng và cấu hình

1. Merge delta proto vào `contracts/recommendation/v1/recommendation.proto`
   theo [contracts/README.md](./contracts/README.md).
2. Thêm `PlanDeadlineSeconds` (mặc định 90, validate 5..120) vào
   `RecommendationOptions`.
3. Tạo `RpcDeadlinePolicy`:

   ```text
   GenerateMealPlan / GenerateWorkoutPlan → PlanDeadlineSeconds
   các method khác                        → DeadlineSeconds
   ```

4. Sửa `ServerDeadlineInterceptor` để lấy deadline từ policy theo
   `context.Method`; giữ nguyên hành vi token/telemetry.
5. Thêm env `RECOMMENDATION__PLAN_DEADLINE_SECONDS=90` vào `.env.example` và
   `deployments/docker-compose.yml` (comment tiếng Anh).

### 2. DTO và validation

1. Thêm request/result records trong `Application/Features/MealPlans` và
   `WorkoutPlans`.
2. `PlanRequestValidator` kiểm tra:
   - profile, goal, target, macro, ngày thứ Hai, days/meals, candidate count,
     sessions/minutes, consumed_date.
   - Trả `AppException` qua các factory mới trong `RecommendationErrors`.
3. `PlanDateRange` tính danh sách ngày ISO từ `week_start_date` + `days`.
4. `WeeklyBudgetSplitter` chia target ngày → bữa theo D-04; bù sai số làm tròn
   vào bữa cuối.

### 3. Meal plan

**Pipeline**:

```text
validate
  → dedupe/trim candidate (<=120) + warnings
  → PlanAllergenFilter (exact) + DietaryPreferenceFilter + excluded
  → nếu rỗng: all_candidates_excluded
  → split budgets + dates
  → retrieve chapter 4 + chapter 12 (chỉ giữ chunk chương 12 có `book_page_from >= 238` = Phụ lục A)
  → build cap day (nếu consumed_date trong tuần và >=105%)
  → AI selection cho các ngày còn lại (một call)
  → parse + verify citations + validate cấu trúc/±10%
  → nếu fail: fallback deterministic
  → build rationale/suggestions/warnings
  → map response
```

**Chi tiết**:

- `MealPlanCandidateFilter`:
  - Dedupe `food_id`, warning `duplicate_candidate_food`.
  - Trim > 120 theo D-05, warning `candidate_foods_truncated`.
  - `PlanAllergenFilter` (exact) → `DietaryPreferenceFilter` → loại
    `excluded_food_ids`.
  - Nếu rỗng → `all_candidates_excluded`.
- `MealPlanPromptBuilder` (mới, dùng `PlanPromptFormatter` để bọc corpus):
  - Chỉ gửi candidate đã lọc, đánh số.
  - Gửi ngân sách từng ngày/bữa.
  - Gửi chunk chương 4 + chunk chương 12 có `book_page_from >= 238` (Phụ lục A)
    trong khối `[BEGIN CORPUS DATA]`.
  - System instruction: chỉ chọn `food_index` trong danh sách; không bịa món/
    số liệu; citation chỉ từ chunk; code sẽ viết rationale.
- `SchemaFactory` thêm `MealPlanSelection`:
  ```json
  {
    "days": [
      {
        "date": "2026-10-26",
        "meals": [
          {
            "meal_type": "LUNCH",
            "food_indices": [0, 2],
            "citations": [{ "chunk_index": 1, "quote": "..." }]
          }
        ]
      }
    ]
  }
  ```
  Không có rationale/số liệu dinh dưỡng trong schema.
- `MealPlanSelectionParser`:
  - Đọc đúng `days`/`meals` theo yêu cầu; bỏ ngày/bữa sai.
  - `food_indices` phải hợp lệ, không trùng trong bữa, <= 5.
  - Món lấy nguyên từ candidate; citation qua `ResolveOptional`.
  - Code dựng rationale từ món + target + citation.
- `MealPlanValidator`:
  - Đúng số ngày/bữa (trừ ngày cap).
  - Mỗi bữa <= 5 món.
  - Mọi `food_id` thuộc candidate.
  - Không allergen (đã lọc; kiểm tra lại).
  - Tổng calo/protein/carbs/fat ngày trong ±10% ngân sách ngày; fiber chỉ báo
    cáo; nếu candidate không đủ, đánh dấu nội bộ để use case ghi warning
    `target_not_met` (không thêm field contract).
- `MealPlanFallbackSelector`:
  - Greedy theo D-07, deterministic, ưu tiên không lặp trong ngày; hạn chế lặp
    trong tuần khi còn lựa chọn.
  - Trả cờ nội bộ `targetNotMet` để use case ghi warning `target_not_met`.
- `MealPlanSuggestionBuilder`:
  - Khi cap: retrieve chương 4 + chunk chương 12 có `book_page_from >= 238`
    (Phụ lục A), dựng suggestion từ quote đã verify.
  - Không có quote → suggestions rỗng + `suggestion_source_missing`.

### 4. Workout plan

**Pipeline**:

```text
validate
  → retrieve chapter 5 (top-K)
  → build closed activity catalog (book aliases + caller constraints)
  → build session-day skeleton (deterministic spread)
  → AI chọn activity trong catalog (một call, optional)
  → parse/validate; nếu fail: skeleton fallback
  → build rationale + citations + warnings
  → map response
```

**Chi tiết**:

- `WorkoutActivityCatalog` (Domain/Constants):
  - Key + alias tiếng Việt/Anh: `walking`, `cycling`, `running`, `swimming`,
    `strength`, `tai_chi`, `dancing`, `gardening`.
  - Thiết bị tối thiểu cho từng hoạt động (none/bike/pool/gym...).
  - `WorkoutActivityCatalogBuilder` chỉ giữ hoạt động có alias trong chunk
    chương 5; lọc theo `activity_preference`, `equipment_available`,
    `limitations`. Khi corpus rỗng, seed catalog từ `WorkoutActivities` và chỉ
    dùng preference caller (degraded mode D-09).
  - `WorkoutLimitationRules` dùng bảng ánh xạ đóng (limitation token → activity
    key); token không khớp bị bỏ qua + warning `unsupported_limitation`.
- `WorkoutPlanSkeletonBuilder`:
  - Rải đúng `sessions_per_week` buổi (sau khi áp mặc định `min(3, days)`) trên
    `days` ngày, mỗi ngày tối đa một buổi, dùng công thức chia đều cố định.
  - Nếu `sessions_per_week > days` → `invalid_workout_params`.
  - Nếu không đủ hoạt động an toàn → ngày nghỉ + `no_safe_activity`.
- `WorkoutPlanPromptBuilder` (mới, dùng `PlanPromptFormatter`) +
  `SchemaFactory.WorkoutPlanSelection`:
  - Gửi skeleton + catalog đóng + chunk chương 5.
  - AI chỉ trả `activity_index` cho từng buổi + citation; không viết số liệu/
    bài tập mới.
- `WorkoutPlanSelectionParser` + `WorkoutPlanValidator`:
  - Activity phải thuộc catalog; duration = caller; intensity chỉ nhận giá trị
    có căn cứ trong chunk; nếu không có căn cứ, intensity rỗng + warning
    `intensity_unverified`.
  - Citation verify; nếu không có citation cho activity → không dùng activity
    đó, ngày đó rest day + `no_safe_activity`.
- `PlanRationaleBuilder` (dùng chung): rationale từ activity + caller constraints
  + quote đã verify; code dựng, không dùng free text AI.
- Degraded mode (corpus rỗng):
  - Có preference → dùng đúng activity caller gửi + duration caller, không
    intensity, citation rỗng, `corpus_not_loaded` + `activity_source_unverified`.
  - Không preference → rest day + `no_safe_activity`.
  - Limitation không khớp bảng ánh xạ đóng → `unsupported_limitation`, không
    suy diễn y tế.

### 5. API mapping và DI

- `MealPlanMapper` / `WorkoutPlanMapper`: contract ↔ Application DTO; map
  `PlanWarning`, `Citation`, `Disclaimer`.
- `RecommendationGrpcService`: thêm 2 override; gọi use case với
  `context.CancellationToken`.
- `RecommendationStartup`:
  - Đăng ký `RpcDeadlinePolicy` (singleton).
  - Đăng ký validator/splitter/filter/parser/fallback/use case (scoped nếu phụ
    thuộc `IKnowledgeStore`/`IChatModel`).
  - Validate `PlanDeadlineSeconds` 5..120.
- Không đổi `GrpcExceptionInterceptor`; mã lỗi đi qua trailer `code`.

### 6. Tests

**Unit**:

- `PlanRequestValidatorTests`: từng mã lỗi; thứ Hai; days/meals/sessions;
  `goal`/`invalid_candidate_food`; locale; target/consumed âm; sessions = 0 với
  `days < 3` → `min(3, days)`.
- `PlanPromptFormatterTests`: corpus-as-data, VERIFIED/NEEDS_REVIEW, không rò
  nội dung ngoài chunk.
- `PlanRationaleBuilderTests`: rationale chỉ từ request + quote verify; không
  chứa khuyến nghị thuốc/chẩn đoán/điều trị.
- `PlanSafetyTests`: SC-011/FR-011/FR-035 — không có medical advice trong
  rationale/disclaimer.
- `WorkoutLimitationRulesTests`: bảng ánh xạ đóng; token lạ bị bỏ qua +
  `unsupported_limitation`; không suy diễn y tế.
- `WeeklyBudgetSplitterTests`: tổng ngày/bữa; mapping 1–4 bữa; làm tròn.
- `PlanDateRangeTests`: 7 ngày liên tiếp; tháng/năm; days = 1.
- `PlanAllergenFilterTests`: exact sau trim/lowercase/NFC; rỗng; không khớp
  substring.
- Bổ sung case `ResolveOptional` trong
  `Features/Ask/CitationResolverTests.cs`: quote khớp/không khớp, rỗng không
  abstain.
- `MealPlanCandidateFilterTests`: dedupe; trim 120; dietary; excluded.
- `MealPlanValidatorTests`: sai số ngày/bữa; món ngoài; >5 món; ±10%; fixture
  "đủ đa dạng" = >= 12 món, có món giàu protein/carbs/fat, món 0 calo, không
  allergen.
- `MealPlanFallbackSelectorTests`: provider-down; không lặp khi còn lựa chọn;
  giữ đủ bữa khi candidate khan hiếm; `target_not_met`.
- `MealPlanSuggestionBuilderTests`: có/không corpus; suggestion không food_id.
- `WorkoutActivityCatalogTests`: alias chương 5; preference; equipment;
  limitations.
- `WorkoutPlanSkeletonBuilderTests`: đúng số buổi; sessions > days; rest day.
- `WorkoutPlanValidatorTests`: activity ngoài catalog; duration; citation.
- `RpcDeadlinePolicyTests`: 2 method plan → 90; 5 method cũ → 30.
- `GenerateMealPlanUseCaseTests` / `GenerateWorkoutPlanUseCaseTests`: happy path,
  provider abstain, corpus rỗng, input lỗi.

**Integration** (Postgres thật + test double):

- `GenerateMealPlanGrpcTests`: 7 ngày, allergen, food_id, target, disclaimer,
  cap, fallback, corpus rỗng, response metadata `week_start_date`/
  `days_requested`/`weekly_target_*`.
- `GenerateWorkoutPlanGrpcTests`: 7 ngày, đúng sessions, rest day, limitations
  rỗng/có, corpus rỗng, fallback, response metadata.
- `MealPlanCitationGrpcTests`: quote khớp chunk; quote sai bị loại; citation
  chỉ thuộc chương 4 hoặc chunk Phụ lục A.
- `WorkoutPlanCitationGrpcTests`: activity chỉ dùng khi có citation verify;
  quote sai bị loại; corpus rỗng → degraded + `activity_source_unverified`.
- `PlanNoUserDataTests`: gọi 2 RPC rồi assert `kb.chunk` không đổi; không bảng
  mới; capture log và assert không có PII.
- Cập nhật `RecommendationApiFixture` để double trả 2 kind mới.

**Negative control**:

- Bỏ `PlanAllergenFilter` → test allergen đỏ.
- Bỏ fallback → test provider-down đỏ.
- Bỏ validator ±10% → test lệch target đỏ.
- Bỏ verify quote → test citation sai đỏ.
- Cho activity ngoài catalog → test workout no-invent đỏ.
- Bỏ `WorkoutLimitationRules`/rationale guard → test limitation/medical advice đỏ.
- Dùng deadline chung → test `RpcDeadlinePolicy` đỏ.

### 7. Tài liệu

- Cập nhật `docs/api/api-spec.md` §5: 2 RPC, message, mã lỗi, warning, deadline.
- Cập nhật `docs/requirements/prd.md` §2.4: US-REC-07 (meal plan) và
  US-REC-08 (workout plan) hoặc ghi chú 2 RPC.
- Không tạo `frontend-guide.md`; ghi kết luận vào `tasks.md`.
- Không sửa `docs/system-design/` vì không đổi ranh giới/kiến trúc.

---

## Risks & Mitigations

| Rủi ro | Ảnh hưởng | Cách xử lý |
| --- | --- | --- |
| AI trả JSON lớn/sai schema cho 7 ngày | Plan lỗi | Validator + fallback deterministic; schema chặt; một call/plan |
| Provider timeout 30s trong deadline 90s | Fallback sớm | Một call/plan; provider timeout hiện có; warning `ai_fallback_used` |
| Exact allergen khác `AllergenGuard` cũ | Hai ngữ nghĩa trong service | Filter riêng cho plan; ghi rõ caller contract; test riêng |
| Catalog workout không phủ hết hoạt động chương 5 | Nhiều rest day | Test với corpus thật; warning `no_safe_activity`; mở rộng alias ở plan nếu cần |
| `suggestions` không có nguồn sách | Nội dung rỗng | Chỉ dùng quote verify; warning `suggestion_source_missing` |
| Quote lồng không verify | Citation sai | `ResolveOptional` + test negative control |
| Per-RPC deadline làm hỏng RPC cũ | Regression | `RpcDeadlinePolicy` unit test + integration test 5 RPC cũ |
| Test fixture cũ không hỗ trợ kind mới | Integration test fail | Cập nhật double tất định; giữ hành vi cũ |
| Vô tình thêm migration/bảng | Vi phạm hiến pháp | Không sửa `KnowledgeDbContext`; test `PlanNoUserDataTests` |
| Log PII | Vi phạm riêng tư | Không log profile/plan; review log statements |

---

## Definition of Done

- [ ] Delta proto merge additive; 5 RPC/message cũ không đổi; `make lint` xanh.
- [ ] 2 RPC có handler, mapper, DI; deadline riêng 90s/30s hoạt động.
- [ ] Meal plan: 7 ngày, 4 bữa, <=5 món, 100% allergen-free, 100% food_id
      thuộc candidate, ±10% khi khả thi, fallback provider-down, cap 105%,
      `suggestions` không food_id.
- [ ] Workout plan: 7 ngày, đúng sessions khi an toàn, activity/duration/
      intensity từ chương 5 + caller khi corpus có; degraded mode theo FR-033 khi
      corpus rỗng; limitations rỗng → warning, provider-down skeleton.
- [ ] Safety: 0 khuyến nghị thuốc/chẩn đoán/điều trị trong rationale/disclaimer
      (PlanSafetyTests + negative control).
- [ ] Citation meal plan chỉ thuộc chương 4 hoặc chunk Phụ lục A; workout chỉ
      thuộc chương 5.
- [ ] Response có `week_start_date`, `days_requested`; meal có `weekly_target_*`;
      workout có `sessions_scheduled`, `minutes_per_session`.
- [ ] Citation: 100% quote trả về khớp chunk; quote sai bị loại.
- [ ] Không migration/bảng/dữ liệu người dùng; `kb.chunk` không đổi sau khi gọi.
- [ ] Không REST/Nginx; không `frontend-guide.md`; kết luận ghi trong `tasks.md`.
- [ ] Unit + integration + E2E Postgres thật xanh; negative control đã chạy đỏ
      đúng lý do.
- [ ] `make test PROJECTS=src/Services/Recommendation` xanh.
- [ ] `make check` xanh trước push/tag.
- [ ] `pwsh scripts/speckit/team-feature.ps1 check` xanh.
- [ ] `docs/api/api-spec.md` và `docs/requirements/prd.md` §2.4 cập nhật.
- [ ] `specs/duck-002-recommendation-plan-rpcs/tasks.md` tick đủ task và ghi
      kết luận không có client surface.
