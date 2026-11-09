# Tasks: Hoàn thiện MVP Longevia

**Spec**: [spec.md](spec.md)  
**Plan**: [plan.md](plan.md)  
**Branch**: `feat/longevia-mvp-completion`

## Phase 0 — Chuẩn bị

- [x] T000 Chốt D1–D9 và ghi nhận quyết định vào plan/spec.
- [x] T001 Rà soát `contracts/**` và `docs/api/api-spec.md`; xác nhận thay đổi additive.
- [x] T002 Tạo một feature spec/branch duy nhất; khai báo `meta.json.services`.
- [x] T003 Chạy baseline gate BE/FE và ghi lại số test hiện tại.  
  - FE: `tsc` 0 lỗi, Vitest **188/188** pass, lint 0 error, build 22 route thành công.  
  - BE: `dotnet build` 0 warning/0 error; `make lint` dừng ở `format-check` vì checkout Windows đang CRLF (lỗi môi trường có sẵn, không do feature).
- [x] T004 Cập nhật `contracts/events/v1/README.md` và `docs/overview/roadmap.md`.
- [x] T005 Fix navbar active state dùng path khớp dài nhất + unit test.

## Phase 1 — Ổn định AI và observability

- [x] T101 Recommendation: `AddObservability`, `/metrics`, Prometheus scrape job, network scrape.
- [x] T102 Diet: bỏ hard-code 2.8s/2s, thêm option deadline, đo p95 và chốt budget D9.  
  - Đã thêm `RecommendationBudgetOptions` (mặc định 20s), bỏ cap 2s ở provider, thêm env `DIET_RECOMMENDATION_DEADLINE_MS`.  
  - Chưa đo 50 request live vì cần stack/provider; sẽ đo ở bước smoke.
- [ ] T103 RAG quality: đánh giá SC-003/SC-005, rerank/topK/chunking, cập nhật deferred.  
  - Chưa chạy `make kb-eval` trong lượt này; cần Docker + corpus + provider.
- [x] T104 Test/gate Phase 1 và cập nhật docs vận hành.  
  - Diet Unit 12/12, MealAiClient 3/3; Recommendation.Api/Diet.Api build 0 warning/error.

## Phase 2 — Diet AI

- [ ] T201 Contract Diet gRPC + REST specs.
- [ ] T202 Food micronutrient model, migration, seed USDA.
- [ ] T203 Meal plan domain/DB/use case/REST/tests.
- [ ] T204 Nutrient gaps use case/REST/tests.
- [ ] T205 Knowledge ask/search facade/REST/tests.
- [ ] T206 Auto target theo goal (nếu owner duyệt; nếu không ghi deferred).
- [ ] T207 Diet gRPC server: nutrition adherence/weekly summary.
- [ ] T208 Diet unit/integration tests.
- [ ] T209 FE Diet: meal plan, nutrient gaps, API/query/mapper/tests.
- [ ] T210 FE Knowledge: feature + trang `/knowledge` + nav + tests.

## Phase 3 — Progress AI

- [ ] T301 Progress gRPC clients + `internal_net` config.
- [ ] T302 Workout plan domain/DB/use case/REST/tests.
- [ ] T303 Longevity habits use case/REST + Diet gRPC client/tests.
- [ ] T304 Weekly digest scheduler + event + tests.
- [ ] T305 FE Progress: workout/longevity pages + tests.
- [ ] T306 (chuyển sang Phase 4) Digest opt-in.

## Phase 4 — Identity/Worker

- [ ] T306 Identity `receiveWeeklyDigest` preference + endpoint + FE toggle.
- [ ] T401 `UserVerifiedEvent` + outbox + tests.
- [ ] T402 Worker welcome consumer + tests.
- [ ] T403 Worker weekly digest consumer + tests.
- [ ] T404 Identity `GetUserContact` gRPC + tests.
- [ ] T405 Compose/env/docs cho digest và gRPC address.

## Phase 5 — CRUD, hardening, E2E

- [ ] T501 Update/delete custom food (soft delete) + FE + tests.
- [ ] T502 Delete body metric/sleep + FE + tests.
- [ ] T503 Service token cho toàn bộ gRPC client/server + negative tests.
- [ ] T504 Nginx rate limit + 429 ProblemDetails + test.
- [ ] T505 Playwright E2E + CI FE.
- [ ] T506 Meal update/delete event v2 (backlog kỹ thuật, chỉ làm nếu còn thời gian).

## Phase 6 — Close-out

- [ ] T601 `frontend-guide.md` + `backend-integration-guide.md` + `docs/api/api-spec.md`.
- [ ] T602 Cập nhật `meta.json`, `docs/overview/roadmap.md`, `docs/PROGRESS.md`, `WORK-LOG.md`.
- [ ] T603 Chạy `make check`, FE `tsc/test/lint/build`, E2E, performance/security.
- [ ] T604 Demo script + seed data.

## Ghi chú handoff

- Feature đổi HTTP surface → cập nhật `frontend-guide.md`.
- Feature đổi gRPC/event surface → cập nhật `backend-integration-guide.md`.
- Deploy thật/CD/TLS production ghi ở `deferred.md` (backlog).
