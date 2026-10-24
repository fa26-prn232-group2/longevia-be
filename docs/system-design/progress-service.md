# Progress Service — foundation và capability hiện hành

Progress dùng bốn layer Domain, Application, Infrastructure và Api, cộng hai
project xUnit. Domain độc lập framework; Application chỉ reference Domain;
Infrastructure triển khai persistence; Api là composition root, tái sử dụng Shared.
Hiện có Fasting, Water, Sleep, body metric snapshots/history, Dashboard và meal inbox/projection.
Height runtime RPC chưa có; Weight RPC vẫn proposal, không công bố feature hoàn chỉnh.

## Persistence và khởi động

ConnectionStrings:Progress phải trỏ `progress_db`, role `progress_user` do init script
deployment provision. Cấu hình thiếu/sai hoặc DB không kết nối được làm startup thất
bại trước nhận traffic. Không tự tạo DB thiếu. DbContext có năm migration Code First
hiện hữu; corrective không sửa migration cũ. Initializer chạy MigrateAsync trước
phục vụ request. Cancellation truyền tới mở kết nối/migration, scope được dispose.

## Auth và operational surface

Shared xác thực ES256 với public keys. Progress-local guard yêu cầu SPKI P-256,
issuer/audience/key id hợp lệ, previous key/id đi cặp và distinct, không nhận private
key cấu hình, reject kid thiếu và subject thiếu/không phải Guid/Guid rỗng.
Q08-A về chính sách xác thực JWT đã được THỐNG NHẤT (APPROVED) giữa Bảo và Nguyên:
chính sách strict JWT (ES256, kid hợp lệ, sub Guid khác empty, fail-start khi thiếu key hoặc
cấu hình nhầm private key) được bảo vệ; Progress giữ guard cục bộ hiện tại, Nguyên cập nhật Shared Auth
trong PR riêng của Identity. Q08-B về resource ownership và error policy (403 vs 404, Problem Details mapping)
tiếp tục PENDING cho các business resource endpoints (Phase 3–6); Phase 2 chưa có resource business
nên chưa nghiệm thu ownership 404 và không tuyên bố đã test.
Q02 về chiều cao cho BMI (Phase 5) đã thống nhất hướng gRPC read từ Identity sang Progress,
kiểu dữ liệu chiều cao là decimal (khớp numeric(5,2) của PR #10), snapshot BMI bất biến;
schema RPC trong identity.proto PENDING trước Phase 5, provider runtime chưa có trong Identity.
Application dùng TimeProvider, không đọc HttpContext. Không có role Technical Lead trong repo.

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

## Corrective PR #13

Q01 được Bảo approve cho Progress MVP: UTC calendar cho server-derived days,
TimeProvider cho current day, week Monday 00:00 UTC, history upper bound đầu ngày
kế tiếp exclusive. Sleep lấy UTC WokeAt, metric activity lấy UTC LoggedAt; duration
và overlap dùng full instants. Không rebucket dữ liệu/BMI snapshots đã lưu.
Diet BusinessDate tiêu thụ nguyên văn, không suy từ CreatedAt/OccurredOn/receive time.
Huy acknowledgment cho tự chọn current meal date theo UTC vẫn PENDING; UTC-only
không tương đương midnight local Việt Nam, không cần timezone từ Identity.

Consumer/inbox atomically persist trong progress_db, canonical EventId=MealLogId,
reject conflicting duplicate và repair partial pair chỉ từ valid replay. Rabbit
credentials phải explicit; retry hữu hạn đưa consumer fault vào MassTransit _error,
không giả định broker DLQ nhận mọi exception. Snapshot JSON v1 reconciliation CLI
là read-only RepeatableRead/batch500, integrity trước DB, exit0 chỉ khi khớp toàn bộ
entry. Không coi snapshot capturedAtUtc là cursor hoặc PASS là outbox drain.

Runtime tests và rollout limitations: ../../specs/qbao-001-progress-service/verification.md.
