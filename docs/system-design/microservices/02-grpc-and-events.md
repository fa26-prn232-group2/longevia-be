# gRPC & Event

## gRPC (đồng bộ)

- Dùng cho request/response **giữa các service**, ví dụ `Diet → Recommendation`.
- Contract: file `.proto` trong `contracts/`; `.proto` là nguồn chân lý.
- Không route gRPC qua Nginx; gọi trực tiếp service đích trong mạng nội bộ.
- Mọi call MUST có **deadline/timeout**; xử lý lỗi transport tường minh.
- Không để lộ entity domain trong message proto (xem contract purity).

## Bảo mật gRPC nội bộ

- **Không xác thực caller.** Quyết định và điều kiện kèm theo:
  [ADR 001](../../decisions/001-authn-authz-architecture.md) §2.2.
- Lý do: các service là **container riêng trong cùng compose project**, cùng
  trust domain — không phải vì chung một process.
- Ràng buộc bắt buộc:
  - **Tách network.** `recommendation` nằm ở internal network không chứa
    nginx/postgres/rabbitmq, để container `swagger-ui` không gọi được
    `recommendation:50051`. Xem `deployments/docker-compose.yml` (network `edge` /
    `internal_net` / `data_net`).
  - **Không** expose cổng service nội bộ ra host.
  - Recommendation **không** được route qua Nginx (xem `constitution.md`).
- Non-goals: không mTLS, không shared-secret header, không phân quyền theo service.
- Phải đánh giá lại nếu có service chạy ngoài compose, môi trường nhiều host /
  Kubernetes, hoặc cần audit "service nào gọi service nào".

## Event (bất đồng bộ)

- Event đi qua **RabbitMQ** với topology tường minh (exchange/queue/routing key).
- Producer publish qua **transactional outbox** trong cùng transaction ghi dữ liệu.
- Tên event ở thì quá khứ (`UserCreated`, `ProgressLogged`).
- Consumer idempotent; bỏ qua an toàn field lạ.

## Khi nào dùng cái nào

| Nhu cầu | Chọn |
| --- | --- |
| Cần trả lời ngay trong cùng luồng | gRPC |
| Bên kia xử lý nền, không cần chờ | Event |
| Nhiều consumer cùng quan tâm | Event (fanout/topic) |
| Truy vấn dữ liệu của service khác | gRPC, qua contract |

## Versioning

- Thêm field optional: tương thích ngược, giữ version.
- Đổi/bỏ field đang dùng: version mới + kế hoạch chuyển đổi.

## Liên quan

- [../event-driven-design.md](../event-driven-design.md)
- [../contract-purity-pattern.md](../contract-purity-pattern.md)
- [../../decisions/001-authn-authz-architecture.md](../../decisions/001-authn-authz-architecture.md)
