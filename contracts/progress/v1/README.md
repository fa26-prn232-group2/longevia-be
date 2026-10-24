# Progress Service Contracts v1 — Latest Weight RPC (Q10 Proposal)

Tài liệu này xác định đề xuất hợp đồng giao tiếp đồng bộ qua gRPC giữa **Progress Service** (Provider — Bảo) và **Diet Service** (Consumer — Huy) nhằm phục vụ tính toán BMR/TDEE.

Trạng thái: **PROPOSED / AWAITING APPROVAL** (Chờ phản hồi & thống nhất từ Huy và Bảo trước khi xuất bản thành production-ready).

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

## 2. Quy tắc Biểu diễn Dữ liệu & Ngữ nghĩa

1. **Biểu diễn Số học (Numeric Precision)**:
   - Sử dụng `int32 weight_kg_x100` (ví dụ `70.00 kg` biểu diễn thành `7000`, `68.50 kg` thành `6850`).
   - Tuyệt đối không dùng kiểu `float` hoặc `double` trong Protobuf để tránh sai lệch làm tròn số học nhị phân khi chuyển đổi sang `decimal` trong .NET.
2. **Ngữ nghĩa As-Of (Effective Time Semantics)**:
   - Truy vấn:
     ```sql
     SELECT * FROM progress.body_metric_logs
     WHERE user_id = @userId AND logged_at <= @asOfUtc
     ORDER BY logged_at DESC, id DESC
     LIMIT 1;
     ```
   - Sắp xếp ưu tiên `logged_at DESC`, nếu cùng timestamp sẽ dùng `id DESC` để đảm bảo kết quả hoàn toàn tất định (deterministic tie-break).
3. **Phân biệt Rõ ràng Giữa "Không có Dữ liệu" và "Lỗi Hệ thống"**:
   - Sử dụng `oneof result`: Khi người dùng chưa từng có cân nặng, trả về `no_record_found = true` (HTTP 200 gRPC OK), KHÔNG trả về số 0.
   - Khi dịch vụ Progress gặp sự cố hoặc kết nối lỗi: trả về mã lỗi gRPC chuẩn (`UNAVAILABLE`, `DEADLINE_EXCEEDED`).

---

## 3. Bảy Câu hỏi Cần Thống nhất Giữa Bảo (Progress) & Huy (Diet)

1. **Tính bắt buộc của `as_of_utc`**: `as_of_utc` nên là optional (nếu không truyền mặc định lấy `DateTimeOffset.UtcNow`) hay mandatory?
2. **Xử lý yêu cầu as-of ở tương lai**: Nếu client gửi `as_of_utc` lớn hơn giờ hiện tại của server, Progress nên giới hạn tại thời điểm hiện tại (`UtcNow`) hay giữ nguyên mốc tương lai?
3. **Loại trừ bản ghi tương lai**: Các bản ghi có `LoggedAt > server UtcNow` (nếu có do lệch đồng hồ client) có nên bị loại trừ khỏi kết quả BMR/TDEE không?
4. **Cơ chế báo thiếu cân nặng**: Cấu trúc `oneof result` (`record` vs `no_record_found`) đã phù hợp với logic xử lý của Diet chưa?
5. **Thời gian chờ (Deadline) & Retry**: Diet dự kiến đặt gRPC client deadline là bao nhiêu ms (ví dụ 1000ms hay 2000ms), có retry policy không?
6. **Ứng xử khi Provider Unavailable**: Khi Progress service bị ngắt kết nối hoặc timeout, Diet sẽ xử lý mục tiêu BMR/TDEE như thế nào (fallback về target manual hay trả null kèm warning)?
7. **Thời điểm snapshot của Diet**: Diet snapshot cân nặng vào thời điểm nào khi tạo/cập nhật mục tiêu dinh dưỡng?
