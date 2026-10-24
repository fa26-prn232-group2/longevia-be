# Hướng dẫn Tích hợp Backend (Backend Integration Guide)

Trạng thái: **PENDING_EXTERNAL_CONTRACTS** (Phần core Progress đã hoàn tất và kiểm chứng độc lập 100%; chờ canonical integration provider từ Identity và Diet).

Tài liệu này xác định giao diện tích hợp giữa **Progress Service** và các microservice liên quan trong hệ thống Longevia Backend.

---

## 1. Điểm tích hợp 1: Identity Service → Progress Service (gRPC Read Height)

- **Mục đích**: Progress Service cần lấy chiều cao (`HeightCm`) từ hồ sơ người dùng trong Identity Service để tính BMI và lưu snapshot bất biến (`HeightUsedCm`, `Bmi`, `BmiCategory`) khi người dùng ghi nhận chỉ số cơ thể (`POST /api/progress/metrics`).
- **Quyết định kiến trúc (Q02)**: **APPROVED (Bảo + Nguyên)**.
  - Hình thức: gRPC read nội bộ đồng bộ qua mạng nội bộ Docker (`http://identity:5000`).
  - Kiểu dữ liệu: `decimal`, khớp với định nghĩa cột PostgreSQL `numeric(5,2)` của Identity trong PR #10.
  - Ranh giới trách nhiệm: Identity là **Provider**, Progress là **Consumer**.
- **Hiện trạng & External Blocker**:
  - Identity đã merge PR #10 (cập nhật model và REST user profile).
  - Schema protobuf `GetBodyProfile` trong `contracts/identity/v1/identity.proto` và runtime implementation gRPC service trong Identity Service hiện **chưa có trên `main`**.
  - **Trạng thái**: **BLOCKED EXTERNAL PROVIDER** (Chủ sở hữu: Nguyên).
  - **Hành động tiếp theo**: Nguyên (`nguyen`) phát hành RPC method trong `contracts/identity/v1/identity.proto` và triển khai gRPC server tại `Identity.Api`.
  - **Triển khai tại Progress**:
    - Progress đã tạo abstraction [`IBodyProfileProvider`](file:///src/Services/Progress/Progress.Application/Abstractions/IBodyProfileProvider.cs) trong Application layer với kết quả `ProfileHeightResult` phân loại trạng thái rõ ràng.
    - Infrastructure layer đã có stub [`BodyProfileProvider`](file:///src/Services/Progress/Progress.Infrastructure/Adapters/Grpc/BodyProfileProvider.cs) mặc định trả về `ServiceUnavailable` khi chưa kết nối runtime Identity provider.
    - Kiểm thử tích hợp `MetricsTests.cs` đã kiểm chứng đầy đủ 4 trường hợp:
      - Khi provider trả về chiều cao (vd: 175cm) -> tính BMI 22.86, lưu snapshot, trả **`201 Created`**.
      - Khi provider trả về `HeightNotRecorded` (chưa có chiều cao) -> trả **`422 Unprocessable Entity`** với mã lỗi `progress.height_unavailable`.
      - Khi provider trả về `ProfileNotFound` -> trả **`404 Not Found`** với mã lỗi `progress.profile_not_found`.
      - Khi provider trả về `ServiceUnavailable` (Identity chưa sẵn sàng / RPC chưa có) -> trả **`503 Service Unavailable`** với mã lỗi `progress.identity_unavailable`.

---

## 2. Điểm tích hợp 2: Diet Service → Progress Service (RabbitMQ Meal Activity Event)

- **Mục đích**: Cập nhật ngày hoạt động (active day) của người dùng khi có bữa ăn được ghi nhận trong Diet Service để tính toán chuỗi ngày hoạt động liên tục (Streak) trên Dashboard.
- **Quyết định kiến trúc (Q06 / Q09)**: **APPROVED**.
  - Quy tắc Active Day: Là tập hợp hợp (union) của các ngày có nhật ký bữa ăn (`Diet.MealLog`) và các ngày có ghi nhận chỉ số cơ thể (`BodyMetricLog`). Nước, ngủ và fasting không tạo streak độc lập.
  - Hình thức tích hợp: Asynchronous Event qua RabbitMQ (`MealLoggedEvent`).
  - Ngày tính streak: Sử dụng `MealLog.Date` (`DateOnly` do client cung cấp hoặc UTC date), không dùng thời điểm nhận event qua mạng.
  - Ranh giới trách nhiệm: Diet Service là **Producer**, Progress Service là **Consumer**.
- **Hiện trạng & Triển khai tại Progress**:
  - **Shared Contract**: Đã bổ sung contract `Longevia.Shared.Events.MealLoggedEvent` với các trường `EventId`, `UserId`, `MealLogId`, `BusinessDate`, `CreatedAt`, `OccurredOn` khớp với PR #12 của Diet.
  - **Durable Inbox & Deduplication**: Bảng `processed_meal_events` (migration `20261024214957_AddProcessedMealEvents`) lưu trữ các event đã xử lý. Sử dụng transaction-scoped advisory lock `pg_advisory_xact_lock(hashtext(mealLogId))` để ngăn chặn race condition khi nhiều consumer nhận cùng event đồng thời.
  - **Conflicting Duplicate Invariant**: Nếu nhận event cùng `MealLogId` nhưng khác `UserId` hoặc `BusinessDate`, hệ thống từ chối và ném exception chuyển vào DLQ (`progress.meal-activity.dlq`), tuyệt đối không âm thầm ghi đè.
  - **MassTransit Consumer**: `MealLoggedConsumer` lắng nghe topic exchange `longevia.events` với routing key `diet.meal.logged`, durable queue `progress.meal-activity`, retry exponential 2s/5s/10s và dead-letter exchange `progress.meal-activity.dlx`.
  - **Historical Replay & Reconciliation**: Script `scripts/progress/reconcile-meal-activity-snapshot.ps1` hỗ trợ đối soát toàn bộ snapshot v1 từ file CSV hoặc API của Diet theo `contracts/diet/v1/meal-activity-snapshot.md`.
  - **Trạng thái**: **PROGRESS IMPLEMENTED & VERIFIED 100% (READY FOR DIET PR #12 MERGE)**. Toàn bộ 6 integration tests trong `MealActivityConsumerTests.cs` đã pass 100% trên Testcontainers PostgreSQL. Khi PR #12 của Huy được merge vào `origin/main`, Progress sẽ tự động nhận và xử lý event ngay tại runtime mà không cần sửa code.

---

## 3. Điểm tích hợp 3: Progress Service → Diet Service (Latest Weight for BMR/TDEE - Q10)

- **Mục đích**: Diet Service cần cân nặng mới nhất để tính chỉ số chuyển hoá cơ bản (BMR/TDEE) khi thiết lập mục tiêu dinh dưỡng.
- **Hiện trạng & Triển khai (Q10 / T038)**:
  - **Canonical Contract Proposal**: Đã biên soạn file protobuf canonical [`contracts/progress/v1/progress.proto`](file:///contracts/progress/v1/progress.proto) định nghĩa service `ProgressGrpcService` với RPC `GetLatestWeight`:
    - Numeric Representation: Sử dụng `int32 weight_kg_x100` (giá trị cân nặng nhân 100, ví dụ 72.45 kg = 7245) để triệt tiêu hoàn toàn sai số dấu phẩy động (floating-point precision issues).
    - Query Semantics: `as_of_utc` (truy vấn bản ghi cân nặng gần nhất tại hoặc trước thời điểm chỉ định: `LoggedAt <= asOfUtc ORDER BY LoggedAt DESC, Id DESC LIMIT 1`).
    - Explicit Empty Result: Sử dụng protobuf `oneof result { WeightRecord record; bool no_record_found; }` phân biệt rõ ràng giữa trường hợp có dữ liệu và trường hợp chưa từng ghi nhận cân nặng.
  - **Proposal Document**: Tài liệu [`contracts/progress/v1/README.md`](file:///contracts/progress/v1/README.md) liệt kê 7 câu hỏi kiến trúc cụ thể chờ Bảo (Dev 3) và Huy (Dev 2) biểu quyết phê duyệt chính thức.
  - **Query Scaffolding Implementation**: Progress đã triển khai service [`IWeightQueryService`](file:///src/Services/Progress/Progress.Application/Abstractions/IWeightQueryService.cs) và [`WeightQueryService`](file:///src/Services/Progress/Progress.Application/Features/Metrics/WeightQueryService.cs) trong Application layer, tuân thủ đúng as-of timestamp và thuật toán làm tròn `Math.Round(metric.WeightKg * 100m)`.
  - **Kiểm chứng**: 5 unit tests (`WeightQueryServiceTests.cs`) và 1 integration test (`WeightProviderIntegrationTests.cs`) pass 100%.
  - **Trạng thái**: **CONTRACT PROPOSED & SCAFFOLDING IMPLEMENTED (AWAITING APPROVAL)**. Runtime gRPC endpoint sẽ được mở khi hợp đồng được hai bên thống nhất.
