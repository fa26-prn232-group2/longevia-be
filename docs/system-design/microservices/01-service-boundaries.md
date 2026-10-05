# Ranh giới service

Mỗi service sở hữu dữ liệu và nghiệp vụ của mình. Không service nào chạm dữ liệu
của service khác.

## Danh sách service

| Service | Trách nhiệm | Database | Expose |
| --- | --- | --- | --- |
| Identity | Đăng ký/đăng nhập, hồ sơ, phát hành JWT | `identity_db` | HTTP qua Nginx |
| Diet | Kế hoạch ăn uống / dinh dưỡng | `diet_db` | HTTP qua Nginx |
| Progress | Theo dõi tiến độ người dùng | `progress_db` | HTTP qua Nginx |
| Recommendation | Sinh gợi ý qua Google AI | không (stateless) | gRPC nội bộ |
| Background Worker | Consume event, gửi email | không | không |

## Quyền gọi (ai gọi ai)

- Client → **Nginx** → Identity / Diet / Progress (HTTP).
- **Diet → Recommendation** qua **gRPC** (nội bộ).
- Bất kỳ service → **RabbitMQ** (publish event).
- **Background Worker** ← RabbitMQ (consume) → Brevo.

Các chiều gọi khác MUST NOT tồn tại nếu chưa có contract và ADR.

## Quy tắc bất biến

- Không truy cập chéo database (kể cả chỉ đọc).
- Không import code nội bộ của service khác.
- Chỉ service sở hữu schema được đổi schema đó.
- Recommendation MUST NOT được expose REST / route qua Nginx.
- Tách/gộp service là quyết định cấp hiến pháp.

## Kiểm tra nhanh

- Có `DbContext` nào trỏ DB của service khác không? → vi phạm.
- Có `using` namespace nội bộ của service khác không? → vi phạm.
- Recommendation có endpoint HTTP công khai không? → vi phạm.
