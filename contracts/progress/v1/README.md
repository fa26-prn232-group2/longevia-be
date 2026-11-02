# Progress Service Contracts v1 — Latest Weight RPC (Q10 Canonical Contract)

Hợp đồng giao tiếp đồng bộ qua gRPC giữa **Progress Service** (Provider — Bảo) và **Diet Service** (Consumer — Huy) nhằm phục vụ tính toán BMR/TDEE và lập mục tiêu dinh dưỡng.

Trạng thái: **APPROVED / CANONICAL** (Đã được phê duyệt bởi Bảo — Progress Service Owner và Huy — Diet Service Owner theo quyết định Q10 và ghi nhận tại [ADR 003](../../docs/decisions/003-diet-to-progress-weight-grpc.md)).

---

## 1. Proto Contract Definition

Tệp nguồn: [`contracts/progress/v1/progress.proto`](progress.proto)

```protobuf
syntax = "proto3";

package longevia.progress.v1;

option csharp_namespace = "Longevia.Progress.Contracts.V1";

import "google/protobuf/timestamp.proto";

service ProgressService {
  rpc GetLatestWeight (GetLatestWeightRequest) returns (GetLatestWeightResponse);
}

message GetLatestWeightRequest {
  string user_id = 1;
  optional google.protobuf.Timestamp as_of_utc = 2;
}

message WeightRecord {
  int32 weight_kg_x100 = 1;
  google.protobuf.Timestamp measured_at = 2;
  string source_metric_id = 3;
}

message GetLatestWeightResponse {
  oneof result {
    WeightRecord record = 1;
    bool no_record_found = 2;
  }
}
```

---

## 2. Quy tắc Biểu diễn Dữ liệu & Ngữ nghĩa Kỹ thuật

1. **Biểu diễn Số học (Numeric Precision)**:
   - Sử dụng kiểu số nguyên `int32 weight_kg_x100` (ví dụ `70.50 kg` biểu diễn thành `7050`, `68.25 kg` thành `6825`).
   - Tuyệt đối không dùng kiểu `float` hoặc `double` trong Protobuf để tránh sai lệch làm tròn số học nhị phân khi chuyển đổi sang `decimal` trong .NET.
   - Provider chuyển đổi từ `decimal WeightKg` sang `int32` có kiểm tra tràn số (`checked`) và làm tròn theo chuẩn toán học (`Math.Round(..., MidpointRounding.AwayFromZero)`).

2. **Ngữ nghĩa Mốc Thời gian (Effective As-Of Semantics)**:
   - Tham số `as_of_utc` là tùy chọn (optional).
   - Nếu `as_of_utc` không được cung cấp, server sử dụng thời điểm hiện tại của server (`TimeProvider.GetUtcNow()`).
   - Nếu `as_of_utc` truyền vào lớn hơn thời điểm hiện tại của server (mốc thời gian ở tương lai), server sẽ tự động giới hạn (clamp) mốc thời gian hiệu lực về thời điểm `UtcNow` hiện tại của server.
   - Các bản ghi đo lường có `LoggedAt` lớn hơn thời điểm hiện tại của server (do lệch đồng hồ client) luôn bị loại trừ.

3. **Truy vấn Tất định (Deterministic Query)**:
   - Truy vấn SQL tương đương:
     ```sql
     SELECT * FROM progress.body_metric_logs
     WHERE user_id = @userId AND logged_at <= @effectiveAsOfUtc
     ORDER BY logged_at DESC, id DESC
     LIMIT 1;
     ```
   - Sắp xếp ưu tiên `logged_at DESC`, nếu cùng timestamp sẽ dùng `id DESC` để đảm bảo kết quả hoàn toàn tất định (deterministic tie-break).

4. **Phân biệt Rõ ràng Giữa "Không có Dữ liệu" và "Lỗi Hệ thống"**:
   - Sử dụng cấu trúc `oneof result`:
     - Khi người dùng chưa từng có cân nặng thỏa mãn điều kiện thời gian: trả về gRPC `OK` với `no_record_found = true`. Tuyệt đối KHÔNG trả về số 0 giả lập hoặc giá trị mặc định.
     - Khi người dùng có cân nặng hợp lệ: trả về gRPC `OK` với `record = WeightRecord(...)`.
   - Khi dịch vụ gặp lỗi:
     - `user_id` không phải GUID hợp lệ hoặc rỗng: trả về gRPC `INVALID_ARGUMENT`.
     - Database hoặc dịch vụ Provider gặp sự cố kết nối: trả về gRPC `UNAVAILABLE`.
     - Caller hủy yêu cầu: trả về gRPC `CANCELLED`.
     - Quá thời hạn xử lý: trả về gRPC `DEADLINE_EXCEEDED`.
     - Lỗi nội bộ không lường trước: trả về gRPC `INTERNAL` kèm thông điệp đã khử nhạy cảm (sanitized).
   - Tuyệt đối không bắt mọi exception chuyển thành `no_record_found`.

5. **Thời gian Chờ (Client Deadline) & Thử lại (Retry)**:
   - Diet Service cấu hình bounded deadline cho client gọi gRPC sang Progress (mặc định 2000ms theo thỏa thuận Q10).
   - Không cấu hình retry mù quáng với các thao tác thất bại do quá thời gian hoặc lỗi logic.

6. **Bất biến của Snapshot Dinh dưỡng (Snapshot Immutability)**:
   - Khi Diet Service tính toán mục tiêu dinh dưỡng (BMR/TDEE), Diet sẽ snapshot giá trị cân nặng đã lấy tại thời điểm đó vào bảng dữ liệu của Diet.
   - Các bản ghi snapshot mục tiêu trong quá khứ tuyệt đối không tự ý tính toán lại khi người dùng cập nhật hoặc ghi thêm bản ghi cân nặng mới trong Progress Service.

7. **Mô hình Bảo mật & Mạng (Trust Domain)**:
   - Áp dụng mô hình internal trust domain theo [ADR 001 §2.2](../../docs/decisions/001-authn-authz-architecture.md):
     - Giao tiếp gRPC nội bộ trong mạng Docker Compose `data_net`.
     - Không yêu cầu mTLS hoặc token xác thực caller riêng.
     - Cổng gRPC (`50051`) của Progress là cổng nội bộ, tuyệt đối không publish ra host và không route qua Nginx Gateway.

---

## 3. Phân định Trách nhiệm (Ownership)

- **Bảo (Progress Service Owner)**:
  - Triển khai và duy trì gRPC Server `ProgressService.ProgressServiceBase` tại Progress Service (`Progress.Api`).
  - Đảm bảo truy vấn PostgreSQL `progress_db`, hiệu năng index, và bộ unit/integration tests trên môi trường thực.
- **Huy (Diet Service Owner)**:
  - Triển khai gRPC Client bên Diet Service gọi tới `http://progress:50051`.
  - Triển khai tính toán BMR/TDEE và lưu trữ snapshot cân nặng vào cơ sở dữ liệu của Diet Service (`diet_db`).
