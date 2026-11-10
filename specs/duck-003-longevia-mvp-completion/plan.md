# Implementation Plan: Hoàn thiện MVP Longevia

**Feature**: `duck-003-longevia-mvp-completion`  
**Branch**: `feat/longevia-mvp-completion`  
**Spec**: [spec.md](spec.md)  
**Ngày**: 2026-11-09

## 1. Constitution Check

| Nguyên tắc | Cách tuân thủ |
| --- | --- |
| Mỗi service sở hữu DB riêng | Diet lưu meal plan/vi chất trong `diet_db`; Progress lưu workout trong `progress_db`; không cross-DB |
| Client-facing qua Nginx | Chỉ Identity/Diet/Progress expose REST; Recommendation gRPC-only |
| Đồng bộ qua gRPC | Progress → Diet/Identity/Recommendation; Diet → Recommendation/Identity/Progress |
| Bất đồng bộ qua RabbitMQ | `UserVerifiedEvent`, `WeeklyDigestReadyEvent`; outbox/idempotency |
| EF Core Code First | Migration tuần tự từng service, không sửa tay snapshot |
| Không log dữ liệu nhạy cảm | Không log email, hồ sơ, câu hỏi, token, API key |
| Quan sát được | Mọi service có `/metrics`; Recommendation bổ sung |

## 2. Kiến trúc thay đổi

```text
Client
  └─ Nginx → Identity / Diet / Progress
                 │            │
                 │            ├─ gRPC → Recommendation (meal plan, workout, nutrients, knowledge, habits)
                 │            ├─ gRPC → Identity (profile/contact)
                 │            └─ gRPC → Diet (nutrition adherence/weekly summary)
                 └─ RabbitMQ → Worker (welcome, weekly digest)
```

- Recommendation vẫn không có REST và không route qua Nginx.
- Diet là facade cho các tính năng AI gắn với dinh dưỡng/tri thức sách.
- Progress là facade cho workout/longevity và scheduler digest.

## 3. Contract Changes

### REST mới

| Service | Method | Path |
| --- | --- | --- |
| Diet | POST | `/api/diet/meal-plans` |
| Diet | GET | `/api/diet/meal-plans/{weekStartDate}` |
| Diet | GET | `/api/diet/nutrition/nutrient-gaps?days=7` |
| Diet | POST | `/api/diet/knowledge/ask` |
| Diet | GET | `/api/diet/knowledge/search` |
| Diet | PUT | `/api/diet/foods/{id}` |
| Diet | DELETE | `/api/diet/foods/{id}` |
| Progress | POST | `/api/progress/workout-plans` |
| Progress | GET | `/api/progress/workout-plans/{weekStartDate}` |
| Progress | GET | `/api/progress/longevity-habits?days=7` |
| Progress | DELETE | `/api/progress/metrics/{id}` |
| Progress | DELETE | `/api/progress/sleep/{id}` |
| Identity | PUT | `/api/identity/users/me/preferences` |

### gRPC mới

| Contract | RPC | Caller → Provider |
| --- | --- | --- |
| `contracts/diet/v1/diet.proto` | `GetNutritionAdherence` | Progress → Diet |
| `contracts/diet/v1/diet.proto` | `GetWeeklyNutritionSummary` | Progress → Diet |
| `contracts/identity/v1/identity.proto` | `GetUserContact` | Progress → Identity |

### Event mới

| Event | Routing key | Producer → Consumer |
| --- | --- | --- |
| `UserVerifiedEvent` | `identity.user.verified` | Identity → Worker |
| `WeeklyDigestReadyEvent` | `progress.weekly-digest.ready` | Progress → Worker |

## 4. Data Model

### Diet (`diet_db`)

- `MealPlan(Id, UserId, WeekStartDate, Days, MealsPerDay, TargetCalories, TargetProtein, TargetCarbs, TargetFat, GeneratedAt, UsedFallback, Disclaimer, WarningsJson)`
- `MealPlanDay(Id, MealPlanId, Date, TotalCalories, TotalProtein, TotalCarbs, TotalFat, TotalFiber, CalorieCapReached)`
- `MealPlanMeal(Id, MealPlanDayId, MealType, TargetCalories, Rationale, CitationsJson)`
- `MealPlanItem(Id, MealPlanMealId, FoodId, NameSnapshot, ServingSize, ServingUnit, Calories, Protein, Carbs, Fat, Fiber)`
- `MealPlanSuggestion(Id, MealPlanDayId, Text, CitationsJson)`
- `FoodMicronutrient(Id, FoodItemId, Nutrient, Amount, Unit)`
- `FoodItem.IsDeleted` (soft delete)

### Progress (`progress_db`)

- `WorkoutPlan(Id, UserId, WeekStartDate, Days, SessionsPerWeek, MinutesPerSession, Goal, GeneratedAt, UsedFallback, Disclaimer, WarningsJson)`
- `WorkoutPlanDay(Id, WorkoutPlanId, Date, RestDay, Activity, DurationMinutes, Intensity, EquipmentJson, Rationale, CitationsJson)`

### Identity (`identity_db`)

- `UserPreference(Id, UserId, ReceiveWeeklyDigest)` hoặc cột trên profile tùy schema hiện có.
- Không đổi schema event/outbox hiện hữu.

## 5. Shared Files & Conflict Hotspots

| Path | Thay đổi | Ghi chú |
| --- | --- | --- |
| `contracts/**` | diet.proto mới, identity.proto additive, events mới | Merge tuần tự, không sửa contract cũ |
| `Directory.Packages.props` | Có thể thêm package gRPC/none | Chỉ thêm nếu cần |
| `longevia-be.slnx` | Không thêm project mới | Giữ nguyên |
| `Program.cs` / DI | Đăng ký endpoint/client/background service | Khai báo rõ trong PR |
| `Migrations/**` | Diet + Progress + Identity | Một service một migration wave, tuần tự |
| `deployments/docker-compose.yml` | Network Progress + env mới | Ảnh hưởng nhiều service |
| `deployments/prometheus/prometheus.yml` | Job Recommendation | Infra |
| `longevia-fe/src/common/constants/*` | Endpoint/route mới | FE shared |
| `longevia-fe/src/components/layout/site-header.tsx` | Fix active nav | FE shared |

## 6. Phases

| Phase | Nội dung | Gate |
| --- | --- | --- |
| P0 | Spec/branch, baseline, docs sync, fix navbar | Baseline gate |
| P1 | Recommendation metrics, AI timeout, RAG quality | Recommendation/Diet tests + Prometheus |
| P2 | Diet AI: micronutrient data, meal plan, nutrient gaps, knowledge, Diet gRPC, FE | Diet tests + FE tests |
| P3 | Progress AI: workout, longevity, digest scheduler, FE | Progress tests + FE tests |
| P4 | Identity/Worker: verified event, welcome, digest, contact/preference | Identity/Worker tests |
| P5 | CRUD, service token, rate limit, E2E | Security/E2E |
| P6 | Docs/meta/roadmap, full gates, demo | `make check` + FE build |

## 7. Verification Strategy

- **Contract**: proto/event/REST khớp `docs/api/api-spec.md` và frontend guide.
- **Unit**: use case, validator, mapper, helper.
- **Integration**: PostgreSQL thật + RabbitMQ thật; gRPC HTTP/2 thật.
- **FE**: mapper/schema/query/component + Vitest; `tsc`, lint, build.
- **AI**: provider double cho CI; live smoke đo p50/p95, ghi nhận.
- **Security**: gRPC thiếu/sai token; rate limit 429; JWT isolation.
- **Data**: migration/seed idempotent; không cross-DB.

## 8. Risks

| Rủi ro | Giảm thiểu |
| --- | --- |
| AI p95 vượt budget | D9: đo 50 request, budget p95×1.25 (cap 25s); rerank/topK |
| Dữ liệu vi chất thiếu | Seed USDA; unknown ≠ 0; hiển thị độ đầy đủ |
| Migration conflict | Một feature/branch; một service một migration wave |
| Digest gửi sai người | Opt-in mặc định false; test opted-in/out |
| Scope creep deploy | Deploy là backlog, tách plan riêng |

## 9. Deferred

- Deploy cloud/CD/TLS/domain/secrets/backup/HA/Brevo production.
- Meal edit/delete (cần event v2 + reconciliation).
- mTLS (sau service token MVP).
