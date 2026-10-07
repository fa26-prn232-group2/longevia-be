# Quy chuẩn API

## Gateway & route

- Client chỉ gọi qua **Nginx**: `http://<host>:8080/api/<service>/**`.
- Prefix được Nginx **cắt bỏ** trước khi tới service; service định nghĩa route của
  mình không kèm prefix.
- Chỉ expose service client-facing: **identity, diet, progress**.
  **Recommendation là gRPC nội bộ**, không có API công khai.

## Phiên bản

- Đặt version trong đường dẫn khi cần phá vỡ tương thích: `/api/identity/v2/...`.
- Thay đổi tương thích ngược không đổi version.

## Xác thực

- JWT bearer ở header `Authorization: Bearer <token>`.
- Token ký bằng **ES256**; chỉ Identity giữ private key, Diet/Progress chỉ giữ public
  key. Xem [ADR 001](../decisions/001-authn-authz-architecture.md).
- Service client-facing validate token tại chỗ bằng public key lấy từ biến môi
  trường (`kid` tra key trong tập key được cấu hình); không gọi Identity mỗi request.
- Endpoint công khai (đăng ký/đăng nhập) nằm ngoài yêu cầu auth.

## Định dạng lỗi

Dùng Problem Details (`application/problem+json`):

```json
{
  "type": "https://longevia/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "Email is required",
  "traceId": "..."
}
```

- Không trả stack trace ra ngoài.
- Mã lỗi nghiệp vụ dùng `type` ổn định, không phải chuỗi tự do.

## Mã trạng thái

| Mã | Dùng khi |
| --- | --- |
| 200 / 201 | Thành công / tạo mới |
| 204 | Thành công, không body |
| 400 | Input sai |
| 401 / 403 | Chưa xác thực / không đủ quyền |
| 404 | Không tìm thấy |
| 409 | Xung đột trạng thái |
| 422 | Vi phạm nghiệp vụ |

## Phân trang & lọc (khi có danh sách)

- Query: `page`, `pageSize` (có giới hạn max); trả kèm `total`.
- Lọc/sắp xếp qua query param tường minh.

## Swagger / OpenAPI

- Dùng **Swashbuckle.AspNetCore**; mỗi service client-facing expose `/swagger` và
  `/swagger/v1/swagger.json`.
- **Chỉ bật ở Development**; production không expose Swagger.
- UI gộp cả 3 service ở gateway: `http://localhost:8080/swagger`.
- Hỗ trợ nút **Authorize** Bearer JWT để test endpoint bảo vệ.
- Recommendation (gRPC-only) không có Swagger; test qua gRPC.
- Chi tiết và snippet cấu hình: [../development/api-testing.md](../development/api-testing.md).

## Quy ước khác

- JSON camelCase.
- Thời gian ISO 8601 UTC.
- Id công khai không lộ khóa nội bộ tuần tự nếu cần che giấu.
- Không trả entity domain trực tiếp; dùng DTO response.
