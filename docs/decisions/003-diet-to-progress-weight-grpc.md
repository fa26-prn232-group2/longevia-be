# ADR 003 - Giao tiếp gRPC đồng bộ Diet → Progress truy vấn cân nặng gần nhất

- **Status**: Accepted
- **Decision Date**: 2026-10-30
- **Decision Maker**: Bảo (Progress Service Owner) và Huy (Diet Service Owner)

## 1. Context

Trong kiến trúc microservices của Longevia:
- **Diet Service** sở hữu nghiệp vụ quản lý dinh dưỡng, kế hoạch bữa ăn và tính toán chỉ số năng lượng tiêu hao/cơ bản (BMR/TDEE) để thiết lập mục tiêu calo và macronutrients cho người dùng.
- Công thức tính BMR/TDEE (Mifflin-St Jeor) yêu cầu 4 chỉ số cơ thể: Giới tính sinh học, Tuổi (tính từ ngày sinh), Chiều cao (cm), và Cân nặng (kg).
- Giới tính, ngày sinh và chiều cao thuộc hồ sơ cá nhân do **Identity Service** sở hữu (`identity_db`).
- Cân nặng là chỉ số theo dõi diễn biến sức khỏe theo thời gian (`BodyMetricLog`), thuộc phạm vi quản lý tiến độ do **Progress Service** độc quyền sở hữu trong `progress_db`.

Ràng buộc kiến trúc:
1. **Ranh giới cơ sở dữ liệu (Database-per-Service)**: Hiến pháp dự án (`.specify/memory/constitution.md`) và `docs/system-design/microservices/01-service-boundaries.md` nghiêm cấm truy cập chéo cơ sở dữ liệu (kể cả quyền chỉ đọc). `diet_db` tuyệt đối không được truy vấn bảng `progress.body_metric_logs`.
2. **Giao tiếp đồng bộ liên service**: Khi một service cần dữ liệu tức thời từ service khác trong cùng luồng xử lý (Diet cần cân nặng khi người dùng tạo/cập nhật mục tiêu), kênh giao tiếp chuẩn là **gRPC đồng bộ nội bộ**, định nghĩa bởi file Protobuf versioned trong thư mục `contracts/`.
3. **Thực trạng**: Trước khi quyết định này được phê duyệt (vấn đề Q10), Progress Service đã phát hành đề xuất hợp đồng Protobuf và query scaffolding, nhưng chưa có ADR chính thức cho phép chiều gọi **Diet → Progress** và chưa thống nhất chi tiết ngữ nghĩa giữa hai service owner Bảo và Huy.

Vào ngày 2026-10-30, Huy (Diet Service Owner) đã xác nhận phê duyệt toàn bộ các khía cạnh ngữ nghĩa của câu hỏi kiến trúc Q10 với Bảo (Progress Service Owner).

## 2. Decision

### 2.1 Cho phép chiều gọi gRPC đồng bộ: Diet → Progress

Bổ sung chiều gọi gRPC đồng bộ nội bộ mới vào kiến trúc hệ thống:
- **Caller (Consumer)**: Diet Service.
- **Provider (Server)**: Progress Service.
- **RPC Method**: `longevia.progress.v1.ProgressService/GetLatestWeight`.

### 2.2 Hợp đồng Protobuf chính thức (Canonical Protobuf Contract)

Hợp đồng được chuẩn hóa tại [`contracts/progress/v1/progress.proto`](../../contracts/progress/v1/progress.proto) thuộc package `longevia.progress.v1` và C# namespace `Longevia.Progress.Contracts.V1`:

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

### 2.3 Ngữ nghĩa Mốc Thời gian (As-Of) và Lọc Dữ liệu

1. **User ID**: Bắt buộc là chuỗi GUID chuẩn RFC 4122 không rỗng. Nếu không hợp lệ hoặc rỗng, Progress Server trả về mã lỗi gRPC `INVALID_ARGUMENT`.
2. **Mốc thời gian hiệu lực (Effective As-Of)**:
   - `as_of_utc` là trường tùy chọn (`optional`).
   - Nếu client không truyền `as_of_utc`, server sử dụng thời điểm server UTC hiện tại (`TimeProvider.GetUtcNow()`).
   - Nếu client truyền `as_of_utc` lớn hơn thời điểm server UTC hiện tại (mốc thời gian ở tương lai), server sẽ tự động clamp mốc thời gian hiệu lực về `server UtcNow`.
3. **Loại trừ số đo tương lai (Future Measurements Exclusion)**:
   - Các bản ghi `BodyMetricLog` có `LoggedAt > server UtcNow` (có thể do sai lệch đồng hồ client khi ghi nhận số đo trước đó) luôn bị loại trừ.
4. **Truy vấn tất định (Deterministic Ordering & Tie-Break)**:
   - Truy vấn chọn bản ghi mới nhất thỏa mãn `LoggedAt <= effectiveAsOf`.
   - Tiêu chí sắp xếp: `ORDER BY LoggedAt DESC, Id DESC LIMIT 1`. Việc bổ sung `Id DESC` bảo đảm kết quả trả về là hoàn toàn tất định khi có nhiều bản ghi có cùng timestamp `LoggedAt`.

### 2.4 Biểu diễn Số học (Numeric Precision)

- Cân nặng được biểu diễn bằng `int32 weight_kg_x100` (nhân với 100), ví dụ: `70.50 kg` thành `7050`, `68.25 kg` thành `6825`.
- Tránh dùng kiểu `float`/`double` trong Protobuf để loại bỏ hoàn toàn sai số làm tròn số học dấu phẩy động nhị phân khi mapping sang kiểu `decimal` trong .NET.
- Server thực hiện chuyển đổi có kiểm tra tràn số `checked((int)Math.Round(metric.WeightKg * 100m, MidpointRounding.AwayFromZero))`.

### 2.5 Phân biệt Dữ liệu Vắng mặt vs Lỗi Hệ thống

- Cấu trúc `oneof result`:
  - Khi người dùng chưa có bản ghi cân nặng nào: Server trả gRPC `OK` với `no_record_found = true`. Tuyệt đối không trả về số 0 giả mạo hoặc giá trị fallback.
  - Khi người dùng có bản ghi cân nặng hợp lệ: Server trả gRPC `OK` với `record = WeightRecord(...)` kèm `measured_at` và `source_metric_id`.
- Khi xảy ra sự cố hạ tầng hoặc kết nối:
  - Database PostgreSQL `progress_db` không kết nối được hoặc timeout: Server trả về mã lỗi `UNAVAILABLE`.
  - Client hủy request: Trả về mã lỗi `CANCELLED`.
  - Quá deadline: Trả về mã lỗi `DEADLINE_EXCEEDED`.
  - Lỗi bất thường khác: Trả về mã lỗi `INTERNAL` với thông điệp chung đã được khử nhạy cảm (sanitized), không lộ connection string hay stack trace.
  - Tuyệt đối không chuyển exception kỹ thuật thành `no_record_found`.

### 2.6 Thời gian Chờ (Deadline) & Snapshot Bất biến

- **Deadline**: Diet Service áp dụng bounded deadline khi gọi gRPC sang Progress (mặc định 2000ms theo thỏa thuận Q10).
- **Snapshot Immutability**:
  - Khi Diet Service tính toán BMR/TDEE và tạo mục tiêu dinh dưỡng, Diet snapshot giá trị cân nặng được dùng vào bảng dữ liệu của Diet.
  - Các mục tiêu dinh dưỡng đã tạo trong quá khứ tuyệt đối không được tự động tính toán lại khi người dùng ghi nhận hoặc thay đổi cân nặng sau đó trong Progress Service.

### 2.7 Mô hình Bảo mật & Mạng (Security Model)

- Áp dụng mô hình internal trust domain đã được phê duyệt tại [ADR 001 §2.2](001-authn-authz-architecture.md):
  - Giao tiếp giữa Diet và Progress diễn ra trong cùng mạng Docker Compose nội bộ (`data_net`).
  - Không triển khai caller authentication, mTLS, hay shared secret token giữa các service trong Compose project.
  - Cổng gRPC của Progress (`50051`) là cổng nội bộ, tuyệt đối không publish ra host và không route qua Nginx Gateway.

### 2.8 Phân định Quyền Sở hữu & Triển khai

- **Bảo (Progress Service Owner)**: Triển khai và vận hành gRPC server trong `Progress.Api`, DbContext, repository, và toàn bộ integration tests của Progress.
- **Huy (Diet Service Owner)**: Triển khai gRPC client trong Diet Service, logic tính toán BMR/TDEE và snapshot dữ liệu sang `diet_db`.
- **Versioning trong tương lai**: Bổ sung trường mới phải là trường `optional` để đảm bảo tương thích ngược; nếu đổi trường hiện có phải tạo proto version mới (`longevia.progress.v2`).

## 3. Consequences

### Tích cực
- Tôn trọng triệt để nguyên tắc độc lập cơ sở dữ liệu (Database-per-Service) và hiến pháp repo.
- Đảm bảo tính nhất quán và loại bỏ sai số số học cho chỉ số BMR/TDEE quan trọng.
- Phân định ranh giới trách nhiệm rõ ràng giữa hai service owner.
- Truy vấn tất định, có cơ chế tie-break ổn định.
- Bảo mật thông qua cô lập mạng nội bộ Compose theo ADR 001 §2.2.

### Tiêu cực / Ràng buộc
- Diet Service chịu phụ thuộc thời gian chạy (runtime synchronous dependency) vào Progress Service khi tính BMR/TDEE; nếu Progress không khả dụng hoặc quá hạn 2s, Diet phải xử lý lỗi phù hợp (fallback target hoặc trả lỗi cho client).
- Cần duy trì Kestrel HTTP/2 gRPC listener tại Progress Service song song với HTTP/1 REST listener hiện hữu.
