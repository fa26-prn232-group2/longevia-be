# Corrective PR #13 — reconciliation thật, broker và Q01 UTC (2026-10-25)

## HEAD, phạm vi và môi trường

- Nhánh feat/progress-service, feature qbao-001-progress-service, owner qbao;
  working tree đầu lượt sạch. Resolve/team check: 6 feature consistent.
- Previous PR #13 HEAD: 29522303c279d6b6bff496fc79ea6100d703574e, OPEN/Draft.
- Diet PR #12 thực fetch: bc79173d6681ce9964b4badad6b16f5ac1d87472, OPEN/chưa merge.
  Không merge/reset/force-push hoặc nhập source Diet vào production Progress.
- Môi trường local: Windows/PowerShell 7, .NET SDK 10.0.203, Docker Engine 29.9.2,
  PostgreSQL 17-alpine và RabbitMQ 4-management-alpine Testcontainers thật.
- Kết quả dưới đây gắn với code tree của commit chứa báo cáo này. SHA xuất bản và
  CI đúng final HEAD được cập nhật trong PR #13/bàn giao sau push; không dùng CI cũ.

## Gates và số đếm hiện hành

Make không cài trên máy; dùng đúng dotnet recipes trong Makefile, thêm -m:1 giới
hạn MSBuild, không filter/bỏ test ở full gate.

| Kiểm tra | Kết quả |
| --- | --- |
| Full solution format verify | PASS sau chuẩn hóa LF checkout; không có diff source ngoài Progress. |
| Debug/Release build warnings-as-errors | PASS, 0 warning / 0 error. |
| Scoped Progress unit | 75 pass / 0 fail / 0 skip. |
| Scoped Progress integration | 103 pass / 0 fail / 0 skip trên DB/broker thật; assertion retry header được chạy lại ở full gate cuối. |
| Full solution Release | 733 pass / 0 fail / 0 skip, đủ 9 test assemblies (Progress 178).  |
| Vulnerability scan | 27 projects: không có vulnerable packages, kể cả transitive. |
| Compose config --quiet / git diff --check / team check | PASS; chỉ Progress block đổi explicit broker settings/dependency. |
| CI final HEAD | Chờ xuất bản và kiểm tra đúng SHA; không nhận local làm CI. |

Một lần Release gate gặp hết ổ E do Worker Web SDK thu test bin/obj lồng nhau sau
nhiều lượt build. Đã dọn đúng generated outputs bin/obj Worker/test, restore lại;
không sửa Worker source, không thay recipe, không skip test. Một assertion ban đầu
đếm consume observer thay cho retry đã đỏ: observer là một lifecycle. Đã chuyển
sang kiểm tra MessageHeaders.FaultRetryCount=3 trong message thực ở _error.
Không coi các lượt đỏ là PASS.

## Reconciliation — 20 process integration cases

[CLI](../../src/Services/Progress/Progress.Infrastructure/Reconciliation/MealSnapshotReconciler.cs)
được gọi bởi launcher PowerShell cũ; chạy trước web host, không JWT/broker/migration.
SHA-256 nguyên compact entry UTF-8 + LF, không reserialize. Kiểm tra version, UUID,
UTC tường minh, DateOnly ISO, unique MealLogId, duplicate JSON property, count và hash
uppercase trước DB. Không default credentials; từ chối DB khác progress_db.

Transaction RepeatableRead READ ONLY; typed Npgsql params, batch500 ID. Contribution
phải đúng một Diet.Meal/SourceLogId; inbox phải đúng một EventId=MealLogId và đúng
MealLogId/UserId/BusinessDate. Query theo cả event và meal ID bắt association sai.
Empty snapshot vẫn query schema bảng trong DB đã migrate. Không ghi/repair dữ liệu.
Extra live records ngoài snapshot NOT EVALUATED, không buộc total DB=count snapshot.

[SnapshotReconciliationTests](../../src/Services/Progress/tests/Progress.IntegrationTests/Messaging/SnapshotReconciliationTests.cs)
spawn process dotnet thật, assert exit code + PASS/FAIL: empty snapshot/empty DB,
matching, missing contribution, missing inbox, wrong user/date/meal association,
duplicate ID, tampered checksum, wrong count, truncated JSON, unsupported version,
DB unavailable, extra live meals, 1.003 entries/3 batches, backdated event, empty UUID,
non-UTC timestamp, invalid date và wrong database. Exit 0/2/3/4 theo từng tình huống.

## Broker thật — 9 test cases và audit persistence

[RabbitMealTests](../../src/Services/Progress/tests/Progress.IntegrationTests/Messaging/RabbitMealTests.cs)
dùng actual MassTransit RabbitMQ transport, MealLoggedConsumer, EF repositories và
5 migration hiện hữu, không in-memory. Poll có timeout 45s (DB outage 60s), không
lấy sleep làm tiêu chí thành công.

- Canonical event: actual topic longevia.events, binding diet.meal.logged, queue
  progress.meal-activity durable; kiểm tra management API và receive context key/exchange.
- Wrong key không ghi; sentinel đúng key kiểm chứng publisher đã chạy.
- 12 duplicate concurrent + consumer restart giữ một inbox/contribution.
- Backdated BusinessDate, hai meal một ngày và user isolation được assert trong DB.
- Kill process Progress host khi broker còn unacked/DB pause: broker redelivery flag
  thực, restart consumer ghi đúng một hiệu ứng. Không giả redelivery bằng gọi service.
- DB pause làm persistence thất bại, retry hữu hạn, không success callback hoặc
  inbox/contribution; _error chứa message với FaultRetryCount=3; DB resume nhận lại được.
- Poison và conflicting duplicate cũng vào MassTransit _error sau 3 retry; không
  overwrite projection. Broker DLQ là cơ chế khác, BindDeadLetterQueue không chứng
  minh consumer fault đi vào .dlq.
- Broker stop_app/start_app giữ durable queued event và consumer nhận sau resume.

[MealIntegrityTests](../../src/Services/Progress/tests/Progress.IntegrationTests/Messaging/MealIntegrityTests.cs)
bổ sung 5 case: sửa atomic partial pair từ replay hợp lệ (hai chiều), reject canonical
IDs sai, reject inbox association sai, actual PostgreSQL PK uniqueness lỗi không liên
quan phải throw và rollback inbox. Không match exception message; dùng SQLSTATE và
constraint names rồi re-read known dedup conflict. ProcessedAt dùng TimeProvider.

## Actual Diet producer interoperability — tách khỏi 733 full-solution tests

Harness [test-diet-interoperability.ps1](../../scripts/progress/test-diet-interoperability.ps1)
archive PR #12 SHA ở trên vào môi trường tạm; chỉ thêm harness test trong đó.
Actual Diet API/MealActivityPublisher/IPublishEndpoint + EF bus outbox → RabbitMQ →
actual Progress consumer/PostgreSQL: **1 integration test PASS**. Assert receive
exchange=longevia.events và routing key=diet.meal.logged. Cấu hình Send routing key
formatter hiện tại của PR #12 thực tế tương thích publish/outbox trong lần chạy này;
không có failing interoperability case cần Huy sửa producer, không sửa Diet source.

Harness tạo 101 historical meals + một meal HTTP, actual replay=102, đợi 102 inbox,
actual Diet exporter JSON v1 rồi Progress reconciliation matched=102/PASS. Đây là
production-producer interoperability + replay/reconciliation fixture, không phải
Gateway/Identity/Frontend E2E hoặc bằng chứng dữ liệu triển khai đã bootstrap đủ.
CapturedAtUtc không làm cursor, reconciliation không chứng minh broker/outbox drain.
Cách tái lập và preconditions: [corrective-reconciliation](corrective-reconciliation.md).

## Q01 và UTC boundary evidence

Q01 **APPROVED FOR PROGRESS MVP BY BẢO**, Huy acknowledgment **PENDING**.
[UtcPolicyTests](../../src/Services/Progress/tests/Progress.IntegrationTests/UtcPolicyTests.cs)
có 3 deterministic scenarios, bao phủ 12 assertion nhóm theo yêu cầu: water trước
2026-10-24T23:59:59Z và sau 2026-10-25T00:00:00Z; today không leak ngày cũ; sleep
wake date/duration qua đêm; metric UTC activity/history inclusive-exclusive; dashboard
current/best union meal+metric không double-count; Monday UTC fasting start inclusive,
next Monday exclusive; replay khác OccurredOn giữ BusinessDate; Việt Nam 00:30 vẫn
ngày UTC trước. Fixed/injected TimeProvider, không dựa timezone máy. Các regressions
Fasting/Sleep/BMI, concurrency và auth cũ được giữ trong full suite.

## Negative controls đã đỏ đúng lỗi và khôi phục

1. Bỏ guard EventId==MealLogId: test canonical IDs FAIL vì không throw.
2. Ép reconciliation pass=true: missing contribution FAIL vì process exit0/PASS,
   matched0 thay vì exit2. Chứng minh regression false-PASS bị bắt.
3. Blind AlreadyProcessed khi inbox tồn tại: partial inbox-only FAIL contribution
   count0 thay vì1 (case contribution-only vẫn pass). Đã restore implementation.

Log local corrective-negative-identity/reconcile/partial, corrective-unit/integration,
corrective-full-tests và corrective-diet-interop; log không commit secret/user data.
CI mới phải chạy broker suite thật, không skip vì Docker hoặc đổi sang in-memory.

## Migration và readiness còn chặn

Không có migration mới, không sửa 5 applied migration hoặc lịch sử BMI/height/date.
Không đổi public Dashboard DTO, shared contracts, Identity/Diet/Worker implementation.
Q02 Height runtime/provider vẫn chờ Nguyên, Q10 Weight vẫn proposal/scaffolding như cũ.
Diet PR #12 chưa merge; rollout writer/queue/replay/snapshot dữ liệu triển khai và
completeness mechanism chưa được nghiệm thu. Không claim historical streak đầy đủ.
Huy chưa acknowledge current-day UTC frontend/Diet; [tin nhắn bàn giao](handoffs/huy-q01-utc.md)
được soạn cho Bảo tự gửi, không gửi thay hoặc fabricate approval.
PR #13 tiếp tục Draft; không merge #12/#13, không push main/force-push.

---

## Báo cáo trước corrective này — giữ như lịch sử, không thay evidence hiện hành
# Báo cáo Kiểm chứng Tổng hợp: Progress Service Phases 2–6 (Corrective PR #13)

## 1. Trạng thái Repository & Feature Context

- **Repository**: `fa26-prn232-group2/longevia-be`
- **Owner**: Bảo (`qbao`), Dev 3
- **Feature**: `qbao-001-progress-service`
- **Branch**: `feat/progress-service`
- **Tình trạng PR**:
  - PR #11 (Phase 2 Foundation & Persistence) đã **MERGED** vào `origin/main` (commit `3c59effb312c9bc90bc6fdce001e016b6590dddf`).
  - PR #13 (Phases 3-6 Business Capabilities) đang mở ở trạng thái **DRAFT**: `https://github.com/fa26-prn232-group2/longevia-be/pull/13`.
  - Phiên hiệu chỉnh (Corrective PR #13) đã khắc phục toàn diện các phát hiện đánh giá độc lập:
    1. Fasting atomic end transition và retry semantics qua PostgreSQL advisory lock.
    2. Sleep concurrency-safe overlap qua PostgreSQL transaction-scoped advisory lock.
    3. BMI classification precision sử dụng unrounded raw BMI.
    4. Phân loại lỗi chính xác cho Identity Height Provider (503 Service Unavailable vs 422 Unprocessable vs 404 Not Found), trung thực ghi nhận blocker.
    5. Dashboard streak trung thực ghi nhận blocker từ Diet PR #12.

---

## 2. Tóm tắt Kiểm chứng Từng Phase & Correctives

### Phase 2: Foundation & Persistence (Đã hoàn tất & merge qua PR #11)
- Clean Architecture (4 production projects + 2 test projects).
- PostgreSQL DB isolation: `progress_db`, user `progress_user`.
- Public-key ES256 authentication: nghiêm ngặt với kid, sub là GUID, fail-start khi thiếu cấu hình public key.
- Observability: Prometheus endpoint `/metrics`, scrape job UP, Grafana integration cổng 3001.

### Phase 3: Fasting Workflows (US2 — T009–T014)
- **Domain**: Entity `FastingSession`, enum `FastingProtocol` (16:8, 18:6, 20:4, Custom 1–24h), `FastingStatus` (InProgress, Completed).
- **Persistence**: Migration `20261024192855_AddFastingSessions`. PostgreSQL **Filtered Unique Index** (`"UserId"` unique where `"EndedAt" IS NULL`) bảo vệ tuyệt đối invariant chỉ 1 phiên active cho mỗi người dùng.
- **Application & API**: Endpoints `/fasting/start`, `/fasting/end`, `/fasting/current`.
- **Atomic End & Concurrency Implementation**:
  - `IFastingRepository.EndSessionWithLockAsync`: Sử dụng PostgreSQL `pg_advisory_xact_lock(hashtext(sessionId))` trong transaction để tuần tự hóa mọi request end đồng thời.
  - Replay retry cùng payload `endedAt` (chênh lệch <= 1s) trả về 200 OK.
  - Retry khác payload `endedAt` (> 1s) trả về 409 Conflict (`progress.fasting_end_conflict`).
- **Kiểm chứng thực tế (Testcontainers PostgreSQL)**:
  - `FastingSessionTests.cs`: 15 unit tests pass 100%.
  - `FastingTests.cs`: 9 integration tests pass 100%:
    - Chu kỳ 975 phút qua đêm (16.25h) thành công, remaining không âm.
    - 20 concurrent start: chính xác 1 request 201 Created, 19 requests 409 Conflict.
    - Ownership hiding: User B không thể kết thúc session của User A -> trả 404 Not Found.
    - 2 concurrent end request với timestamp khác nhau: chính xác 1 request 200 OK, 1 request 409 Conflict.
    - 2 concurrent end request cùng timestamp: cả 2 đều thành công 200 OK (idempotent replay).
    - Retry cùng timestamp sau khi completed: 200 OK replay.
    - Retry khác timestamp sau khi completed: 409 Conflict.

### Phase 4: Water & Sleep Tracking (US3 — T015–T021)
- **Domain**: Entity `WaterLog` (amountMl 1..5000, date gán UTC), `SleepLog` (durationHours, sleepQuality: Poor/Fair/Good/Excellent, date gán theo wake time).
- **Persistence**: Migration `20261024193519_AddWaterAndSleep`. Indexes trên `(UserId, Date)` và `(UserId, WokeAt)`.
- **Concurrency-Safe Overlap Implementation**:
  - `ISleepRepository.LogWithConcurrencyCheckAsync`: Sử dụng PostgreSQL Transaction kết hợp Transaction-scoped Advisory Lock `pg_advisory_xact_lock(hashtext(userId))` để tuần tự hóa ghi nhận sleep của cùng một user mà không khóa bảng hay ảnh hưởng user khác.
  - Kiểm tra overlap `x.SleptAt < wokeAt && x.WokeAt > sleptAt` trong cùng transaction đang giữ lock.
  - Cho phép touching intervals (ví dụ: 22:00-06:00 và 06:00-07:00).
- **Kiểm chứng thực tế (Testcontainers PostgreSQL)**:
  - `SleepDurationTests.cs`: 5 unit tests pass 100%.
  - `WaterTests.cs`: 3 integration tests pass 100% (350+650=1000ml, 40% target default 2500ml, bounds 1-5000ml 400 Bad Request, cách ly 2 users).
  - `SleepTests.cs`: 5 integration tests pass 100%:
    - 8h qua đêm 23:00-07:00.
    - Overlap phát hiện trả 409 Conflict.
    - WokeAt <= sleptAt trả 400 Bad Request.
    - Cách ly giữa 2 users độc lập không cross-blocking.
    - **Concurrency Stress Test**: 5 requests đồng thời tạo sleep overlap cho cùng 1 user -> **chính xác 1 request 201 Created và 4 requests 409 Conflict**.

### Phase 5: Body Metrics & BMI Snapshot (US1 — T022–T027, T038)
- **Domain**: Entity `BodyMetricLog`, `BmiCategory` (Underweight < 18.5, NormalWeight < 25.0, Overweight < 30.0, Obese >= 30.0).
- **Persistence**: Migration `20261024193906_AddBodyMetrics`. Cột decimal precision `numeric(5,2)` khớp PR #10 của Identity.
- **BMI Precision Fix**:
  - Phân loại `BmiCategory` dựa trên giá trị `rawBmi` chưa làm tròn (`weightKg / (heightM * heightM)`).
  - Giá trị hiển thị/lưu trữ `Bmi` được làm tròn 2 chữ số thập phân (`Math.Round(rawBmi, 2)`).
  - Snapshot bất biến: `HeightUsedCm`, `Bmi`, `BmiCategory` lưu cố định tại thời điểm ghi nhận, không đổi khi user cập nhật profile.
- **Identity Height Provider Error Categorization**:
  - `ProfileHeightResult` với trạng thái rõ ràng:
    - `ServiceUnavailable`: Identity service chưa sẵn sàng -> trả **503 Service Unavailable** (`progress.identity_unavailable`).
    - `ProfileNotFound`: User profile chưa tồn tại -> trả **404 Not Found** (`progress.profile_not_found`).
    - `HeightNotRecorded`: User chưa khai báo chiều cao -> trả **422 Unprocessable Entity** (`progress.height_unavailable`).
    - `Success`: Có chiều cao hợp lệ -> trả **201 Created**.
- **Kiểm chứng thực tế**:
  - `BmiTests.cs`: 19 unit tests pass 100%:
    - Kiểm chứng boundary rounding threshold: cân nặng 99.984 kg, chiều cao 200.0 cm -> `rawBmi = 24.996` (< 25.0 -> `NormalWeight`), hiển thị `Bmi = 25.00`.
    - Kiểm chứng toàn diện các ngưỡng 18.5, 25.0, 30.0 với các giá trị: just below, exactly on threshold, just above.
    - Snapshot immutability.
  - `MetricsTests.cs`: 5 integration tests pass 100%:
    - 70kg/175cm -> BMI 22.86 NormalWeight (201 Created).
    - Thiếu chiều cao -> 422 UnprocessableEntity.
    - Profile not found -> 404 NotFound.
    - Identity service unavailable -> 503 ServiceUnavailable.
    - Phân trang history và cách ly 2 users.

### Phase 6: Dashboard Aggregation, Streaks & Diet Messaging Integration (US4 — T028–T037)
- **Domain & Persistence**:
  - Entity `ActivityContribution`, migration `20261024194214_AddActivityContributions` với unique constraint `(Source, SourceLogId)` cho idempotent projection.
  - Entity `ProcessedMealEvent`, migration `20261024214957_AddProcessedMealEvents` làm Durable Inbox lưu vết các event bữa ăn đã nhận và xử lý.
- **Thuật toán Streak & Weight Delta**:
  - Active day = Hợp ngày có MealLog và ngày có BodyMetricLog. Nước, ngủ và fasting không tạo streak độc lập.
  - Weight delta so với tuần trước (bản ghi gần nhất tại/trước `now - 7 days`). Thiếu bản ghi tuần trước trả `null`.
  - Current streak kéo dài đến hôm nay (nếu active) hoặc neo ở hôm qua; nếu gap >= 2 ngày trả 0. Best streak là chuỗi dài nhất trong lịch sử.
- **Tích hợp Diet Meal Activity Event (Workstream A)**:
  - Event contract: `Longevia.Shared.Events.MealLoggedEvent` (EventId, UserId, MealLogId, BusinessDate, CreatedAt, OccurredOn).
  - Durable Inbox & Concurrency: `MealActivityConsumerService` sử dụng transaction-scoped advisory lock `pg_advisory_xact_lock(hashtext(mealLogId))` để tuần tự hóa, idempotent dedup nếu event lặp lại, và **phát hiện/từ chối conflicting duplicate** (ném exception vào DLQ nếu cùng MealLogId nhưng khác UserId hoặc Date).
  - MassTransit Consumer: `MealLoggedConsumer` lắng nghe topic exchange `longevia.events` với routing key `diet.meal.logged`, durable queue `progress.meal-activity`, exponential retry (2s/5s/10s), DLQ `progress.meal-activity.dlq`.
  - Reconciliation Script: `scripts/progress/reconcile-meal-activity-snapshot.ps1` kiểm tra tính toàn vẹn SHA-256 từng dòng snapshot v1 và đối chiếu DB.
- **Tích hợp Latest Weight gRPC Provider (Workstream B — Q10 / T038)**:
  - Canonical Contract: `contracts/progress/v1/progress.proto` định nghĩa `GetLatestWeight` RPC với `weight_kg_x100: int32` chống sai số floating-point, as-of UTC timestamp, `oneof result { WeightRecord record; bool no_record_found; }`.
  - Proposal Documentation: `contracts/progress/v1/README.md` với 7 câu hỏi kiến trúc cho Bảo + Huy duyệt.
  - Query Scaffolding: `IWeightQueryService` và `WeightQueryService` trong `Progress.Application` xử lý truy vấn as-of và làm tròn `Math.Round(metric.WeightKg * 100m)`.
- **Kiểm chứng thực tế (Testcontainers PostgreSQL)**:
  - `StreakTests.cs`: 4 unit tests pass 100%.
  - `WeightQueryServiceTests.cs`: 5 unit tests pass 100% (chính xác as-of record, bỏ qua record sau as-of, thứ tự ưu tiên Id khi cùng timestamp, chuyển đổi weight_kg_x100, trả null khi không có record).
  - `MealActivityConsumerTests.cs`: 6 integration tests pass 100%:
    - Tạo mới projection và inbox record từ event bữa ăn hợp lệ.
    - Idempotent deduplication khi nhận lại cùng event (không tạo bản ghi lặp, trả AlreadyProcessed).
    - Concurrent duplicate: 5 requests đồng thời cho cùng một event -> chính xác 1 tạo mới, 4 deduplicate an toàn.
    - Conflicting duplicate: Cùng MealLogId nhưng khác UserId -> từ chối và ném exception `InvalidOperationException` để vào DLQ.
    - Conflicting duplicate: Cùng MealLogId nhưng khác BusinessDate -> từ chối và ném exception `InvalidOperationException` để vào DLQ.
    - Multi-event aggregation: 3 events khác nhau cho cùng một user trong nhiều ngày tạo đủ 3 projections và 3 inbox records.
  - `WeightProviderIntegrationTests.cs`: 1 integration test pass 100% kiểm chứng truy vấn as-of weight thực tế trên PostgreSQL.
  - `DashboardTests.cs`: Tích hợp toàn diện: initial 72kg (14 ngày trước), baseline 70kg (8 ngày trước), current 68.5kg (hôm nay) -> `weightDeltaKg = -1.5kg`; bữa ăn hôm qua + metric hôm nay -> `currentStreakDays = 2`; fasting 1 phiên; giấc ngủ trung bình 8.0h.

---

## 3. Tổng hợp Bằng chứng Kiểm thử Toàn Solution (Gate Evidence)

### 3.1 Progress Service Test Suites
- **Unit Tests (`Progress.UnitTests`)**: **69 / 69 tests PASSED** (0 failed, 0 skipped, Duration: 90 ms).
- **Integration Tests (`Progress.IntegrationTests`)**: **66 / 66 tests PASSED** (0 failed, 0 skipped, chạy thật trên Testcontainers PostgreSQL, Duration: 16 s).
  - `AuthTests`: 16 tests
  - `HostTests`: 13 tests
  - `FastingTests`: 9 tests (atomic end, concurrency conflict, idempotent replay)
  - `WaterTests`: 3 tests
  - `SleepTests`: 5 tests (concurrent stress test 5 requests đồng thời)
  - `MetricsTests`: 5 tests (tests 404, 503, 422, 201 cho Identity height provider)
  - `MealActivityConsumerTests`: 6 tests (durable inbox, idempotency, concurrent dedup, conflicting duplicate rejection, multi-day projection)
  - `WeightProviderIntegrationTests`: 1 test (as-of weight query)
  - `DashboardTests`: 1 test
  - Foundation fixtures & probes: 7 tests
- **Tổng Progress Tests**: **135 / 135 tests PASSED (100%)**.

### 3.2 Toàn bộ Solution (`dotnet test longevia-be.slnx -c Release -m:1`)
- **Tổng số tests passed**: **690 / 690 tests PASSED** (0 failed, 0 skipped):
  - `Diet.IntegrationTests`: 16 tests passed
  - `Diet.UnitTests`: 11 tests passed
  - `Identity.IntegrationTests`: 9 tests passed
  - `Identity.UnitTests`: 67 tests passed
  - `Progress.IntegrationTests`: 66 tests passed
  - `Progress.UnitTests`: 69 tests passed
  - `Recommendation.IntegrationTests`: 64 tests passed
  - `Recommendation.UnitTests`: 372 tests passed
  - `Worker.UnitTests`: 16 tests passed

### 3.3 Static Analysis & Security Scan
- **Format check**: `dotnet format --verify-no-changes` sạch sẽ trên toàn bộ các project của Progress Service.
- **Build Release**: `dotnet build src/Services/Progress/Progress.Api/Progress.Api.csproj -c Release -warnaserror` thành công với **0 Warning, 0 Error**.
- **Vulnerability scan**: `dotnet list longevia-be.slnx package --vulnerable --include-transitive` xác nhận **27 projects sạch sẽ, 0 lỗ hổng bảo mật**.

---

## 4. Tình trạng Tích hợp Liên Microservice (External Integration Status)

| Điểm tích hợp | Phía đối ứng | Hình thức | Trạng thái hiện tại | Xử lý tại Progress |
| --- | --- | --- | --- | --- |
| Lấy chiều cao người dùng (Q02) | Identity Service (Nguyên) | gRPC Read đồng bộ | **BLOCKED EXTERNAL PROVIDER** (Chưa có RPC trong `identity.proto` trên main) | Đã tạo abstraction `IBodyProfileProvider`, stub adapter gRPC trả `ServiceUnavailable` (503). Không tự bịa RPC. |
| Sự kiện bữa ăn (Q06 / Q09) | Diet Service (Huy) | RabbitMQ Event bất đồng bộ | **PROGRESS IMPLEMENTED (READY FOR DIET PR #12 MERGE)** | Đã triển khai hoàn tất MassTransit `MealLoggedConsumer`, durable inbox `ProcessedMealEvent` với advisory lock, conflicting duplicate detection, và reconciliation script. Kiểm chứng 6 tests pass 100%. |
| Cung cấp cân nặng cho Diet (Q10 / T038) | Diet Service (Huy) | gRPC Read đồng bộ | **CONTRACT PROPOSED & SCAFFOLDING IMPLEMENTED (AWAITING APPROVAL)** | Đã phát hành canonical protobuf `contracts/progress/v1/progress.proto`, tài liệu 7 câu hỏi proposal `contracts/progress/v1/README.md`, và query scaffolding `WeightQueryService` với 6 tests pass 100%. |

---

## 5. Kết luận & Trạng thái PR #13

- Toàn bộ các yêu cầu nghiệp vụ nội bộ của US1 (Metrics/BMI), US2 (Fasting), US3 (Water/Sleep), US4 (Dashboard/Streak) và hạ tầng tích hợp Progress-owned (Meal Activity Consumer, Durable Inbox, Weight Query Provider Scaffolding) đã được hiện thực hóa và kiểm chứng độc lập hoàn chỉnh với 135 tests xanh 100%.
- Các vấn đề concurrency và idempotency (Fasting atomic end, Sleep overlap locking, BMI raw precision, Meal event advisory locking & conflicting payload rejection) đã được khắc phục triệt để và chứng minh bằng integration tests trên PostgreSQL thật.
- **PR #13 TIẾP TỤC GIỮ Ở DRAFT PR**: Không chuyển sang Ready for Review hoặc Auto-merge cho đến khi:
  1. Nguyên (Identity) cung cấp canonical height-read RPC trong `contracts/identity/v1/identity.proto`.
  2. Huy (Diet) merge PR #12 cung cấp canonical MealLoggedEvent outbox publisher vào `origin/main`.
  3. Bảo và Huy thống nhất phê duyệt 7 câu hỏi kiến trúc cho hợp đồng gRPC `contracts/progress/v1/progress.proto`.
