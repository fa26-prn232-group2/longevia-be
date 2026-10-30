# Ranh giới service

Mỗi service sở hữu dữ liệu và nghiệp vụ của mình. Không service nào chạm dữ liệu
của service khác.

## Danh sách service

| Service | Trách nhiệm | Database | Expose |
| --- | --- | --- | --- |
| Identity | Đăng ký/đăng nhập, hồ sơ, phát hành JWT | `identity_db` | HTTP qua Nginx, gRPC nội bộ |
| Diet | Kế hoạch ăn uống / dinh dưỡng | `diet_db` | HTTP qua Nginx |
| Progress | Theo dõi tiến độ người dùng | `progress_db` | HTTP qua Nginx, gRPC nội bộ |
| Recommendation | Sinh gợi ý qua Google AI, RAG trên tri thức sách | `recommendation_db` (knowledge store) | gRPC nội bộ |
| Background Worker | Consume event, gửi email | không | không |

## Quyền gọi (ai gọi ai)

- Client → **Nginx** → Identity / Diet / Progress (HTTP).
- **Diet → Recommendation** qua **gRPC** (nội bộ).
- **Progress → Identity** qua **gRPC** (nội bộ, lấy chiều cao `GetUserHeight`).
- **Diet → Progress** qua **gRPC** (nội bộ, lấy cân nặng gần nhất `GetLatestWeight` — xem [ADR 003](../../decisions/003-diet-to-progress-weight-grpc.md)).
- Bất kỳ service → **RabbitMQ** (publish event, ví dụ Diet `diet.meal.logged` → Progress `progress.meal-activity`).
- **Background Worker** ← RabbitMQ (consume) → Brevo.

Các chiều gọi khác MUST NOT tồn tại nếu chưa có contract và ADR.

## Knowledge store của Recommendation

`Recommendation` sở hữu `recommendation_db` để phục vụ RAG — xem
[ADR 002](../../decisions/002-recommendation-knowledge-base.md).

- DB này chỉ chứa **corpus tri thức đã nhúng** (chunks + embeddings của tài liệu
  tham chiếu), **MUST NOT** chứa dữ liệu người dùng.
- Dữ liệu người dùng đi vào qua gRPC request và trả về qua gRPC response; không
  lưu lại.
- Ingest corpus là pipeline **offline** (`scripts/book-kb`), không thuộc runtime
  service.
- Recommendation vẫn là service nội bộ: **MUST NOT** expose REST / route qua
  Nginx.

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
- `recommendation_db` có bảng/dữ liệu người dùng không? → vi phạm.
