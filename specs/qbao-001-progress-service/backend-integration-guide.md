# Hướng dẫn tích hợp Backend — Progress Service (Close-Out)

Trạng thái tổng thể: **PROGRESS_IMPLEMENTED_PENDING_EXTERNAL_ROLLOUT**.
- Toàn bộ phía Progress (gRPC Provider, Height Consumer, Meal Activity Consumer, Reconciler) đã hoàn tất và kiểm chứng 100%.
- Chờ rollout phía external: Huy triển khai Diet gRPC client & snapshot, chạy replay & đối soát dữ liệu trên môi trường triển khai thực tế, và Huy xác nhận Q01 UTC.

---

## 1. Latest Weight Provider (Q10 / ADR 003) — Diet → Progress

Q10 đã chính thức được **PHÊ DUYỆT (APPROVED)** bởi Bảo (Progress) và Huy (Diet) theo [ADR 003](../../docs/decisions/003-diet-to-progress-weight-grpc.md).

- **Canonical Contract**: [`contracts/progress/v1/progress.proto`](../../contracts/progress/v1/progress.proto)
- **Package**: `longevia.progress.v1`
- **C# Namespace**: `Longevia.Progress.Contracts.V1`
- **RPC Service**: `ProgressService`
- **RPC Method**: `rpc GetLatestWeight (GetLatestWeightRequest) returns (GetLatestWeightResponse)`
- **Địa chỉ kết nối nội bộ**: `http://progress:50051` (HTTP/2 cleartext trong mạng `data_net`).
- **Bounded Deadline đề xuất**: 2000ms.
- **Trạng thái Server Progress**: **READY & VERIFIED** (20 test cases và luồng End-to-End đã kiểm chứng).
- **Trách nhiệm của Huy (Diet Service)**:
  - Triển khai gRPC Client bên Diet Service gọi tới Progress.
  - Sử dụng cân nặng nhận được để tính BMR/TDEE theo công thức Mifflin-St Jeor.
  - Snapshot cân nặng đã dùng vào `diet_db`. Không tự động recompute target cũ khi có cân nặng mới.

### Chi tiết Hợp đồng và Ngữ nghĩa Wire
```protobuf
message GetLatestWeightRequest {
  string user_id = 1;                              // GUID bắt buộc, RFC 4122 không rỗng
  optional google.protobuf.Timestamp as_of_utc = 2; // Mốc thời gian (tùy chọn)
}

message WeightRecord {
  int32 weight_kg_x100 = 1;                        // Cân nặng kg * 100 (ví dụ 70.50 kg -> 7050)
  google.protobuf.Timestamp measured_at = 2;       // Thời điểm đo thực tế (LoggedAt)
  string source_metric_id = 3;                     // ID của BodyMetricLog nguồn (GUID string)
}

message GetLatestWeightResponse {
  oneof result {
    WeightRecord record = 1;                       // Có bản ghi cân nặng hợp lệ
    bool no_record_found = 2;                      // Không tìm thấy bản ghi nào (true)
  }
}
```

### Bảng Mã Lỗi và Xử lý gRPC:
| Tình huống | Mã gRPC | Mô tả xử lý |
| --- | --- | --- |
| `user_id` không hợp lệ hoặc rỗng | `INVALID_ARGUMENT` | Client truyền user GUID sai định dạng |
| Không có bản ghi thỏa mãn | `OK` (`no_record_found = true`) | Người dùng chưa có cân nặng tại/trước as-of |
| Có bản ghi thỏa mãn | `OK` (`record = WeightRecord(...)`) | Trả về bản ghi mới nhất theo `LoggedAt DESC, Id DESC` |
| Database PostgreSQL lỗi/timeout | `UNAVAILABLE` | Lỗi kết nối `progress_db`, client không coi là thiếu cân nặng |
| Caller hủy request | `CANCELLED` | Yêu cầu bị hủy |
| Quá hạn xử lý | `DEADLINE_EXCEEDED` | Quá thời gian chờ |
| Lỗi không xác định khác | `INTERNAL` | Lỗi server, thông điệp đã khử nhạy cảm |

---

## 2. Identity Height gRPC (Q02) — Progress → Identity

- **Trạng thái Functional Integration**: **DONE & VERIFIED** (17 scoped scenarios).
- **Trạng thái Security Trust Model**: Tuân thủ ADR 001 §2.2 (Docker Compose network trust).
- **Canonical Contract**: `contracts/identity/v1/identity.proto` (`IdentityService/GetUserHeight`).
- **Địa chỉ kết nối**: `http://identity:50051`.
- **Cơ chế**: Khi người dùng ghi chỉ số cơ thể (`POST /api/progress/metrics`), Progress gọi gRPC sang Identity lấy chiều cao tươi để tính BMI và lưu snapshot bất biến (`HeightUsedCm`, `Bmi`, `BmiCategory`).
- **Mapping lỗi gRPC Identity → HTTP Problem Details của Progress**:
  - Valid Height → 201 Created.
  - Height unset (optional null) → 422 Unprocessable Entity (`progress.height_unavailable`).
  - Profile not found (`NOT_FOUND`) → 404 Not Found (`progress.profile_not_found`).
  - Identity unavailable / timeout → 503 Service Unavailable (`progress.identity_unavailable`).
  - Invalid argument / internal error → 502 Bad Gateway (`progress.identity_integration_failure`).

---

## 3. Diet Meal Activity Event & Projection — RabbitMQ

Diet PR #12 đã được **MERGED vào main** (commit `4c62273`).

- **Canonical Event**: `Longevia.Shared.Events.MealLoggedEvent` (SchemaVersion 1 constant).
  - `EventId`: GUID (bằng MealLogId, ổn định qua replay).
  - `UserId`: GUID chủ nhật ký bữa ăn.
  - `MealLogId`: GUID bản ghi bữa ăn.
  - `BusinessDate`: `DateOnly` ngày ăn do client chọn, bất biến.
  - `CreatedAt`: `DateTimeOffset` UTC lúc tạo bản ghi gốc.
  - `OccurredOn`: `DateTimeOffset` UTC lúc enqueue (có thể mới hơn khi replay).
- **Topology**:
  - Exchange: `longevia.events` (topic).
  - Routing key: `diet.meal.logged`.
  - Queue: `progress.meal-activity` (durable).
  - Error Queue: `progress.meal-activity_error`.
- **Idempotency & Projection**:
  - Atomic transaction: Inbox (`processed_meal_events`) + Contribution (`activity_contributions`).
  - Nhiều bữa ăn trong cùng một `BusinessDate` chỉ tạo duy nhất 1 active day cho streak.
  - Bữa ăn và bản ghi số đo cơ thể trong cùng một ngày chỉ tính 1 active day.
  - Bữa ăn ghi lùi ngày (backdated `BusinessDate`) được giữ nguyên ngày nghiệp vụ, không đổi theo thời gian nhận.
- **Đối soát Snapshot JSON v1**:
  - Công cụ: `Progress.Api --reconcile-meal-snapshot <path>`.
  - Kiểm tra SHA-256 checksum, đối soát RepeatableRead transaction trên `progress_db`.
  - Chi tiết runbook 4 bước tại [corrective-reconciliation.md](corrective-reconciliation.md).

---

## 4. Counted Integration Sync

| Chiều tích hợp | Giao thức | Hợp đồng / Schema | Số lượng RPC / Message / Field | Vai trò Progress | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| **Progress → Identity** | gRPC (HTTP/2) | `contracts/identity/v1/identity.proto` | 1 RPC (`GetUserHeight`), 2 Messages, 2 Fields | Client (Consumer) | **DONE & VERIFIED** |
| **Diet → Progress** | gRPC (HTTP/2) | `contracts/progress/v1/progress.proto` | 1 RPC (`GetLatestWeight`), 3 Messages, 7 Fields | Server (Provider) | **READY & VERIFIED** |
| **Diet → Progress** | RabbitMQ (Topic) | `Longevia.Shared.Events.MealLoggedEvent` | 1 Event Class, 6 Fields (`EventId`, `UserId`, `MealLogId`, `BusinessDate`, `CreatedAt`, `OccurredOn`) | Consumer | **DONE & VERIFIED** |

---

## 5. Quy tắc Thời gian Q01 (UTC MVP) & Trạng thái Cross-Service

- **Nội bộ Progress**: Bảo đã phê duyệt áp dụng chuẩn UTC Calendar cho mọi ngày server-derived (water/today, sleep wake date, metric activity date, streak calculation, và week boundary bắt đầu từ thứ Hai 00:00 UTC).
- **Phía Diet**: Tiêu thụ nguyên văn `MealLoggedEvent.BusinessDate`.
- **Trạng thái Xác nhận từ Huy**: **PENDING**. Cần Huy xác nhận rằng phía Diet/frontend tự chọn ngày hiện tại theo UTC date.
