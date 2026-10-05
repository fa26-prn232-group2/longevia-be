# gRPC & Event

## gRPC (đồng bộ)

- Dùng cho request/response **giữa các service**, ví dụ `Diet → Recommendation`.
- Contract: file `.proto` trong `contracts/`; `.proto` là nguồn chân lý.
- Không route gRPC qua Nginx; gọi trực tiếp service đích trong mạng nội bộ.
- Mọi call MUST có **deadline/timeout**; xử lý lỗi transport tường minh.
- Không để lộ entity domain trong message proto (xem contract purity).

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
