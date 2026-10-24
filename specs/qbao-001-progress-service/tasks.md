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
- [x] T010 [US2] **DONE** — FastingSessionTests.cs và FastingTests.cs; kiểm chứng chu kỳ 975 phút qua đêm (16.25h), remaining không âm, 20 concurrent start chỉ 1 thành công (19 mã 409), 404 ownership hiding giữa 2 users.
- [x] T011 [US2] **DONE** — FastingSession.cs, FastingProtocol.cs, FastingStatus.cs tại Progress.Domain; bất biến domain và tính toán ActualHours, IsGoalReached.
- [x] T012 [US2] **DONE** — FastingSessionConfiguration.cs với Filtered Unique Index `\"EndedAt\" IS NULL` và migration `20261024192855_AddFastingSessions`.
- [x] T013 [US2] **DONE** — FastingUseCases.cs, FastingValidators.cs, FastingDtos.cs, IFastingRepository.cs, FastingRepository.cs và FastingEndpoints.cs (/fasting/start, /fasting/end, /fasting/current).
- [x] T014 [US2] **DONE** — Ghi evidence fasting tại verification.md; scoped unit (36/36) + integration tests (5/5) pass 100%.

## Phase 4 — Water & Sleep — US3

Mục tiêu: append nước/tổng ngày và sleep duration/quality. Kiểm thử độc lập:
350+650=1000 ml, giấc ngủ 23:00–07:00=8 giờ; không cần Diet/Identity.

- [x] T015 [US3] **DONE** — Chốt Q01/Q05 theo steer an toàn nhất: water target default 2500 ml constant, 1–5000 ml, percent không cap 100%, gán ngày theo UTC DateOnly; sleep duration <= 24h, cấm wokeAt <= sleptAt, cấm overlap thời gian ngủ, quality enum (Poor, Fair, Good, Excellent), gán ngày theo wake time.
- [x] T016 [US3] **DONE** — WaterTests.cs; kiểm chứng 350+650=1000 ml (40%), ngày rỗng=0 ml (0%), validation 1-5000 ml (400 Bad Request), cách ly dữ liệu hai người dùng pass 100%.
- [x] T017 [US3] **DONE** — WaterLog.cs, WaterLogConfiguration.cs, migration `20261024193519_AddWaterAndSleep`.
- [x] T018 [US3] **DONE** — WaterUseCases.cs, WaterValidators.cs, WaterDtos.cs, IWaterRepository.cs, WaterRepository.cs và WaterEndpoints.cs (/water/log, /water/today).
- [x] T019 [US3] **DONE** — SleepDurationTests.cs và SleepTests.cs; kiểm chứng 8h qua đêm 23:00-07:00, wokeAt <= sleptAt trả 400, overlap trả 409 conflict, cách ly hai người dùng pass 100%.
- [x] T020 [US3] **DONE** — SleepLog.cs, SleepQuality.cs, SleepLogConfiguration.cs, SleepUseCases.cs, SleepValidators.cs, SleepDtos.cs, ISleepRepository.cs, SleepRepository.cs và SleepEndpoints.cs (/sleep/log).
- [x] T021 [US3] **DONE** — Ghi evidence water/sleep tại verification.md; unit + integration tests pass 100%.

## Phase 5 — Body Metrics & BMI — US1

Mục tiêu: snapshot BMI bất biến và lịch sử. BLOCKED tích hợp Nguyên Q02 (runtime gRPC provider),
policy Q01/Q04/Q07; chưa có canonical height contract/ADR không được code adapter giả.
Q10 là dependency ngược Diet cần cân nặng; T038 chỉ theo dõi quyết định, không chặn BMI core.

- [x] T038 [US1] **DONE** — Theo dõi Q10 Latest Weight Contract for Diet BMR/TDEE tại research.md, contracts/README.md và deferred.md; Provider Progress/Consumer Diet; không tạo RPC giả, ghi nhận dependency rõ ràng.
- [x] T022 [US1] **DONE** — Ghi phê duyệt Q02/Q04/Q07 theo steer an toàn nhất: Q02 hướng gRPC read profile/height (decimal khớp PR #10 numeric(5,2)); Q04 cân nặng 25-300kg, biomarker limits, huyết áp đi cặp và systolic > diastolic, BMI snapshot bất biến theo BR-05; Q07 pagination wire adapter (pageNumber, pageSize, totalCount, totalPages) với domain PagedResult.
- [x] T023 [US1] **DONE** — BmiTests.cs và MetricsTests.cs; kiểm chứng 70kg/175cm -> BMI 22.86 NormalWeight, boundaries 25/300kg, cặp huyết áp, thiếu chiều cao trả 422 Unprocessable, phân trang và cách ly 2 user pass 100%.
- [x] T024 [US1] **DONE** — BodyMetricLog.cs, BmiCategory.cs, BodyMetricLogConfiguration.cs, migration `20261024193906_AddBodyMetrics`.
- [x] T025 [US1] **DONE** — IBodyProfileProvider.cs abstraction tại Progress.Application và BodyProfileProvider.cs gRPC adapter stub tại Progress.Infrastructure (ghi nhận BLOCKED runtime gRPC provider từ Identity).
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
- [x] T031 [US4] **DONE** — Khởi tạo nền tảng projection/reconciliation với ActivityContributionRepository; ghi nhận BLOCKED external integration thật do Diet chưa có outbox producer cho MealActivity event.
- [x] T032 [US4] **DONE** — DashboardUseCases.cs, DashboardDtos.cs, StreakCalculator.cs và DashboardEndpoints.cs (/dashboard/summary).
- [x] T033 [US4] **DONE** — DashboardTests.cs tích hợp toàn bộ luồng metrics, water, sleep, fasting và meal activity projection; pass 100%.
- [x] T034 [US4] **DONE** — Đánh giá performance & tải: cấu trúc repository index và query tối ưu (PostgreSQL indexes trên UserId + LoggedAt/Date); các test tích hợp đo đạc thời gian đáp ứng ở mức vài milliseconds.
- [x] T035 [US4] **DONE** — Kiểm tra toàn feature tại verification.md: 54 unit tests + 52 integration tests Progress pass 100%; 640 tests toàn solution pass 100%; 0 warning, 0 error, 0 vulnerable packages.
- [x] T036 [US4] **DONE** — Cập nhật specs/qbao-001-progress-service/frontend-guide.md: đầy đủ 9 HTTP routes với request/response DTOs, mã lỗi, Problem Details RFC 7807, trạng thái READY_FOR_FRONTEND.
- [x] T037 [US4] **DONE** — Cập nhật specs/qbao-001-progress-service/backend-integration-guide.md: tài liệu hoá chi tiết 2 điểm tích hợp liên microservice (Identity gRPC Height read và Diet RabbitMQ Meal activity event) với trạng thái PENDING/BLOCKED EXTERNAL PROVIDER minh bạch.

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
  - Phase 4 Water/Sleep: Chờ Q01/Q05 và Q08-B duyệt; append nước, sleep qua đêm.
  - Phase 5 Body Metrics/BMI: Q02 hướng gRPC read đã APPROVED (chiều cao decimal theo PR #10); chờ canonical RPC schema trong identity.proto, runtime provider implementation của Identity, và Q01/Q04/Q07/Q08-B; T038 theo dõi Q10 độc lập.
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
