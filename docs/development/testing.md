# Testing

Framework: **xUnit**. Mọi thay đổi hành vi MUST có test tương ứng.

## Tầng test

| Tầng | Phạm vi | Công cụ |
| --- | --- | --- |
| Unit | Domain/use case, không I/O | xUnit, mock interface |
| Integration | API/DB/messaging thật | `WebApplicationFactory` + Testcontainers (Postgres, RabbitMQ) |
| Contract | gRPC `.proto`, event schema | test consumer/producer với contract |
| End-to-end | Một đường đầy đủ qua hạ tầng thật | Testcontainers / compose |

- Unit test KHÔNG chạm DB, mạng, hay message broker.
- Test xuyên DB/broker dùng **container thật** (Testcontainers), không mock tầng
  persistence.

## Quy ước

- Tên test: `Method_Scenario_ExpectedResult`.
- Arrange–Act–Assert rõ ràng; mỗi test một hành vi.
- Dữ liệu test tạo trong test, không phụ thuộc dữ liệu có sẵn.
- Không assert "không lỗi" — assert **giá trị cụ thể**.
- Không để test phụ thuộc thứ tự chạy.

## Negative control (bắt buộc cho test bắt bug)

Với test viết để bắt một lỗi cụ thể:

1. Làm hỏng code theo đúng cách lỗi từng xảy ra.
2. Xác nhận test **đỏ** và đỏ vì đúng lý do.
3. Khôi phục code.
4. Xác nhận test **xanh**.

Báo cáo cả hai màu. "Test passes" thiếu nửa đỏ là một claim chưa đủ.

## Đường end-to-end

Mỗi feature MUST có ít nhất một đường end-to-end thật chạy qua hạ tầng thật
(DB thật, broker thật) và assert giá trị cụ thể. Chạy một lần gần cuối feature.

## Gate

- `make test PROJECTS=<path>` — test scoped theo project (trong phase).
- `make check` — toàn bộ test ở chế độ Release (close-out, trước push).
- Gate đỏ là hard stop: không bỏ test, không nới gate để có commit.
