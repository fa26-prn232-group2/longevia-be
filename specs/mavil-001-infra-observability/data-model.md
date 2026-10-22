# Mô Hình Dữ Liệu & Hợp Đồng (Data Model & Schema) — Infra & Observability

**Feature**: `specs/mavil-001-infra-observability`  
**Nhánh**: `feat/infra-observability`  
**Ngày**: 2026-10-22

---

## 1. Cấu trúc Mô hình Sự kiện Bất đồng bộ (Event Models)

Các mô hình sự kiện này được định nghĩa dưới dạng C# `record` bất biến (Immutable Records) đặt trong `src/Shared/Events/` để các dịch vụ Producer (Identity) và Consumer (Worker) sử dụng chung qua MassTransit. Canonical schema có version được lập chỉ mục tại `contracts/events/v1/README.md` (trỏ tới record C# tương ứng — record là nguồn chân lý khả biên dịch, tài liệu contracts ghi lại version và tương thích ngược).

Mọi event mang hằng `SchemaVersion = 1`. Quy tắc version theo `docs/system-design/event-driven-design.md` §Contract event: thêm field optional = tương thích ngược (giữ version); đổi tên/xóa field hoặc đổi ngữ nghĩa = version mới + kế hoạch chuyển đổi. Consumer MUST bỏ qua field lạ (cấu hình serializer `MissingMemberHandling.Ignore` / tương đương) và MUST idempotent (xem §4).

### 1.1 `UserRegisteredEvent` (`SchemaVersion = 1`)
Sự kiện được phát hành bởi Identity Service khi người dùng đăng ký tài khoản thành công. Producer phía Identity PHẢI publish qua **transactional outbox** (ghi outbox trong cùng transaction với tạo user, relay nền publish lên RabbitMQ) — việc cài đặt outbox thuộc phạm vi service Identity (sở hữu `identity_db` và migration); tính năng này cung cấp phía consumer (queue bền, retry, DLX, idempotency) và định nghĩa contract tại đây.

| Thuộc tính | Kiểu dữ liệu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `UserId` | `Guid` | Có | Định danh duy nhất của người dùng |
| `Email` | `string` | Có | Địa chỉ email người nhận |
| `FullName` | `string` | Có | Họ và tên người dùng |
| `VerificationToken` | `string` | Có | Mã token dùng để kích hoạt tài khoản |
| `OccurredOn` | `DateTimeOffset` | Có | Thời điểm phát sinh sự kiện (UTC) |

### 1.2 `PasswordResetRequestedEvent` (`SchemaVersion = 1`)
Sự kiện được phát hành khi người dùng yêu cầu đặt lại mật khẩu.

| Thuộc tính | Kiểu dữ liệu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `Email` | `string` | Có | Địa chỉ email yêu cầu |
| `ResetToken` | `string` | Có | Mã token xác thực đổi mật khẩu |
| `ExpiresAt` | `DateTimeOffset` | Có | Thời hạn hết hiệu lực của token (15 phút) |
| `OccurredOn` | `DateTimeOffset` | Có | Thời điểm phát sinh sự kiện (UTC) — consumer đóng timestamp xử lý riêng theo Hiến pháp §III, không dùng field này để sắp thứ tự |

**Ghi chú bảo mật token trong event (quyết định có chủ đích):** `VerificationToken` / `ResetToken` là mã single-use, TTL ngắn (kích hoạt 24h, reset 15 phút), bắt buộc phải có trong payload để Worker render email mà không gọi ngược producer (đúng nguyên tắc "payload đủ để consumer xử lý" của `contract-purity-pattern.md`). Giảm thiểu: RabbitMQ nội bộ mạng `data_net` (không expose), KHÔNG log token ở bất kỳ mức nào (log redaction bắt buộc), TLS khi ra Brevo. Không coi token trong event nội bộ là vi phạm "dữ liệu nhạy cảm" (mật khẩu/secret dài hạn) — quyết định này được ghi để audit.

---

## 1.3 Topology RabbitMQ (khai báo tường minh, MassTransit)

Tuân thủ `docs/system-design/event-driven-design.md` §Topology — khai báo bằng code (MassTransit endpoint configuration), không dựa vào auto-declare ngầm:

| Thành phần | Giá trị |
| :--- | :--- |
| Exchange nghiệp vụ | `topic`, tên `longevia.events` |
| Routing key | `identity.user.registered` cho `UserRegisteredEvent`; `identity.user.password-reset-requested` cho `PasswordResetRequestedEvent` (dạng `<domain>.<entity>.<action>`) |
| Queue | `worker.email` (mỗi consumer một queue, dạng `<service>.<purpose>`) |
| Dead-letter | `worker.email.dlx` + `worker.email.dlq`; retry 3 lần (2s, 5s, 10s) trước khi vào DLQ |
| Prefetch | giới hạn (mặc định 10) để tránh một consumer ngốn hết |

**Idempotency consumer:** Worker lưu `UserId + SchemaVersion + OccurredOn` (event id) đã xử lý vào Redis với TTL 7 ngày; nhận trùng → ack và bỏ qua (không gửi lại email). Serializer bỏ qua field lạ để tương thích ngược.

---

## 2. Mô hình Email nội bộ Worker (Email DTOs & Templates)

Được sử dụng nội bộ trong Background Worker để chuẩn hóa dữ liệu đầu vào cho `IEmailSender`.

### 2.1 `EmailMessage`
```csharp
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null
);
```

### 2.2 Danh mục Mẫu Thư điện tử (Email Templates)
1. **Verification Email**: Gửi kèm nút bấm có URL: `http://localhost:8080/api/identity/auth/verify?token={token}`.
2. **Password Reset Email**: Gửi kèm mã OTP và liên kết đặt lại mật khẩu trong vòng 15 phút.
3. **Welcome Email**: Thư chào mừng sau khi kích hoạt thành công, giới thiệu các tính năng ăn uống và theo dõi sức khỏe.
4. **Weekly Digest Email**: Báo cáo tổng kết tuần (Calo trung bình, số giờ nhịn ăn, chuỗi Streak) — mọi số liệu do service producer (Diet/Progress) tính sẵn và đóng gói trong event payload; Worker CHỈ render HTML, KHÔNG chứa logic tổng hợp nghiệp vụ (Hiến pháp: Worker MUST NOT chứa nghiệp vụ riêng).

---

## 3. Mô hình Cấu hình Môi trường (Environment Variables Schema)

Bảng đối chiếu các biến môi trường được quản lý tại `.env` và `.env.example`:

| Tên biến | Kiểu giá trị | Mặc định (Local) | Mô tả |
| :--- | :--- | :--- | :--- |
| `GATEWAY_PORT` | `int` | `8080` | Cổng tiếp nhận của Nginx Gateway |
| `GRAFANA_PORT` | `int` | `3001` | Cổng Dashboard Grafana trên Host |
| `PROMETHEUS_PORT` | `int` | `9090` | Cổng máy chủ Prometheus |
| `MAILPIT_WEB_PORT` | `int` | `8025` | Cổng giao diện Web Mailpit |
| `MAILPIT_SMTP_PORT`| `int` | `1025` | Cổng nhận thư SMTP Mailpit |
| `POSTGRES_PORT` | `int` | `5432` | Cổng máy chủ PostgreSQL (pgvector) |
| `REDIS_HOST` | `string` | `redis` (trong compose) / `localhost` (chạy tay) | Redis host cho Worker dedup (compose truyền `REDIS__HOST`, code fallback `REDIS_HOST` rồi `localhost`) |
| `REDIS_PORT` | `int` | `6379` | Cổng máy chủ Redis 7 |
| `RABBITMQ_PORT` | `int` | `5672` | Cổng kết nối AMQP RabbitMQ |
| `SMTP__HOST` | `string` | `mailpit` (trong compose network) / `localhost` (chạy tay ngoài Docker) / `smtp-relay.brevo.com` (Prod) | Địa chỉ máy chủ SMTP |
| `SMTP__PORT` | `int` | `1025` (Local) / `587` (Prod) | Cổng kết nối SMTP |
| `SMTP__USE_SSL` | `bool` | `false` (Local) / `true` (Prod) | Sử dụng mã hóa StartTLS/SSL |
| `SMTP__USER` | `string` | `""` | Tài khoản đăng nhập SMTP (Brevo login key ở Prod) |
| `SMTP__PASSWORD` | `string` | `""` | Mật khẩu SMTP (Brevo SMTP key ở Prod) |
| `SMTP__SENDER_EMAIL`| `string` | `no-reply@longevia.local` | Email hiển thị người gửi |

---

## 4. Mô hình Đo lường OpenTelemetry Metrics (Observability Schema)

Các chỉ số chuẩn được tự động thu thập từ endpoint `/metrics`:

| Metric Name | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `http.server.request.duration` | Histogram | Thời gian thực thi HTTP request (đo lường p50, p95, p99) |
| `http.server.active_requests` | UpDownCounter | Số lượng HTTP request đang được xử lý đồng thời |
| `process.cpu.utilization` | Gauge | Tỷ lệ sử dụng CPU của tiến trình .NET (0.0 - 1.0) |
| `process.memory.working_set` | Gauge | Bộ nhớ RAM vật lý đang chiếm dụng (bytes) |
| `dotnet.gc.collections` | Counter | Số lần thực hiện Garbage Collection theo Gen 0, Gen 1, Gen 2 |
| `dotnet.threadpool.thread.count`| Gauge | Số lượng thread đang chạy trong ThreadPool |
