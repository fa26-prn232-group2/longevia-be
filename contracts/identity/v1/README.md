# Identity contracts (v1)

gRPC contract do **Identity** sở hữu. Đây là nguồn chân lý cho giao tiếp đồng bộ
liên service (service khác gọi Identity qua gRPC).

- File: `identity.proto` — package `longevia.identity.v1`.
- Codegen: `Grpc.Tools` sinh client/server lúc build (khi project tham chiếu).
- Quy ước contract: không lộ entity/domain type; chỉ dữ liệu + method.
- `GetUserHeight` là RPC tương thích hiện có cho Progress; `GetUserProfile` cung
  cấp ngày sinh ISO (`YYYY-MM-DD`), giới tính sinh học và activity level theo các
  giá trị canonical của Identity, cùng chiều cao dạng `height_cm_x100`.
- `GetUserProfile` trả `NOT_FOUND` khi user hoặc profile không tồn tại,
  `INVALID_ARGUMENT` khi `user_id` không phải GUID khác rỗng, và `INTERNAL` khi
  dữ liệu profile đã lưu không hợp lệ. Caller phải đặt deadline và xử lý status
  gRPC tường minh.
- Xem [`docs/system-design/contract-purity-pattern.md`](../../../docs/system-design/contract-purity-pattern.md).

- `GetUserProfile` bổ sung optional `dietary_preference` (field 5) và repeated
  `allergies` (field 6). Field 5 có presence: absent là server cũ chưa cung cấp
  metadata, không được suy allergies rỗng nghĩa không dị ứng. Khi field 5 present,
  allergies rỗng là profile đã khai báo không có dị ứng. Diet gọi theo ADR 004.
