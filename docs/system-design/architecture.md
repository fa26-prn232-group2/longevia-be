# Kiến trúc tổng thể

Sơ đồ nguồn: [`diagrams/png/c4-model-lv2.png`](diagrams/png/c4-model-lv2.png).

## Bối cảnh (C4 Level 1)

Người dùng dùng **Web App** và **Mobile App**. Hai client nói chuyện với hệ thống
**Longevia** qua **Nginx** bằng HTTP/HTTPS. Hệ thống tích hợp ba dịch vụ ngoài:
**Cloudinary** (lưu media), **Google AI** (sinh gợi ý), **Brevo** (gửi email).

## Containers (C4 Level 2)

| Container | Vai trò | Sở hữu dữ liệu |
| --- | --- | --- |
| **Nginx** | API Gateway, điểm vào công khai duy nhất | — |
| **Identity Service** | Đăng ký/đăng nhập, hồ sơ người dùng, JWT | `identity_db` |
| **Diet Service** | Kế hoạch ăn uống / dinh dưỡng | `diet_db` |
| **Progress Service** | Theo dõi tiến độ | `progress_db` |
| **Recommendation Service** | Sinh gợi ý qua Google AI, RAG trên tri thức sách | `recommendation_db` (knowledge store, không chứa dữ liệu người dùng) |
| **RabbitMQ** | Message broker cho sự kiện bất đồng bộ | — |
| **Background Worker** | Consume event, gửi email qua Brevo | — |

## Luồng giao tiếp

- **Client → hệ thống**: HTTP/HTTPS qua Nginx. Chỉ **Identity, Diet, Progress**
  được expose, theo prefix `/api/<service>/**`.
- **Đồng bộ giữa service**: **gRPC**, ví dụ `Diet → Recommendation`. gRPC là lưu
  lượng nội bộ, **không** đi qua Nginx.
- **Bất đồng bộ**: service publish event lên **RabbitMQ**; **Background Worker**
  consume rồi gọi Brevo. Chi tiết: [event-driven-design.md](event-driven-design.md).
- **Tri thức (offline)**: corpus tri thức trong
  [`docs/reference/`](../reference/che-do-an-truong-tho/README.md) được pipeline
  `scripts/book-kb` chunk → embed → nạp vào `recommendation_db`. Pipeline chạy
  ngoài runtime, không phải traffic của client. Chi tiết:
  [ADR 002](../decisions/002-recommendation-knowledge-base.md).

## Quyết định nền tảng

- Microservices, **database per service**; mỗi service chỉ chạm DB của mình.
- Không truy cập chéo DB, không import code nội bộ giữa các service.
- Giao tiếp liên service qua **contract** (gRPC `.proto`, event schema).
- Schema do **EF Core Code First** quản lý; xem [../development/migration.md](../development/migration.md).

## Ràng buộc & phi chức năng

- Service stateless có thể scale ngang. Recommendation giữ state chỉ ở
  `recommendation_db` (knowledge store, read-mostly); mọi dữ liệu người dùng
  vẫn đi qua gRPC request/response và không được lưu lại.
- Cấu hình/secret qua biến môi trường.
- Local và production dùng cùng image + Docker Compose.

## Liên quan

- [design-pattern.md](design-pattern.md) — cấu trúc bên trong mỗi service.
- [diet-service.md](diet-service.md) — dữ liệu, API và phạm vi Diet độc lập.
- [microservices/01-service-boundaries.md](microservices/01-service-boundaries.md) — ranh giới và quyền gọi.
- [contract-purity-pattern.md](contract-purity-pattern.md) — hợp đồng liên service.
- [event-driven-design.md](event-driven-design.md) — event & RabbitMQ.
