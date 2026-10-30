# Tasks: Progress Service — roadmap Phase 2–6

Đầu vào: [spec](spec.md), [plan](plan.md), [research](research.md),
[data model](data-model.md), [proposal](contracts/README.md).
Phase 1 chỉ planning; Phase 2 được developer giao ngày 2026-10-25 sau PR #9 merge.
Trạng thái T001–T008 bên dưới chỉ được xác nhận bằng evidence trong verification.md.
US1/US2/US3/US4 tương ứng bốn US-PROG trong spec.
Mọi path source dưới đây là dự kiến. Các task có nhãn BLOCKED không được tự gỡ
bằng mock/fallback; cần evidence approve và canonical contract ở producer PR.

Phase 1 không thêm/sửa/xóa HTTP endpoint, DTO đang chạy, RPC, event hoặc topology;
chưa cần guide tích hợp thực thi và không đặt trạng thái READY. T036–T037 dành cho
Phase 2–6 khi thực sự đổi surface. Việc ngoài scope ở deferred, không thành task code.

## Phase 2 — Foundation & Persistence

Phạm vi developer cho phép tiếp tục phần độc lập khi Q08 chưa được nghiệm thu.
Steer 2026-10-25 cho Bảo quyền integration riêng Progress vào solution/Compose/
Prometheus/routing/env; không chờ Duy, không có Technical Lead được chỉ định.
Chỉ Bảo + Nguyên xác nhận Q08 liên service. Các reference lead/slot trong lịch sử
Phase 1 không phải blocker quyền hạ tầng của Phase 2.
Phase 2 không đổi RPC/event/topology hoặc giao tiếp liên service: không tạo
backend-integration-guide.md. HTTP operational surface có frontend-guide.md;
T036 vẫn dành cho handoff business Phase 3–6, không tick nhờ operational routes.

Mục tiêu: host Progress độc lập, DB riêng, bảo mật/quan sát được, chưa dựng schema business thiếu quyết định.
Kiểm chứng: app khởi động với config hợp lệ, fail khi config/DB invalid, 401 đúng,
health/metrics hoạt động, migration chỉ progress_db. Liên service chưa cần hoạt động.

- [x] T001 **DONE** — Theo dõi Q01/Q08 tại research.md và plan.md; FR-001/FR-017/FR-019. Q08-A JWT policy đã được Bảo + Nguyên **APPROVED** chính thức (policy strict: token ES256, kid hợp lệ, sub là GUID, fail-start khi thiếu public key; Shared Auth remediation do Nguyên sở hữu trong PR riêng của Identity); Q08-B resource ownership và error policy PENDING cho business endpoints; Q01 tiếp tục theo dõi cho Phase 4–6. Phase 2 hoàn tất mục tiêu theo dõi kiến trúc.
- [x] T002 **DONE** — Bốn project Clean Architecture tại src/Services/Progress/Progress.Domain/Progress.Domain.csproj, Progress.Application/Progress.Application.csproj, Progress.Infrastructure/Progress.Infrastructure.csproj, Progress.Api/Progress.Api.csproj; FR-019, SC-001. Sáu project gồm tests đăng ký longevia-be.slnx theo explicit developer authorization; dependency hướng vào trong, không service import, Domain không framework.
- [x] T003 **DONE** — src/Services/Progress/tests/Progress.UnitTests và Progress.IntegrationTests; SC-003/SC-007. xUnit, WebApplicationFactory, test-only keys/TimeProvider, PostgreSQL Testcontainers progress_db/progress_user và DI fixtures thực. Không RabbitMQ vì chưa publisher/consumer, không EF InMemory. Assertion DB/current user/validation/negative control cụ thể; xem verification.md.
- [x] T004 **DONE** — AuthTests.cs, HostTests.cs và FoundationOptionsTests.cs trong hai test project; FR-001/SC-003. Q08-A JWT policy đã được duyệt; 14 kịch bản auth (unsigned, wrong algorithm, invalid/missing/unknown kid, missing/empty/invalid/conflicting sub, current/previous key rotation) và configuration fail-start chạy thật pass 100% (cả local và CI). Nghiệm thu an toàn Phase 2 hoàn tất; resource ownership (403 vs 404 thuộc Q08-B) cho entity nghiệp vụ sẽ kiểm chứng ở từng phase tương ứng khi có schema.
- [x] T005 **DONE** — Progress.Api/Program.cs, Auth/, Extensions/HostExtensions.cs và ProgressExceptionHandler.cs; FR-001/FR-017. Q08-A đã được duyệt; cấu hình ES256 public-key-only, ProgressJwtOptionsValidator fail-start, Problem Details RFC 7807, centralized exception handling, DI và TimeProvider đã hoàn tất và kiểm chứng 100% tests xanh; không sửa Shared/Identity trong PR này.
- [x] T006 **DONE** — Progress.Infrastructure/Persistence/ProgressDbContext.cs, ProgressDatabaseOptions.cs, ProgressDatabaseInitializer.cs và IUnitOfWork; FR-001/FR-019. DbContext là UoW, không thêm wrapper EfUnitOfWork vô ích. Xác minh DB trước traffic, MigrateAsync khi có migration; chưa entity nên không migration rỗng/EnsureCreated/DDL giả. PostgreSQL thật xác nhận DB/user/schema rỗng, init lặp, cancellation và fail-start.
- [x] T007 **DONE** — HostExtensions.cs, Program.cs, Dockerfile, deployments/docker-compose.yml, deployments/prometheus/prometheus.yml và scripts/smoke/progress-smoke.ps1; SC-006/SC-007. Được Bảo cấp quyền hạ tầng, không chờ Duy. AddObservability("ProgressService"), scrape, health/Swagger đã hoạt động; đã có evidence thật: container healthy, gateway prefix stripping đúng, Prometheus target UP, metric tăng (32 -> 45), Grafana datasource & dashboard kiểm chứng thành công.
- [x] T008 **DONE** — verification.md; SC-001/FR-019. Ghi nhận toàn bộ evidence thực tế: Fast Gate, Release build, Progress tests (21 unit + 34 integration), toàn hệ thống (592 tests branch-only, 610 tests PR merge-ref), scan 27 projects không lỗ hổng, runtime smoke pass 100%, CI pass; Q08-A approved, Q08-B deferred, Q02 gRPC read direction đã ghi nhận. Phase 2 hoàn tất đầy đủ acceptance criteria để human review.

## Phase 3 — Fasting — US2

Mục tiêu: start/current/end, concurrency và retry. Kiểm thử độc lập: 975 phút qua
đêm, 20 start chỉ 1 active/user; không gọi Identity hoặc Diet.

- [x] T009 [US2] **DONE** — Chốt Q03 và schema start/end/current + error catalog tại specs/qbao-001-progress-service/contracts/README.md và research.md theo steer an toàn nhất: 16:8, 18:6, 20:4, Custom (1–24h), không tương lai, 1 active session qua PostgreSQL Filtered Unique Index, 409 conflict, idempotent end 200, 404 ownership hiding.
- [x] T010 [US2] **DONE** — FastingSessionTests.cs và FastingTests.cs; kiểm chứng chu kỳ 975 phút qua đêm (16.25h), remaining không âm, 20 concurrent start chỉ 1 thành công (19 mã 409), 404 ownership hiding giữa 2 users; kiểm chứng atomic end transition qua PostgreSQL advisory lock (pg_advisory_xact_lock theo sessionId), 2 concurrent end request khác timestamp chỉ 1 thành công (409 conflict), retry cùng timestamp replay 200 OK.
- [x] T011 [US2] **DONE** — FastingSession.cs, FastingProtocol.cs, FastingStatus.cs tại Progress.Domain; bất biến domain và tính toán ActualHours, IsGoalReached.
- [x] T012 [US2] **DONE** — FastingSessionConfiguration.cs với Filtered Unique Index `\"EndedAt\" IS NULL` và migration `20261024192855_AddFastingSessions`.
- [x] T013 [US2] **DONE** — FastingUseCases.cs, FastingValidators.cs, FastingDtos.cs, IFastingRepository.cs, FastingRepository.cs và FastingEndpoints.cs (/fasting/start, /fasting/end, /fasting/current).
- [x] T014 [US2] **DONE** — Ghi evidence fasting tại verification.md; scoped unit (36/36) + integration tests (9/9) pass 100%.

## Phase 4 — Water & Sleep — US3

Mục tiêu: append nước/tổng ngày và sleep duration/quality. Kiểm thử độc lập:
350+650=1000 ml, giấc ngủ 23:00–07:00=8 giờ; không cần Diet/Identity.

- [x] T015 [US3] **DONE** — Q01 UTC MVP được Bảo approve trực tiếp trong corrective; Q05 giữ nguyên: water target default 2500 ml constant, 1–5000 ml, percent không cap 100%, gán ngày theo UTC DateOnly; sleep duration <= 24h, cấm wokeAt <= sleptAt, cấm overlap thời gian ngủ, quality enum (Poor, Fair, Good, Excellent), gán ngày theo wake time.
- [x] T016 [US3] **DONE** — WaterTests.cs; kiểm chứng 350+650=1000 ml (40%), ngày rỗng=0 ml (0%), validation 1-5000 ml (400 Bad Request), cách ly dữ liệu hai người dùng pass 100%.
- [x] T017 [US3] **DONE** — WaterLog.cs, WaterLogConfiguration.cs, migration `20261024193519_AddWaterAndSleep`.
- [x] T018 [US3] **DONE** — WaterUseCases.cs, WaterValidators.cs, WaterDtos.cs, IWaterRepository.cs, WaterRepository.cs và WaterEndpoints.cs (/water/log, /water/today).
- [x] T019 [US3] **DONE** — SleepDurationTests.cs và SleepTests.cs; kiểm chứng 8h qua đêm 23:00-07:00, wokeAt <= sleptAt trả 400, overlap trả 409 conflict, cách ly hai người dùng; kiểm chứng concurrency-safe overlap qua PostgreSQL advisory lock (pg_advisory_xact_lock theo userId trong transaction), 5 concurrent overlapping requests chỉ đúng 1 thành công (4 mã 409), touching intervals (22-06 và 06-07) được cho phép.
- [x] T020 [US3] **DONE** — SleepLog.cs, SleepQuality.cs, SleepLogConfiguration.cs, SleepUseCases.cs, SleepValidators.cs, SleepDtos.cs, ISleepRepository.cs, SleepRepository.cs và SleepEndpoints.cs (/sleep/log).
- [x] T021 [US3] **DONE** — Ghi evidence water/sleep tại verification.md; unit + integration tests pass 100%.

## Phase 5 — Body Metrics & BMI — US1

Mục tiêu: snapshot BMI bất biến và lịch sử. Q02 functional integration DONE; secure production readiness BLOCKED service-caller auth,
policy Q01/Q04/Q07; chưa có canonical height contract/ADR không được code adapter giả.
Q10 là dependency ngược Diet cần cân nặng; T038 chỉ theo dõi quyết định, không chặn BMI core.

- [x] T038 [US1] **DONE (Q10 APPROVED & SERVER IMPLEMENTED)** — Q10 chính thức được phê duyệt bởi Bảo + Huy (ADR 003); canonical Protobuf contracts/progress/v1/progress.proto hoàn tất; triển khai ProgressGrpcService trên cổng HTTP/2 nội bộ 50051; 20 test cases và end-to-end Progress flow verified; Progress gRPC Server READY; Diet client & snapshot do Huy sở hữu.
- [x] T022 [US1] **DONE** — Ghi phê duyệt Q02/Q04/Q07 theo steer an toàn nhất: Q02 hướng gRPC read profile/height (decimal khớp PR #10 numeric(5,2)); Q04 cân nặng 25-300kg, biomarker limits, huyết áp đi cặp và systolic > diastolic, BMI snapshot bất biến theo BR-05; Q07 pagination wire adapter (pageNumber, pageSize, totalCount, totalPages) với domain PagedResult.
- [x] T023 [US1] **DONE** — BmiTests.cs và MetricsTests.cs; kiểm chứng 70kg/175cm -> BMI 22.86 NormalWeight, boundaries 25/300kg, cặp huyết áp, phân loại theo raw unrounded BMI (99.984kg/200cm raw=24.996 NormalWeight dù làm tròn 25.00), kiểm chứng toàn diện các ngưỡng 18.5, 25.0, 30.0 (just below, exact, just above), phân biệt 422 (thiếu chiều cao), 404 (profile not found), 503 (Identity unavailable), phân trang và cách ly 2 user pass 100%.
- [x] T024 [US1] **DONE** — BodyMetricLog.cs, BmiCategory.cs, BodyMetricLogConfiguration.cs, migration `20261024193906_AddBodyMetrics`.
- [x] T025 [US1] **DONE — FUNCTIONAL HEIGHT GRPC** — Canonical GetUserHeight client thật và actual Identity provider đã verified; 175/175.25 cm, BMI snapshot bất biến, errors/deadline/cancellation. Service-caller auth chưa có, secure readiness BLOCKED độc lập.
- [x] T026 [US1] **DONE** — MetricUseCases.cs, MetricValidators.cs, MetricDtos.cs, IBodyMetricRepository.cs, BodyMetricRepository.cs và MetricEndpoints.cs (/metrics, /metrics/history).
- [x] T027 [US1] **DONE** — Ghi evidence metrics tại verification.md; unit + integration tests pass 100%.

## Phase 6 — Dashboard, Streaks & Integration — US4

Mục tiêu: summary đúng dữ liệu, streak có Diet đầy đủ; BLOCKED Q06/Q09 và canonical
meal contract/ADR. Không coi projection fixture là tích hợp production hoàn tất.

Diet MVP hiện create-only; T028–T033 phải kiểm chứng create/idempotency/business
date/bootstrap/completeness và Diet → Progress E2E. Các case edit/delete/change-date,
revision/tombstone dưới đây chỉ bắt buộc khi lifecycle mở rộng được approve và
producer hỗ trợ; nếu chưa có thì ghi deferred/compatibility plan rõ, không giả endpoint
hoặc coi các case future đã pass. Q09 vẫn BLOCKED/PENDING INTEGRATION tới approval.

- [x] T028 [US4] **DONE** — Chốt Q06/Q09 theo steer an toàn nhất: Active day = hợp ngày có MealLog và BodyMetricLog (nước, ngủ, fasting không tạo streak độc lập); weight delta so tuần trước (now - 7 days, 72->70->68.5 delta -1.5kg); current streak chạy đến hôm nay hoặc neo hôm qua, gap >= 2 ngày trả 0; best streak dài nhất lịch sử.
- [x] T029 [US4] **DONE** — StreakTests.cs và DashboardTests.cs; kiểm chứng active 1,2,3,5 best=3 current=1, anchor hôm qua=3, gap 2 ngày=0; dashboard summary đầy đủ 8 trường canonical, delta -1.5kg, streak 2 ngày hợp meal+metric pass 100%.
- [x] T030 [US4] **DONE** — ActivityContribution.cs, ActivityContributionConfiguration.cs, migration `20261024194214_AddActivityContributions`, IActivityContributionRepository.cs, ActivityContributionRepository.cs.
- [x] T031 [US4] **DONE (MERGED DIET INTEROP VERIFIED)** — Diet PR #12 đã merged vào main; consumer/inbox/projection và đối soát snapshot JSON v1 với progress_db (RepeatableRead, batch 500) đã kiểm chứng; runbook vận hành đối soát 4 bước sẵn sàng cho rollout production.
- [x] T032 [US4] **DONE** — DashboardUseCases.cs, DashboardDtos.cs, StreakCalculator.cs và DashboardEndpoints.cs (/dashboard/summary).
- [x] T033 [US4] **DONE (SCOPED PROGRESS INTEGRATION)** — DashboardTests.cs và MealActivityConsumerTests.cs tích hợp toàn bộ luồng metrics, water, sleep, fasting và meal activity projection pass 100% trên scoped test fixture; full live multi-service E2E journey qua Gateway + Identity + Diet + RabbitMQ được ghi nhận là PENDING INTEGRATION chờ merge upstream PRs.
- [ ] T034 [US4] **PROVISIONAL / BENCHMARK PENDING** — Đánh giá performance sơ bộ: cấu trúc database indexes trên UserId + LoggedAt/Date; các test tích hợp đo đạc thời gian đáp ứng ở mức vài milliseconds; benchmark chính thức với target workload: PENDING môi trường load test.
- [x] T035 [US4] **DONE — CORRECTIVE VERIFIED** — Progress 178/178, full solution 733/733, 0 skip; format/Debug/Release/vulnerability pass. Evidence hiện hành tại verification.md, số đếm cũ là lịch sử.
- [x] T036 [US4] **DONE** — Cập nhật specs/qbao-001-progress-service/frontend-guide.md: trạng thái PARTIAL_READY_FOR_FRONTEND (Fasting, Water, Sleep, Dashboard query sẵn sàng; POST /metrics phụ thuộc runtime Identity height).
- [x] T037 [US4] **DONE (INTEGRATION GUIDES & HANDOFFS COMPLETED)** — Backend integration guide hoàn tất cho cả hai chiều Identity Height (consumer) và Diet Weight (provider); handoff Q10 hoàn tất cho Huy; Q01 UTC nội bộ Progress approved, Huy acknowledgment pending; streak production readiness phụ thuộc rollout replay thực tế.

## Thứ tự, dependency và khả năng song song

- **Tiến trình nghiệm thu Phase 2 (Foundation & Persistence)**:
  - Q08-A JWT Authentication Policy đã APPROVED bởi Bảo + Nguyên (ES256, kid hợp lệ, sub là GUID, fail-start khi thiếu key).
  - Progress-local guard nghiêm ngặt đã triển khai hoàn tất và kiểm chứng đầy đủ trong T004/T005, độc lập với Shared Auth.
  - T001–T008 đã hoàn tất thỏa mãn toàn bộ acceptance criteria của Phase 2 foundation và pass 100% các gates.
- **External Shared Auth Remediation**:
  - Do Nguyên (`nguyen`, owner Identity) sở hữu trong PR riêng của Identity.
  - Được theo dõi như external follow-up cross-service, không chặn Progress Phase 2 và không nằm trong PR #11 của Progress.
- **Resource Ownership & Error Policy (Q08-B)**:
  - PENDING cho các business resource endpoints (403 vs 404, Problem Details mapping, two-user isolation).
  - Sẽ được quyết định và kiểm chứng khi triển khai các entity nghiệp vụ cụ thể ở Phase 3 (Fasting), Phase 4 (Water/Sleep), Phase 5 (Body Metrics), Phase 6 (Dashboard); không tuyên bố đã test ở Phase 2.
- **Future Phases Prerequisites**:
  - Phase 3 Fasting: Q03 và Q08-B (resource ownership và error policy) cần chốt trước/cùng khi triển khai; concurrency, retries, current.
  - Phase 4 Water/Sleep: Q01 UTC nội bộ đã APPROVED; Q05/Q08-B theo quyết định hiện hành; append nước, sleep qua đêm.
  - Phase 5 Body Metrics/BMI: Q02 hướng gRPC read đã APPROVED (chiều cao decimal theo PR #10); chờ canonical RPC schema trong identity.proto, runtime provider implementation của Identity, và Q04/Q07/Q08-B (Q01 UTC nội bộ đã APPROVED); T038 theo dõi Q10 độc lập.
  - Phase 6 Dashboard/Streak: Chờ Q06/Q09 và Diet meal activity contract/bootstrap.
  - Không tạo phụ thuộc nhân tạo bắt Phase 2 hoặc các phase sau phải chờ PR Shared Auth của Nguyên khi Progress guard cục bộ đã an toàn độc lập.
- Roadmap yêu cầu: Phase 2 → 3 → 4 → 5 → 6. Về nghiệp vụ US2 và US3 độc lập
  Identity/Diet; US1 chờ Nguyên; US4 đầy đủ chờ cả ba story và Huy.
- T015/T022 có thể chuẩn bị approval song song sau foundation, nhưng không chạy
  migration/DI đồng thời. T016 và T019 là ví dụ hai bộ test độc lập có thể chuẩn bị
  song song sau T015; không đánh dấu [P] ở task còn chờ prerequisite chưa hoàn thành.
- US1: BMI unit test và history fixture có thể chia khi Q02/Q04/Q07 đã duyệt.
  US2: arithmetic unit test và race integration fixture khác file có thể chuẩn bị
  cùng lúc sau T009. US4: pure streak cases và meal broker fixture khác file sau T028.
- Một người sở hữu tasks.md; một migration stream. Mỗi story tests trước/cùng code,
  model→persistence→use cases→endpoint→verification, không ghi code service khác.

## Chiến lược delivery

MVP increment đầu tiên có thể demo Fasting sau foundation/Q03, chưa cần BMI/Diet.
Tiếp theo Water/Sleep, rồi BMI có contract và Dashboard đầy đủ. Demo từng phần
không đồng nghĩa hoàn tất feature. Phase 1 dừng ở tài liệu/review; không bắt đầu
T001 vì chỉ có yêu cầu planning trong phiên này. AI/digest/wearable ở deferred.

## Corrective PR #13 — phạm vi được Bảo ủy quyền

- [x] T039 **DONE** — Đối soát JSON v1 thực với progress_db trong read-only RepeatableRead, batch parameterized; kiểm tra exit code qua process thật, không PASS khi skip DB.
- [x] T040 **DONE** — Canonical IDs, partial inbox/contribution recovery, conflicting duplicates và structured unique violation; atomic transaction, negative controls.
- [x] T041 **DONE** — RabbitMQ/PostgreSQL Testcontainers thật, routing/runtime error queue, retry/redelivery/restart; giữ CI chạy test thật, synthetic transport tách actual Diet harness.
- [x] T042 **DONE** — Q01 APPROVED FOR PROGRESS MVP BY BẢO: UTC day/week/history, fixed midnight tests; Diet BusinessDate bất biến, Huy acknowledgment PENDING; không rebucket lịch sử.
- [x] T043 **DONE — LOCAL GATES VERIFIED** — Artifacts và full gates hoàn tất; corrective commit này được xuất bản vào PR #13 giữ Draft, SHA/CI final theo báo cáo PR; không merge PR #12/#13. Public dashboard schema không đổi, completeness readiness vẫn bị chặn trước rollout thật.
## Identity Height gRPC — lượt 2026-10-26

- [x] T044 **DONE** — Đồng bộ main/PR #14 không rewrite lịch sử, giữ consumer/inbox/reconcile/Q01; canonical proto giữ nguyên.
- [x] T045 **DONE** — Generated gRPC Client, DI/channel reuse, explicit validated address/deadline, decimal conversion và safe status mapping.
- [x] T046 **DONE — SCOPED VERIFIED** — 17 gRPC scenarios thật/controlled và 9 options unit cases; immutable snapshots, user isolation, cancellation, missing/fault cases; không đổi schema để giả height missing.
- [x] T047 **DONE** — Docker build canonical proto, Compose endpoint/network/listeners; regression suite và EF model consistency.
- [x] T048 **DONE LOCAL GATES** — Full format/Debug/Release,766tests và vulnerability PASS; artifacts đã cập nhật. Scoped merge commit/push và CI đúng published HEAD được ghi trên PR #13 Draft, không dùng CI cũ.
- Client-facing response fields không đổi; thêm mapping error 502 trong Problem Details hiện hữu và frontend guide.
- Counted integration sync: 1 RPC, 2 messages, 2 fields đối chiếu canonical generated client và actual provider; chưa READY_FOR_BACKEND toàn feature vì caller-auth và rollout dependencies.
## Phase 7 — Q10 Weight Provider & Diet PR #12 Integration Close-Out (2026-10-30)

- [x] T049 **DONE** — Đồng bộ nhánh main (merge Diet PR #12, commit 4c62273) vào feat/progress-service an toàn, giải quyết xung đột MealLoggedEvent theo canonical main, bảo toàn 100% chức năng Progress.
- [x] T050 **DONE** — Ban hành ADR 003 (docs/decisions/003-diet-to-progress-weight-grpc.md) ghi nhận phê duyệt chính thức Q10 giữa Bảo (Progress) và Huy (Diet); cập nhật contracts/progress/v1/progress.proto và README.md với ngữ nghĩa as-of clamping, lọc số đo tương lai, ordering tất định, numeric precision int32 x100, absence vs error handling.
- [x] T051 **DONE** — Triển khai ProgressGrpcService (src/Services/Progress/Progress.Api/Grpc/ProgressGrpcService.cs), Kestrel HTTP/2 listener trên cổng 50051 (PROGRESS:GRPC_PORT), tích hợp DI AddGrpc() và MapGrpcService(), cập nhật Dockerfile bao gồm contracts/progress/.
- [x] T052 **DONE** — Xây dựng bộ kiểm thử Weight gRPC toàn diện (ProgressWeightGrpcTests.cs & ProgressWeightConfigurationTests.cs) bao phủ đủ 20 test cases bắt buộc và luồng End-to-End Progress flow (Identity Height gRPC -> Progress BMI snapshot bất biến -> Progress Weight gRPC).
- [x] T053 **DONE** — Tích hợp và kiểm chứng producer merged từ Diet PR #12, xuất snapshot JSON v1, SHA-256 verification và đối soát cơ sở dữ liệu progress_db; ban hành Runbook 4 bước vận hành cho Huy, Bảo, Duy.
- [x] T054 **DONE** — Đối chiếu quyết định Q01 UTC (nội bộ Progress approved; cross-service Huy acknowledgment pending); hoàn thiện backend-integration-guide.md, frontend-guide.md, research.md, plan.md và handoff cho Huy.
- [x] T055 **DONE** — Chạy toàn bộ quality gates (format, build Release, unit tests, vulnerability scan), commit theo Conventional Commits và xuất bản PR #13 giữ Draft.
