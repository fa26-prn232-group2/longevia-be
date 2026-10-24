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

### Phase 6: Dashboard Aggregation & Streaks (US4 — T028–T037)
- **Domain & Persistence**: Entity `ActivityContribution`, migration `20261024194214_AddActivityContributions` với unique constraint `(Source, SourceLogId)` cho idempotent projection.
- **Thuật toán Streak & Weight Delta**:
  - Active day = Hợp ngày có MealLog và ngày có BodyMetricLog. Nước, ngủ và fasting không tạo streak độc lập.
  - Weight delta so với tuần trước (bản ghi gần nhất tại/trước `now - 7 days`). Thiếu bản ghi tuần trước trả `null`.
  - Current streak kéo dài đến hôm nay (nếu active) hoặc neo ở hôm qua; nếu gap >= 2 ngày trả 0. Best streak là chuỗi dài nhất trong lịch sử.
- **Tích hợp Diet Meal Activity**:
  - Schema và repository `ActivityContribution` đã sẵn sàng cho idempotent projection.
  - Tuy nhiên, PR #12 của Diet (`feat/diet-service`) vẫn đang là OPEN trên main; do đó Progress không tạo speculative consumer hay bịa đặt event schema, mà ghi nhận trung thực **BLOCKED / PENDING PR #12**.
- **Kiểm chứng thực tế**:
  - `StreakTests.cs`: 4 unit tests pass 100%.
  - `DashboardTests.cs`: Tích hợp toàn diện: initial 72kg (14 ngày trước), baseline 70kg (8 ngày trước), current 68.5kg (hôm nay) -> `weightDeltaKg = -1.5kg`; bữa ăn hôm qua + metric hôm nay -> `currentStreakDays = 2`; fasting 1 phiên; giấc ngủ trung bình 8.0h.

---

## 3. Tổng hợp Bằng chứng Kiểm thử Toàn Solution (Gate Evidence)

### 3.1 Progress Service Test Suites
- **Unit Tests (`Progress.UnitTests`)**: **64 / 64 tests PASSED** (0 failed, 0 skipped, Duration: 106 ms).
- **Integration Tests (`Progress.IntegrationTests`)**: **59 / 59 tests PASSED** (0 failed, 0 skipped, chạy thật trên Testcontainers PostgreSQL, Duration: 16 s).
  - `AuthTests`: 16 tests
  - `HostTests`: 13 tests
  - `FastingTests`: 9 tests (bổ sung 4 tests cho atomic end, concurrency conflict, idempotent replay)
  - `WaterTests`: 3 tests
  - `SleepTests`: 5 tests (bổ sung concurrent stress test 5 requests đồng thời)
  - `MetricsTests`: 5 tests (bổ sung tests 404, 503, 422, 201 cho Identity height provider)
  - `DashboardTests`: 1 test
  - Foundation fixtures & probes: 7 tests
- **Tổng Progress Tests**: **123 / 123 tests PASSED**.

### 3.2 Toàn bộ Solution (`dotnet test longevia-be.slnx -c Release -m:1`)
- **Tổng số tests passed**: **678 / 678 tests PASSED** (0 failed, 0 skipped):
  - `Diet.IntegrationTests`: 16 tests passed
  - `Diet.UnitTests`: 11 tests passed
  - `Identity.IntegrationTests`: 9 tests passed
  - `Identity.UnitTests`: 67 tests passed
  - `Progress.IntegrationTests`: 59 tests passed
  - `Progress.UnitTests`: 64 tests passed
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
| Sự kiện bữa ăn (Q06 / Q09) | Diet Service (Huy) | RabbitMQ Event bất đồng bộ | **BLOCKED EXTERNAL PROVIDER** (PR #12 của Diet chưa merge vào main) | Đã tạo entity và repository `ActivityContribution` (idempotent unique key) cho projection. Không tạo speculative consumer. |
| Cung cấp cân nặng cho Diet (Q10 / T038) | Diet Service (Huy) | gRPC Read đồng bộ | **PENDING APPROVAL** (Phụ thuộc ngược từ Diet) | Đã chuẩn bị phương thức `GetLatestAsync` trong `IBodyMetricRepository`. |

---

## 5. Kết luận & Trạng thái PR #13

- Toàn bộ các yêu cầu nghiệp vụ nội bộ của US1 (Metrics/BMI), US2 (Fasting), US3 (Water/Sleep), US4 (Dashboard/Streak) đã được hiện thực hóa và kiểm chứng độc lập hoàn chỉnh với 123 tests xanh 100%.
- Các vấn đề concurrency (Fasting atomic end, Sleep overlap locking, BMI raw precision) đã được khắc phục triệt để và chứng minh bằng integration tests trên PostgreSQL thật.
- **PR #13 TIẾP TỤC GIỮ Ở DRAFT PR**: Không chuyển sang Ready for Review hoặc Auto-merge cho đến khi:
  1. Nguyên (Identity) cung cấp canonical height-read RPC trong `contracts/identity/v1/identity.proto`.
  2. Huy (Diet) merge PR #12 cung cấp canonical MealLoggedEvent contract.
  Hoặc đội ngũ thống nhất cắt deliverable độc lập cho các tính năng standalone (Fasting, Water, Sleep, Dashboard query).
