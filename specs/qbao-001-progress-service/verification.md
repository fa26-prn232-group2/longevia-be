# Báo cáo Kiểm chứng Tổng hợp: Progress Service Phases 2–6 (2026-10-25)

## 1. Trạng thái Repository & Feature Context

- **Repository**: `fa26-prn232-group2/longevia-be`
- **Owner**: Bảo (`qbao`), Dev 3
- **Feature**: `qbao-001-progress-service`
- **Branch**: `feat/progress-service`
- **Tình trạng PR**:
  - PR #11 (Phase 2 Foundation & Persistence) đã **MERGED** vào `origin/main` (commit `3c59effb312c9bc90bc6fdce001e016b6590dddf`).
  - Toàn bộ Phases 3, 4, 5 và 6 đã được triển khai và kiểm chứng trong một phiên phối hợp độc lập, tuân thủ Clean Architecture, bảo tồn foundation và persistence Phase 2, và không làm hỏng bất kỳ test suite nào của các microservices khác.

---

## 2. Tóm tắt Kiểm chứng Từng Phase

### Phase 2: Foundation & Persistence (Đã hoàn tất & merge qua PR #11)
- Clean Architecture (4 production projects + 2 test projects).
- PostgreSQL DB isolation: `progress_db`, user `progress_user`.
- Public-key ES256 authentication: nghiêm ngặt với kid, sub là GUID, fail-start khi thiếu cấu hình public key.
- Observability: Prometheus endpoint `/metrics`, scrape job UP, Grafana integration cổng 3001.

### Phase 3: Fasting Workflows (US2 — T009–T014)
- **Domain**: Entity `FastingSession`, enum `FastingProtocol` (16:8, 18:6, 20:4, Custom 1–24h), `FastingStatus` (InProgress, Completed).
- **Persistence**: Migration `20261024192855_AddFastingSessions`. PostgreSQL **Filtered Unique Index** (`"UserId"` unique where `"EndedAt" IS NULL`) bảo vệ tuyệt đối invariant chỉ 1 phiên active cho mỗi người dùng.
- **Application & API**: Endpoints `/fasting/start`, `/fasting/end`, `/fasting/current`.
- **Kiểm chứng thực tế**:
  - `FastingSessionTests.cs`: 15 unit tests pass 100% (preset protocol mismatch, Custom target bounds 1..24h, tính toán actual hours, isGoalReached).
  - `FastingTests.cs`: 5 integration tests pass 100% trên PostgreSQL thật.
    - Chu kỳ 975 phút qua đêm (16.25h) thành công, remaining không âm.
    - Concurrency test bắn 20 requests start đồng thời: chính xác **1 request thành công (201 Created)** và **19 requests bị chặn bởi DB constraint (409 Conflict)**.
    - Ownership hiding: User B không thể kết thúc session của User A -> trả **404 Not Found**.

### Phase 4: Water & Sleep Tracking (US3 — T015–T021)
- **Domain**: Entity `WaterLog` (amountMl 1..5000, date gán UTC), `SleepLog` (durationHours, sleepQuality: Poor/Fair/Good/Excellent, date gán theo wake time).
- **Persistence**: Migration `20261024193519_AddWaterAndSleep`. Indexes trên `(UserId, Date)` và `(UserId, WokeAt)`.
- **Application & API**: Endpoints `/water/log`, `/water/today`, `/sleep/log`.
- **Kiểm chứng thực tế**:
  - `SleepDurationTests.cs`: 5 unit tests pass 100% (8h qua đêm, wokeAt <= sleptAt cấm, duration > 24h cấm, quality invalid cấm).
  - `WaterTests.cs`: 3 integration tests pass 100% (350+650=1000ml, 40% target default 2500ml, bounds 1-5000ml 400 Bad Request, cách ly 2 users).
  - `SleepTests.cs`: 4 integration tests pass 100% (8h qua đêm, phát hiện overlap trả 409 Conflict, wokeAt <= sleptAt trả 400 Bad Request, cách ly 2 users).

### Phase 5: Body Metrics & BMI Snapshot (US1 — T022–T027, T038)
- **Domain**: Entity `BodyMetricLog`, `BmiCategory` (Underweight, NormalWeight, Overweight, Obese).
- **Persistence**: Migration `20261024193906_AddBodyMetrics`. Cột decimal precision `numeric(5,2)` khớp PR #10 của Identity.
- **Quyết định Q02 & BMI Snapshot**: Lưu snapshot bất biến `HeightUsedCm`, `Bmi`, `BmiCategory` lúc ghi; không recalculate khi xem lịch sử (BR-05).
- **Application & API**: Endpoints `/metrics`, `/metrics/history`. Abstraction `IBodyProfileProvider` lấy chiều cao từ Identity.
- **Kiểm chứng thực tế**:
  - `BmiTests.cs`: 9 unit tests pass 100% (công thức BMI, ngưỡng phân loại, weight bounds 25..300kg, huyết áp đi cặp và systolic > diastolic, snapshot bất biến).
  - `MetricsTests.cs`: 3 integration tests pass 100% (70kg/175cm -> BMI 22.86 NormalWeight, thiếu chiều cao trả 422 Unprocessable, phân trang và cách ly 2 users).

### Phase 6: Dashboard Aggregation & Streaks (US4 — T028–T037)
- **Domain & Persistence**: Entity `ActivityContribution`, migration `20261024194214_AddActivityContributions` với unique constraint `(Source, SourceLogId)` cho idempotent projection.
- **Thuật toán Streak & Weight Delta**:
  - Active day = Hợp ngày có MealLog và ngày có BodyMetricLog. Nước, ngủ và fasting không tạo streak độc lập.
  - Weight delta so với tuần trước (bản ghi gần nhất tại/trước `now - 7 days`). Thiếu bản ghi tuần trước trả `null`.
  - Current streak kéo dài đến hôm nay (nếu active) hoặc neo ở hôm qua; nếu gap >= 2 ngày trả 0. Best streak là chuỗi dài nhất trong lịch sử.
- **Application & API**: Endpoint `/dashboard/summary` tổng hợp đầy đủ 8 trường canonical.
- **Kiểm chứng thực tế**:
  - `StreakTests.cs`: 4 unit tests pass 100% (chuỗi 1,2,3,5 -> best=3, current=1; anchor hôm qua -> current=3; gap 2 ngày -> current=0).
  - `DashboardTests.cs`: Tích hợp toàn diện: initial 72kg (14 ngày trước), baseline 70kg (8 ngày trước), current 68.5kg (hôm nay) -> `weightDeltaKg = -1.5kg`; bữa ăn hôm qua + metric hôm nay -> `currentStreakDays = 2`; fasting 1 phiên; giấc ngủ trung bình 8.0h.

---

## 3. Tổng hợp Bằng chứng Kiểm thử Toàn Solution (Gate Evidence)

### 3.1 Progress Service Test Suites
- **Unit Tests (`Progress.UnitTests`)**: **54 / 54 tests PASSED** (0 failed, 0 skipped, Duration: 358 ms).
- **Integration Tests (`Progress.IntegrationTests`)**: **52 / 52 tests PASSED** (0 failed, 0 skipped, chạy thật trên Testcontainers PostgreSQL, Duration: 15 s).
  - `AuthTests`: 16 tests
  - `HostTests`: 13 tests
  - `FastingTests`: 5 tests
  - `WaterTests`: 3 tests
  - `SleepTests`: 4 tests
  - `MetricsTests`: 3 tests
  - `DashboardTests`: 1 test
  - Foundation fixtures & probes: 7 tests

### 3.2 Toàn bộ Solution (`dotnet test longevia-be.slnx -c Release -m:1`)
- **Tổng số tests passed**: **640 / 640 tests PASSED** (0 failed, 0 skipped):
  - `Diet.IntegrationTests`: 16 tests passed
  - `Diet.UnitTests`: 11 tests passed
  - `Identity.IntegrationTests`: 9 tests passed
  - `Identity.UnitTests`: 67 tests passed
  - `Progress.IntegrationTests`: 52 tests passed
  - `Progress.UnitTests`: 54 tests passed
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
| Lấy chiều cao người dùng (Q02) | Identity Service | gRPC Read đồng bộ | **BLOCKED EXTERNAL PROVIDER** (Chưa có RPC trong `identity.proto` trên main) | Đã tạo abstraction `IBodyProfileProvider`, stub adapter gRPC. Trả 422 khi thiếu chiều cao. |
| Sự kiện bữa ăn (Q06 / Q09) | Diet Service | RabbitMQ Event bất đồng bộ | **BLOCKED EXTERNAL PROVIDER** (Diet chưa có outbox producer trên main) | Đã tạo entity `ActivityContribution` (idempotent unique key) và tích hợp vào thuật toán tính Streak. |
| Cung cấp cân nặng cho Diet (Q10 / T038) | Diet Service | gRPC Read đồng bộ | **PENDING APPROVAL** (Phụ thuộc ngược từ Diet) | Đã chuẩn bị phương thức `GetLatestAsync` trong `IBodyMetricRepository`. |

---

## 5. Kết luận & Hướng bàn giao

- Toàn bộ các yêu cầu nghiệp vụ của US1 (Metrics/BMI), US2 (Fasting), US3 (Water/Sleep), US4 (Dashboard/Streak) đã được hiện thực hóa và kiểm chứng độc lập hoàn chỉnh.
- Mở **Draft Pull Request** mới nhắm tới `main` để đội ngũ review, minh bạch ghi nhận trạng thái blocked của 2 external providers.
