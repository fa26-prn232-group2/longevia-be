# System Design

Tài liệu thiết kế hệ thống của longevia-be.

## Cấu trúc

```
docs/system-design/
  diagrams/
    png/        # ảnh sơ đồ (.png/.jpg) — nơi dán ảnh kiến trúc
    drawio/     # file nguồn .drawio (tạo khi cần chỉnh sửa)
  README.md
```

## Quy ước đặt tên

| File | Nội dung |
| --- | --- |
| `c4-model-lv2.png` | Sơ đồ container (C4 level 2) — kiến trúc tổng thể |
| `architecture.png` | Sơ đồ kiến trúc tổng thể (nếu tách riêng khỏi C4) |
| `erd.png` | Sơ đồ quan hệ thực thể (database) |
| `event-backbone.png` | Luồng event/message bus giữa các service |

Nếu có nhiều phần, thêm hậu tố số: `erd-01.png`, `erd-02.png`, ...

## Containers (theo `c4-model-lv2.png`)

- **API Gateway**: Nginx
- **Client-facing services** (qua Nginx): Identity, Diet, Progress
- **Internal service**: Recommendation — gRPC-only, không expose REST, không có DB
- **Database**: mỗi service sở hữu DB riêng — Identity DB, Diet DB, Progress DB
- **Message Broker**: RabbitMQ (services publish event; Background Worker consume)
- **Background Worker** → Brevo (gửi email)
- **External**: Cloudinary (media storage), Google AI (AI provider)
- **Giao tiếp**: client → Nginx (HTTP); Nginx → Identity/Diet/Progress (HTTP); Diet → Recommendation (gRPC, nội bộ); service → service bất đồng bộ qua RabbitMQ

## Ghi chú

- Ưu tiên `.png`. Chấp nhận `.jpg`/`.jpeg`.
- Khi có bản chỉnh sửa được, đặt file `.drawio` tương ứng vào `diagrams/drawio/` cùng tên gốc.
- Mô tả bằng chữ (nếu cần) đặt thành file `.md` cùng cấp `docs/system-design/`, ví dụ `architecture.md`.
