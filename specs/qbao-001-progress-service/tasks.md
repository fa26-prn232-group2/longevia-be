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

- [ ] T001 **PARTIAL** — Theo dõi Q01/Q08 tại research.md và plan.md; FR-001/FR-017/FR-019. Phase 2 đã được giao; Q01 vẫn PENDING và chỉ chặn date logic Phase 4–6; Q08 Bảo + Nguyên PENDING, không tự approve từ guard riêng Progress.
- [x] T002 **DONE** — Bốn project Clean Architecture tại src/Services/Progress/Progress.Domain/Progress.Domain.csproj, Progress.Application/Progress.Application.csproj, Progress.Infrastructure/Progress.Infrastructure.csproj, Progress.Api/Progress.Api.csproj; FR-019, SC-001. Sáu project gồm tests đăng ký longevia-be.slnx theo explicit developer authorization; dependency hướng vào trong, không service import, Domain không framework.
- [x] T003 **DONE** — src/Services/Progress/tests/Progress.UnitTests và Progress.IntegrationTests; SC-003/SC-007. xUnit, WebApplicationFactory, test-only keys/TimeProvider, PostgreSQL Testcontainers progress_db/progress_user và DI fixtures thực. Không RabbitMQ vì chưa publisher/consumer, không EF InMemory. Assertion DB/current user/validation/negative control cụ thể; xem verification.md.
- [ ] T004 **PARTIAL** — AuthTests.cs, HostTests.cs và FoundationOptionsTests.cs trong hai test project; FR-001/SC-003. Auth/config/subject tests chạy thật; missing kid mutation đỏ 401→200 rồi khôi phục xanh. Q08 chưa có đồng thuận Bảo + Nguyên; chưa entity/endpoint business nên không claim resource ownership 404 hay persistence hai-user đã kiểm chứng.
- [ ] T005 **PARTIAL** — Progress.Api/Program.cs, Auth/, Extensions/HostExtensions.cs và ProgressExceptionHandler.cs; FR-001/FR-017. Shared ES256/current user, local ADR guards, validation filter, Problem Details, DI và TimeProvider; không tạo IClock trùng chức năng. Host chạy/test xanh; security acceptance chờ Q08, không sửa Shared/Identity.
- [x] T006 **DONE** — Progress.Infrastructure/Persistence/ProgressDbContext.cs, ProgressDatabaseOptions.cs, ProgressDatabaseInitializer.cs và IUnitOfWork; FR-001/FR-019. DbContext là UoW, không thêm wrapper EfUnitOfWork vô ích. Xác minh DB trước traffic, MigrateAsync khi có migration; chưa entity nên không migration rỗng/EnsureCreated/DDL giả. PostgreSQL thật xác nhận DB/user/schema rỗng, init lặp, cancellation và fail-start.
- [x] T007 **DONE** — HostExtensions.cs, Program.cs, Dockerfile, deployments/docker-compose.yml, deployments/prometheus/prometheus.yml và scripts/smoke/progress-smoke.ps1; SC-006/SC-007. Được Bảo cấp quyền hạ tầng, không chờ Duy. AddObservability("ProgressService"), scrape, health/Swagger đã hoạt động; đã có evidence thật: container healthy, gateway prefix stripping đúng, Prometheus target UP, metric tăng (32 -> 45), Grafana datasource & dashboard kiểm chứng thành công.
- [ ] T008 **PARTIAL** — verification.md; SC-001/FR-019. Ghi evidence/gates thực và blocker Q08; không claim toàn Phase 2 complete hoặc bắt đầu Phase 3 khi security acceptance chưa duyệt.

## Phase 3 — Fasting — US2

Mục tiêu: start/current/end, concurrency và retry. Kiểm thử độc lập: 975 phút qua
đêm, 20 start chỉ 1 active/user; không gọi Identity hoặc Diet.

- [ ] T009 [US2] Chốt Q03 và schema start/end/current + error catalog tại specs/qbao-001-progress-service/contracts/README.md sau approval; FR-006/FR-007/FR-008/FR-009; sau T008; BLOCKED Q03/Q08; verification: Custom bounds, target mismatch, future timestamps, no-active, replay/conflict và retention đều có người duyệt, không còn proposal lẫn accepted.
- [ ] T010 [US2] Viết test trước tại src/Services/Progress/tests/Progress.UnitTests/Fasting/FastingSessionTests.cs và Progress.IntegrationTests/Fasting/FastingTests.cs; FR-006–FR-009/SC-002/SC-003/SC-004; sau T009; verification: preset/Custom, 975 phút, sát target, remaining không âm, same/changed retry payload, no-current, ownership, 20 concurrent start/end race đỏ đúng lỗi.
- [ ] T011 [US2] Tạo src/Services/Progress/Progress.Domain/Entities/FastingSession.cs và Domain/Constants/FastingProtocol.cs; FR-006/FR-008; sau T010; constraints: "Chỉ 16:8, 18:6, 20:4, Custom", "InProgress hoặc Completed", "EndedAt không trước StartedAt"; verification: test domain xanh, quá target không auto-complete theo policy đã duyệt.
- [ ] T012 [US2] Cấu hình persistence tại src/Services/Progress/Progress.Infrastructure/Persistence/Configurations/FastingSessionConfiguration.cs và Migrations; FR-007; sau T011; constraints: "unique filtered index UserId ở các hàng active (EndedAt IS NULL)"; retry receipt nếu Q03 chọn, unique UserId/operation/key; verification: DB thật 20 start chỉ một active, receipt/session cùng transaction, rollback không để receipt mồ côi.
- [ ] T013 [US2] Tạo use cases/DTO/validators/mappers tại src/Services/Progress/Progress.Application/Features/Fasting/ và Progress.Api/Endpoints/FastingEndpoints.cs; FR-001/FR-006–FR-009; sau T012; verification: 3 route theo contract đã duyệt, atomic end/start, clock một mốc/current, protocol/Custom/timestamp validation, lỗi tập trung và ownership tests xanh.
- [ ] T014 [US2] Ghi evidence fasting tại specs/qbao-001-progress-service/verification.md; SC-002/SC-003/SC-004; sau T013; verification: unit + DB integration/negative control và gate phase xanh; ghi active count, elapsed/remaining, replay status, 975 phút thực tế.

## Phase 4 — Water & Sleep — US3

Mục tiêu: append nước/tổng ngày và sleep duration/quality. Kiểm thử độc lập:
350+650=1000 ml, giấc ngủ 23:00–07:00=8 giờ; không cần Diet/Identity.

- [ ] T015 [US3] Chốt Q01/Q05 và schema water/today, sleep/log tại specs/qbao-001-progress-service/contracts/README.md; FR-010/FR-011/FR-012/FR-017; sau T008 (roadmap thực hiện sau T014); BLOCKED Q01/Q05; verification: source timezone, day assignment, target default, amount bounds/type, overlap, multiple sleep, empty/percent policy có approval.
- [ ] T016 [US3] Viết test nước trước tại src/Services/Progress/tests/Progress.IntegrationTests/Water/WaterTests.cs; FR-001/FR-010/FR-011/FR-017, SC-003/SC-004; sau T015; verification: 350+650, ngày rỗng=0, target/percent, rollover zone, concurrent sums, invalid amount, user B, đỏ đúng lỗi.
- [ ] T017 [US3] Tạo src/Services/Progress/Progress.Domain/Entities/WaterLog.cs và Progress.Infrastructure/Persistence/Configurations/WaterLogConfiguration.cs cùng migration chỉ water; FR-010; sau T016; constraints: "AmountMl dương", "thời điểm ghi do server"; bounds/kiểu theo Q05 approved; verification: append log/SUM không lost update, index UserId/day, schema không thêm target table nếu chọn constant.
- [ ] T018 [US3] Tạo use cases/validator/DTO tại src/Services/Progress/Progress.Application/Features/Water/ và Progress.Api/Endpoints/WaterEndpoints.cs; FR-010/FR-011; sau T017; verification: 2 route water/log và water/today, timezone Q01, error/ownership, T016 xanh; không tự thêm request loggedAt hoặc target endpoint.
- [ ] T019 [US3] Viết test ngủ trước tại src/Services/Progress/tests/Progress.UnitTests/Sleep/SleepDurationTests.cs và Progress.IntegrationTests/Sleep/SleepTests.cs; FR-001/FR-012/FR-017, SC-003/SC-004; sau T015; verification: 8h qua đêm, DST nếu zone áp dụng, quality lạ, wake-before-sleep, duplicate/overlap/concurrent theo policy, user B, đỏ đúng lỗi.
- [ ] T020 [US3] Tạo src/Services/Progress/Progress.Domain/Entities/SleepLog.cs và Progress.Infrastructure/Persistence/Configurations/SleepLogConfiguration.cs cùng migration, use cases tại Progress.Application/Features/Sleep/ và Progress.Api/Endpoints/SleepEndpoints.cs; FR-012; sau T019/T018 (schema/DI tuần tự); constraints: "Quality: Poor, Fair, Good, Excellent"; duration/overlap theo Q05 duyệt; verification: DB thật bảo vệ race overlap nếu cấm, đúng ngày sleep, T019 xanh, không business trong endpoint.
- [ ] T021 [US3] Ghi evidence water/sleep tại specs/qbao-001-progress-service/verification.md; SC-003/SC-004/FR-017; sau T020; verification: gate phase + scoped tests xanh, ghi totals/duration/rollover và negative control; không đổi điều kiện streak thành water/sleep.

## Phase 5 — Body Metrics & BMI — US1

Mục tiêu: snapshot BMI bất biến và lịch sử. BLOCKED tích hợp Nguyên Q02,
policy Q01/Q04/Q07; chưa có canonical height contract/ADR không được code adapter giả.
Q10 là dependency ngược Diet cần cân nặng; T038 chỉ theo dõi quyết định, không chặn BMI core.

- [ ] T038 [US1] Theo dõi Q10 Latest Weight Contract for Diet BMR/TDEE tại specs/qbao-001-progress-service/research.md, contracts/README.md và deferred.md; FR-019, SC-001; sau human review Phase 1, độc lập T022–T027; PENDING APPROVAL Bảo/Huy/Nguyên/lead; verification: ghi latest/asOf/effective-time, absence/null vs unavailable/stale, future/backdate/provenance, refresh policy, deadline và ADR direction, provider Progress/consumer Diet; không tạo RPC/event/adapter hoặc tính BMR/TDEE, implementation chỉ feature riêng sau approval.

- [ ] T022 [US1] Ghi phê duyệt Q02/Q04/Q07 và tham chiếu canonical Identity contract/ADR tại specs/qbao-001-progress-service/research.md và contracts/README.md; FR-002–FR-005/FR-018; sau T008 (roadmap sau T021); BLOCKED Nguyên/lead; verification: height int proposal vs decimal được reconcile, presence/backdate/missing/unavailable, bounds/category/precision, pagination wire được duyệt; không tự sửa Identity/contracts.
- [ ] T023 [US1] Viết tests trước tại src/Services/Progress/tests/Progress.UnitTests/Metrics/BmiTests.cs và Progress.IntegrationTests/Metrics/MetricsTests.cs; FR-001–FR-005/FR-017/FR-018, SC-003/SC-004/SC-005; sau T022; verification: 25/300/sát ngoài, optional biomarker bounds/null/pair, BMI/category thresholds, missing/timeout, 70kg/175cm, BR-05, two-user, page/date/tie/invalid range đỏ đúng lý do.
- [ ] T024 [US1] Tạo src/Services/Progress/Progress.Domain/Entities/BodyMetricLog.cs và Progress.Infrastructure/Persistence/Configurations/BodyMetricLogConfiguration.cs cùng migration; FR-002/FR-003/FR-004; sau T023; constraints: "WeightKg ... 25–300 kg", "Int nullable" cho BP/RHR, snapshot BMI/height/category không recalculate; verification: schema theo precision đã duyệt, index UserId/LoggedAt/Id, không FK DB khác, BR-05 test xanh.
- [ ] T025 [US1] Tạo height abstraction tại src/Services/Progress/Progress.Application/Abstractions/IBodyProfileProvider.cs và adapter tại Progress.Infrastructure/Adapters/Grpc/BodyProfileProvider.cs; FR-003/FR-018; sau T024/T022; BLOCKED canonical contract + ADR; verification: deadline/error/presence theo approved contract, không gọi REST/import Identity source, contract tests against provider thật, timeout không ghi metric dở.
- [ ] T026 [US1] Tạo metric write/history use cases/validator/mapper tại src/Services/Progress/Progress.Application/Features/Metrics/ và Progress.Api/Endpoints/MetricEndpoints.cs; FR-001–FR-005/FR-017; sau T025; verification: 2 route approved, request không HeightCm, snapshot một lần lúc ghi, query scope user, Count trước Skip/Take, stable order, Q07 adapter không đổi Shared, T023 xanh.
- [ ] T027 [US1] Ghi evidence metrics tại specs/qbao-001-progress-service/verification.md; SC-003/SC-004/SC-005; sau T026; verification: scoped unit/DB/contract tests + negative control và gate xanh, assert BMI cũ giữ nguyên sau đổi height, số page/count/boundary/date chính xác.

## Phase 6 — Dashboard, Streaks & Integration — US4

Mục tiêu: summary đúng dữ liệu, streak có Diet đầy đủ; BLOCKED Q06/Q09 và canonical
meal contract/ADR. Không coi projection fixture là tích hợp production hoàn tất.

Diet MVP hiện create-only; T028–T033 phải kiểm chứng create/idempotency/business
date/bootstrap/completeness và Diet → Progress E2E. Các case edit/delete/change-date,
revision/tombstone dưới đây chỉ bắt buộc khi lifecycle mở rộng được approve và
producer hỗ trợ; nếu chưa có thì ghi deferred/compatibility plan rõ, không giả endpoint
hoặc coi các case future đã pass. Q09 vẫn BLOCKED/PENDING INTEGRATION tới approval.

- [ ] T028 [US4] Chốt Q06/Q09 và ghi approved Diet contract/topology/backfill/completeness tại specs/qbao-001-progress-service/contracts/README.md và research.md; FR-013–FR-016/FR-018; sau T027/T014/T021; BLOCKED Huy/Bảo/lead/Duy; verification: revision/delete/tombstone/timezone/late/bootstrap, ownership và baseline/windows/null/error đã được duyệt, không sửa producer.
- [ ] T029 [US4] Viết tests dashboard/streak trước tại src/Services/Progress/tests/Progress.UnitTests/Dashboard/StreakTests.cs và Progress.IntegrationTests/Dashboard/DashboardTests.cs; FR-001/FR-013–FR-018, SC-003/SC-004/SC-005; sau T028; verification: active 1,2,3,5 best=3, current today/yesterday/gap, meal+metric union, water-only không active, 72/70/68.5 delta -1.5, empty/null, sleep mean/fasting boundaries, hai user đỏ đúng lý do.
- [ ] T030 [US4] Tạo activity contribution/projection tại src/Services/Progress/Progress.Infrastructure/Persistence/Configurations/ActivityContributionConfiguration.cs cùng migrations và Progress.Application/Features/Dashboard/ActivityProjectionUseCase.cs; FR-014/FR-016; sau T029; constraints: "Khóa (Source, SourceLogId)", "meal lưu tombstone revision"; verification: delete không xóa ngày còn metric/meal khác, metric contribution cùng transaction write; không reference Diet entities.
- [ ] T031 [US4] Tạo consumer/adapter và bootstrap tại src/Services/Progress/Progress.Infrastructure/Messaging/MealActivityConsumer.cs và Progress.Application/Features/Dashboard/ReconcileMealActivityUseCase.cs; FR-014/FR-016/FR-018; sau T030/T028; BLOCKED canonical contract; verification: DB/broker thật duplicate/late/out-of-order/edit chuyển ngày/delete, unknown optional fields, inbox rollback khi lỗi, watermark/replay race đúng; routing/retry/DLQ approved, không claim metrics-only streak đủ.
- [ ] T032 [US4] Tạo summary query/DTO tại src/Services/Progress/Progress.Application/Features/Dashboard/GetDashboardSummaryUseCase.cs và Progress.Api/Endpoints/DashboardEndpoints.cs; FR-001/FR-013/FR-014/FR-015/FR-018; sau T031; verification: đủ 8 field canonical, snapshot BMI, delta tuần trước, current/best/window theo duyệt, incompleteness rõ, T029 xanh.
- [ ] T033 [US4] Viết và chạy E2E tại src/Services/Progress/tests/Progress.IntegrationTests/EndToEnd/ProgressJourneyTests.cs; SC-007/FR-001/FR-003/FR-007/FR-010/FR-012/FR-014/FR-016; sau T032; verification: HTTP qua Nginx, PostgreSQL/RabbitMQ thật, approved meal producer fixture, assert snapshot, water=1000, sleep=8h, fasting=975 phút, streak chính xác và rollback/retry; negative control đỏ rồi xanh.
- [ ] T034 [US4] Tạo benchmark tại src/Services/Progress/tests/Progress.IntegrationTests/Performance/ProgressLoadTests.cs và ghi specs/qbao-001-progress-service/verification.md; SC-006; sau T033; verification: plan fixture 10.000 metric/user, warmup 30s, 100RPS/5 phút, ghi read/write p95 và error rate trên môi trường cố định; không tự giảm target hoặc claim pass khi chưa đo.
- [ ] T035 [US4] Kiểm tra toàn feature tại specs/qbao-001-progress-service/verification.md theo quickstart.md; FR-019/SC-001–SC-007; sau T034; verification: schema/contract ownership, toàn bộ test/negative-control/E2E/load evidence, gates theo AGENTS xanh; phần thiếu vẫn blocked, không tự đánh dấu hoàn tất.
- [ ] T036 [US4] Tạo specs/qbao-001-progress-service/frontend-guide.md theo template khi HTTP surface thực sự triển khai; FR-002/FR-005/FR-006/FR-008/FR-009/FR-010/FR-011/FR-012/FR-013; sau T035; verification: đối chiếu source/canonical đếm 9 route hoặc giải thích delta đã duyệt, đếm field/enum/status/error, auth/retry/pagination/null; chỉ READY_FOR_FRONTEND khi evidence và counted sync hoàn tất.
- [ ] T037 [US4] Tạo specs/qbao-001-progress-service/backend-integration-guide.md theo template cho height/meal integrations đã approved; FR-003/FR-016/FR-018; sau T035; verification: đếm RPC/message/field/enum/error/trailer/deadline, event field/routing key/topology, producer/consumer versions, replay/bootstrap và tests; chỉ READY_FOR_BACKEND khi evidence và counted sync hoàn tất.

## Thứ tự, dependency và khả năng song song

- Sau human giao phase/shared slot: T002→T003 độc lập Q01/Q08 và Identity/Diet.
  Phần Q08 của T001 + remediation owner → T004→T005→T006→T007→T008; security
  acceptance không được miễn. Q01 chỉ chặn date logic Phase 4–6, không skeleton.
  T038 theo dõi Q10 độc lập chuỗi core, không vòng phụ thuộc; không code provider
  trong core. Gate đỏ dừng, không bỏ test/migration/scan để đi tiếp.
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
