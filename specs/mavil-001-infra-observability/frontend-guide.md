# Frontend Integration Guide — Infra & Observability

> Tài liệu hướng dẫn tích hợp frontend cho tính năng hạ tầng, API Gateway và xử lý lỗi RFC 7807 tại Gateway.
> Lưu tại `specs/mavil-001-infra-observability/frontend-guide.md`.

## 1. Summary

- Feature/spec: `specs/mavil-001-infra-observability/spec.md`
- Requirement: `docs/system-design/03-api-gateway.md`, `docs/decisions/001-authn-authz-architecture.md`
- Handoff status: `READY_FOR_FRONTEND`
- Canonical contracts: `deployments/nginx/nginx.conf`, RFC 7807 (Problem Details for HTTP APIs)
- Counts: new endpoints 1 (`/healthz`) · changed 3 (`/api/identity/*`, `/api/diet/*`, `/api/progress/*`) · removed 0 · events 0 (chỉ dùng nội bộ qua RabbitMQ) · DTOs 2 (RFC 7807 Problem Details 404 và 502)

## 2. Authentication and behavior

- **Authentication / Authorization**:
  - API Gateway **không** can thiệp và **không** chấm dứt (terminate) JWT authentication.
  - Gateway chuyển tiếp toàn bộ header `Authorization: Bearer <token>` nguyên vẹn về upstream service tương ứng.
  - Mỗi downstream microservice (`Identity`, `Diet`, `Progress`) tự xác thực chữ ký token ES256 qua public key cấu hình từ biến môi trường (ADR 001).
- **Prefix Stripping**:
  - Client gọi API thông qua Gateway port 8080 (hoặc domain chuẩn):
    - `/api/identity/{route}` → chuyển tiếp tới `identity:8080/{route}`
    - `/api/diet/{route}` → chuyển tiếp tới `diet:8080/{route}`
    - `/api/progress/{route}` → chuyển tiếp tới `progress:8080/{route}`
  - Upstream backend services định nghĩa route không cần lặp lại prefix `/api/<service>/`.
- **SSE & WebSocket**:
  - Nginx Gateway cấu hình chuyển tiếp WebSocket/SSE Upgrade headers (`$http_upgrade`, `$connection_upgrade`).
  - `proxy_read_timeout 300s` đảm bảo kết nối stream thời gian thực không bị ngắt giữa chừng.
- **Service Isolation**:
  - Dịch vụ `Recommendation` là gRPC nội bộ trong `internal_net`, không mở route công khai qua Gateway. Frontend không thể và không được gọi trực tiếp Recommendation.

## 3. Endpoints

| Method | Path | Status | Before → After |
|---|---|---|---|
| GET | `/healthz` | new | Endpoint kiểm tra liveness của API Gateway, trả về `200 OK` (text/plain: `ok\n`). |
| * | `/api/identity/*` | changed | Reverse proxy tới `identity:8080/*` (bỏ tiền tố `/api/identity`). |
| * | `/api/diet/*` | changed | Reverse proxy tới `diet:8080/*` (bỏ tiền tố `/api/diet`). |
| * | `/api/progress/*` | changed | Reverse proxy tới `progress:8080/*` (bỏ tiền tố `/api/progress`). |
| GET | `/swagger/` | new | Cổng giao diện Swagger UI tổng hợp cho các dịch vụ (chỉ bật ở môi trường Development). |

## 4. Events

Tính năng hạ tầng này chỉ tiêu thụ các sự kiện nội bộ qua RabbitMQ (`UserRegisteredEvent`, `PasswordResetRequestedEvent`) cho Background Worker gửi email. Không có event nào phát trực tiếp ra client frontend qua WebSocket/SSE trong phạm vi tính năng này.

- Tổng số client-facing events: 0.

## 5. Client-facing DTOs

Gateway chuẩn hóa phản hồi lỗi ở tầng biên mạng bằng cấu trúc chuẩn RFC 7807 Problem Details với `Content-Type: application/problem+json; charset=utf-8`:

| DTO | Field | Type | Status | Mô tả |
|---|---|---|---|---|
| `ProblemDetails` | `type` | `string` | new | URI định danh loại lỗi theo chuẩn RFC 7807 |
| `ProblemDetails` | `title` | `string` | new | Tiêu đề tóm tắt lỗi bằng tiếng Việt UTF-8 |
| `ProblemDetails` | `status` | `integer` | new | HTTP status code tương ứng (404 hoặc 502) |
| `ProblemDetails` | `detail` | `string` | new | Mô tả chi tiết nguyên nhân lỗi bằng tiếng Việt UTF-8 |

## 6. Enums

Không phát sinh Enum mới cho client surface trong tính năng này (count: 0).

## 7. Errors and compatibility

### 7.1 Lỗi 404 Route không tồn tại trên Gateway

Khi client gửi request tới route không khớp bất kỳ location nào trên Gateway:

- HTTP Status: `404 Not Found`
- Content-Type: `application/problem+json; charset=utf-8`
- Body:
```json
{
  "type": "https://longevia.com/errors/not-found",
  "title": "Không tìm thấy",
  "status": 404,
  "detail": "Đường dẫn API không tồn tại hoặc phương thức không được hỗ trợ."
}
```

*Lưu ý*: Nginx không bật `proxy_intercept_errors`, do đó nếu bản thân backend service trả về 404 (ví dụ entity không tồn tại), body 404 của backend service sẽ được chuyển nguyên vẹn về client chứ không bị ghi đè bởi lỗi này.

### 7.2 Lỗi 502 / 503 / 504 Dịch vụ backend không khả dụng hoặc Timeout

Khi upstream microservice đang khởi động, crash hoặc không thể kết nối qua mạng nội bộ:

- HTTP Status: `502 Bad Gateway`
- Content-Type: `application/problem+json; charset=utf-8`
- Body:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Dịch vụ không khả dụng",
  "status": 502,
  "detail": "Không thể kết nối đến dịch vụ phía sau hoặc dịch vụ đang khởi động."
}
```

## 8. Sync checklist

- [x] Every `json:"…"` tag the feature's DTOs emit appears above (count: 4: `type`, `title`, `status`, `detail`)
- [x] Every value of every enum compared against its source constant block, in both directions (count: 0 - tính năng hạ tầng không định nghĩa enum)
- [x] Every route compared after normalizing path params (count: 5 routes: `/healthz`, `/swagger/`, `/api/identity/*`, `/api/diet/*`, `/api/progress/*`)
- [x] Every HTTP status claimed above is one the handler can actually return (count: 3: `200`, `404`, `502`)
