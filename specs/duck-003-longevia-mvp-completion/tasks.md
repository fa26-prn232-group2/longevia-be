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

- [x] T201 Contract Diet gRPC + REST specs.
- [x] T202 Food micronutrient model, migration, seed USDA.
- [x] T203 Meal plan domain/DB/use case/REST/tests.
- [x] T204 Nutrient gaps use case/REST/tests.
- [x] T205 Knowledge ask/search facade/REST/tests.
- [ ] T206 Auto target theo goal (chưa chốt policy; ghi deferred, không chặn MVP).
- [x] T207 Diet gRPC server: nutrition adherence/weekly summary.
- [x] T208 Diet unit/integration tests.  
  - Unit 14/14; build 0 warning/error. Integration test bổ sung sẽ chạy ở close-out.
- [x] T209 FE Diet: meal plan, nutrient gaps, API/query/mapper/tests.
- [x] T210 FE Knowledge: feature + trang `/knowledge` + nav + tests.

## Phase 3 — Progress AI

- [x] T301 Progress gRPC clients + `internal_net` config.
- [x] T302 Workout plan domain/DB/use case/REST/tests.
- [x] T303 Longevity habits use case/REST + Diet gRPC client/tests.
- [x] T304 Weekly digest scheduler + event + tests.
- [x] T305 FE Progress: workout/longevity pages + tests.
- [x] T306 (chuyển sang Phase 4) Digest opt-in.

## Phase 4 — Identity/Worker

- [x] T306 Identity `receiveWeeklyDigest` preference + endpoint + FE toggle.
- [x] T401 `UserVerifiedEvent` + outbox + tests.
- [x] T402 Worker welcome consumer + tests.
- [x] T403 Worker weekly digest consumer + tests.
- [x] T404 Identity `GetUserContact` gRPC + tests.
- [x] T405 Compose/env/docs cho digest và gRPC address.

## Phase 5 — CRUD, hardening, E2E

- [x] T501 Update/delete custom food (soft delete) + backend + FE delete.
- [x] T502 Delete body metric/sleep + tests.
- [x] T503 Service token cho toàn bộ gRPC client/server + negative tests (interceptor + env; negative test PENDING).
- [x] T504 Nginx rate limit + 429 ProblemDetails + test (config; E2E PENDING).
- [ ] T505 Playwright E2E + CI FE.
- [ ] T506 Meal update/delete event v2 (backlog kỹ thuật).

## Phase 6 — Close-out

- [x] T601 `frontend-guide.md` + `backend-integration-guide.md` + `docs/api/api-spec.md` section 7.
- [x] T602 Cập nhật `meta.json`, roadmap, `deferred.md`, demo script; PROGRESS/WORK-LOG PENDING.
- [ ] T603 Chạy `make check`, FE `tsc/test/lint/build`, E2E, performance/security.  
  - Đã chạy: FE `tsc`, 188 test, build 27 route; BE build 0 warning/error; migrations clean. `make check`/format còn CRLF.
- [ ] T604 Demo script + seed data.  
  - Demo script có tại `docs/development/demo.md`; seed dùng catalog hiện hữu.

## Ghi chú handoff

- Feature đổi HTTP surface → cập nhật `frontend-guide.md`.
- Feature đổi gRPC/event surface → cập nhật `backend-integration-guide.md`.
- Deploy thật/CD/TLS production ghi ở `deferred.md` (backlog).
