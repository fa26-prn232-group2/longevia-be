# Contract Purity

Mọi phụ thuộc liên service đi qua **contract**, không qua source code. Service
khác chỉ được thấy những gì contract khai báo.

## Contract gồm những gì

| Loại | Định dạng | Vị trí |
| --- | --- | --- |
| Đồng bộ (request/response) | gRPC `.proto` | `contracts/` (hoặc thư mục Contracts dùng chung) |
| Bất đồng bộ (event) | schema event có version | `contracts/` |
| HTTP công khai (client) | route + DTO trong service client-facing | trong service |

## Nguyên tắc

- **Contract là nguồn chân lý**: đổi hành vi liên service phải đổi contract
  trước, rồi mới code.
- **Không rò rỉ nội bộ**: consumer MUST NOT phụ thuộc entity, bảng, hay cấu trúc
  module của producer. Contract chỉ mô tả dữ liệu trao đổi.
- **Kiểu tường minh**: DTO contract khai báo rõ; không dùng `object`/`dynamic`/
  dictionary mơ hồ.
- **Versioning**: thay đổi phá vỡ consumer phải lên version mới và có kế hoạch
  chuyển đổi; thay đổi tương thích ngược (thêm field optional) ưu tiên hơn.
- **Kiểm thử contract**: thay đổi `.proto`/event schema MUST có test cho consumer
  hiện có.

## gRPC

- `service` và `message` đặt tên theo nghiệp vụ, không theo bảng.
- Không nhét logic vào proto; proto chỉ là dữ liệu và phương thức.
- Client gọi MUST có deadline/timeout.

## Event

- Event là **sự kiện đã xảy ra** (quá khứ), tên ở thì quá khứ (`UserCreated`).
- Payload đủ để consumer xử lý mà không cần gọi ngược producer (tránh chatty).
- Không đưa dữ liệu nhạy cảm (secret, mật khẩu) vào event.

## Liên quan

- [event-driven-design.md](event-driven-design.md)
- [microservices/02-grpc-and-events.md](microservices/02-grpc-and-events.md)
- [../development/migration.md](../development/migration.md)
