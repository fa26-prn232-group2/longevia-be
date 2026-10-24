# Progress Service — foundation Phase 2

Progress dùng bốn layer Domain, Application, Infrastructure và Api, cộng hai
project xUnit. Domain độc lập framework; Application chỉ reference Domain;
Infrastructure triển khai persistence; Api là composition root, tái sử dụng Shared.
Chưa có entity, business endpoint, RPC/event, broker client hoặc adapter liên service.

## Persistence và khởi động

ConnectionStrings:Progress phải trỏ `progress_db`, role `progress_user` do init script
deployment provision. Cấu hình thiếu/sai hoặc DB không kết nối được làm startup thất
bại trước nhận traffic. Không tự tạo DB thiếu. DbContext chưa entity nên chưa tạo
migration rỗng/bảng giả/EnsureCreated. Khi assembly có migration, initializer gọi
MigrateAsync trước phục vụ request; migration đầu tiên sinh bằng EF ở phase có
schema được duyệt. Cancellation truyền tới mở kết nối/migration, scope được dispose.

## Auth và operational surface

Shared xác thực ES256 với public keys. Progress-local guard yêu cầu SPKI P-256,
issuer/audience/key id hợp lệ, previous key/id đi cặp và distinct, không nhận private
key cấu hình, reject kid thiếu và subject thiếu/không phải Guid/Guid rỗng.
Q08 về đồng thuận auth/ownership liên service vẫn PENDING Bảo + Nguyên; không sửa
Shared/Identity, không coi local tests là approval. Chưa có resource business để
nghiệm thu ownership 404. Application dùng TimeProvider, không đọc HttpContext.

- `/alive`: liveness 200 JSON status=ok, độc lập DB.
- `/health`: readiness 200 status=ok khi kết nối PostgreSQL được; 503 khi DB lỗi.
- `/metrics`: Prometheus endpoint từ Shared AddObservability("ProgressService").
- Swagger chỉ Development, server `/api/progress`, JWT Bearer và asset URL tương đối.

Problem Details/exception handler tập trung, validation dùng Shared filter và
FluentValidation; traceId có trên lỗi, không trả lỗi kỹ thuật/stack cho client.

Compose `progress` chỉ join edge + data_net, không publish host port/private key.
Chờ PostgreSQL service_healthy, healthcheck gọi binary probe tới /health (runtime
chiseled không cần curl/shell). Nginx reuse route prefix stripping hiện có;
Prometheus static target progress:8080/metrics. Bảo được developer cấp quyền
integration Progress vào shared infrastructure; không cần Duy duyệt riêng.
Kiểm chứng, giới hạn và trạng thái task: ../../specs/qbao-001-progress-service/verification.md.
