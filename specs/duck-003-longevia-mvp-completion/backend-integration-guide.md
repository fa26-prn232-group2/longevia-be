# Backend Integration Guide — Longevia MVP completion

**Feature**: `duck-003-longevia-mvp-completion`  
**Status**: `READY_FOR_BACKEND` cho các contract đã implement; service token/E2E còn `PENDING`.

## gRPC mới

| Contract | RPC | Caller → Provider |
| --- | --- | --- |
| `contracts/diet/v1/diet.proto` | `GetNutritionAdherence` | Progress → Diet |
| `contracts/diet/v1/diet.proto` | `GetWeeklyNutritionSummary` | Progress → Diet |
| `contracts/identity/v1/identity.proto` | `GetUserContact` | Progress → Identity |
| `contracts/recommendation/v1/recommendation.proto` | `GenerateMealPlan` | Diet → Recommendation |
| `contracts/recommendation/v1/recommendation.proto` | `AnalyzeNutrientGaps` | Diet → Recommendation |
| `contracts/recommendation/v1/recommendation.proto` | `AskLongevityQuestion` / `SearchLongevityKnowledge` | Diet → Recommendation |
| `contracts/recommendation/v1/recommendation.proto` | `GenerateWorkoutPlan` | Progress → Recommendation |
| `contracts/recommendation/v1/recommendation.proto` | `EvaluateLongevityHabits` | Progress → Recommendation |

## Events mới

| Event | Routing key | Producer → Consumer |
| --- | --- | --- |
| `UserVerifiedEvent` | `identity.user.verified` | Identity → Worker |
| `WeeklyDigestReadyEvent` | `progress.weekly-digest.ready` | Progress → Worker |

## Network / config

- Progress tham gia `internal_net` để gọi Recommendation/Diet.
- Diet có HTTP/2 listener `50051` (`DIET__GRPC_PORT`).
- Progress config: `RecommendationGrpc__Address`, `DietGrpc__Address`.
- Digest: `DIGEST__ENABLED`, `DIGEST__RUN_ON_START`.
- Diet AI budget: `DIET_RECOMMENDATION_DEADLINE_MS` (mặc định 20000).

## PENDING

- Service token/metadata cho toàn bộ gRPC client/server.
- Rate limit đã có ở Nginx; chưa có E2E test.
- mTLS production để backlog deploy.
