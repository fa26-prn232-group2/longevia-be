# Hướng dẫn tích hợp Backend — Progress PR #13

Trạng thái: **PENDING_EXTERNAL_INTEGRATION**. Không READY_FOR_BACKEND toàn feature.
Không công bố lịch sử streak production đầy đủ chỉ vì test fixture xanh.

## Identity height — Q02 không đổi

Hướng Progress đọc height decimal từ Identity qua gRPC đã được Bảo/Nguyên thống nhất.
Canonical height-read RPC và runtime provider chưa có trên checkout hiện tại.
[BodyProfileProvider](../../src/Services/Progress/Progress.Infrastructure/Adapters/Grpc/BodyProfileProvider.cs)
trả ServiceUnavailable; không thay bằng height giả, HTTP hoặc đọc identity_db.
POST metric phân biệt 503 unavailable, 404 profile missing và 422 height missing;
snapshot HeightUsedCm/BMI/category bất biến. Blocker owner: Nguyên.

## Diet meal activity — JSON event v1 và projection

Canonical event: [MealLoggedEvent](../../src/Shared/Events/MealLoggedEvent.cs),
EventId=MealLogId, UserId, BusinessDate, CreatedAt, OccurredOn; SchemaVersion=1 constant.
Topology topic `longevia.events`, key `diet.meal.logged`, durable queue
`progress.meal-activity`. Không sửa Diet/Worker queues.

Consumer chạy transaction progress_db: advisory lock theo MealLogId, inbox và
contribution atomic. Canonical IDs không khớp bị reject. Duplicate phải khớp owner,
business date và CreatedAt (độ chính xác PostgreSQL microsecond); không overwrite.
Inbox association sai hoặc nhiều association bị reject. Replay hợp lệ có thể phục
hồi một nửa cặp inbox/contribution bị thiếu trong cùng transaction. Unique violation
chỉ cân nhắc re-read với SQLSTATE 23505 và đúng dedup constraint; lỗi khác không
được coi AlreadyProcessed. ProcessedAt lấy injected TimeProvider ở lúc xử lý.

Retry hữu hạn 2s/5s/10s. Consumer exception hết retry đi vào queue MassTransit
`progress.meal-activity_error`, không phải tự động vào broker DLQ.
Binding `progress.meal-activity.dlx`/`.dlq` dành cho broker dead-letter semantics;
không dùng cấu hình BindDeadLetterQueue làm bằng chứng consumer fault routing.
Broker test kiểm tra error message thực, không dựa tên cấu hình.

RabbitMQ host/user/password bắt buộc cấu hình tường minh, port hợp lệ (mặc định
protocol 5672 khi không cấp). Không default localhost/guest/password. In-memory
transport chỉ dùng khi operator/test đặt explicit Messaging:UseInMemory=true;
suite RabbitMQ không bật cờ này.

Q01 nội bộ Progress **APPROVED bởi Bảo cho MVP UTC**. Diet BusinessDate luôn giữ
nguyên, kể cả backdate/replay; không đổi theo timestamp. Frontend tự chọn current
meal day theo UTC cần Huy xác nhận; client chọn ngày lịch sử giữ ngày được chọn.
Việt Nam 00:30 có thể vẫn là ngày UTC trước đó. **Huy acknowledgment PENDING**.

## Snapshot, replay và bằng chứng

Snapshot canonical là JSON v1 (schemaVersion/snapshotId/capturedAtUtc/entries/
recordCount/entriesSha256), không phải CSV/public API. Contract và verifier do Diet
sở hữu trên PR #12 chưa merge; corrective không copy/đổi shared contract.

[Hướng dẫn reconciliation](corrective-reconciliation.md) mô tả kiểm tra raw checksum,
batch SQL read-only RepeatableRead, exit codes và harness isolated PR #12.
Reconciliation PASS chỉ chứng minh tập entries của file có projection/inbox đúng;
không chứng minh producer outbox hoặc broker đã drain. Extra live records ngoài
snapshot được giữ nguyên, không buộc total count DB bằng recordCount.

Transport suite dùng synthetic publisher + actual consumer/PG/RabbitMQ. Harness
ngoài solution dùng actual Diet publisher/outbox/replay/exporter ở SHA PR #12 để
chứng minh interoperability riêng; kết quả và SHA ở [verification](verification.md).
Không nhập source Diet vào production Progress, không merge PR #12.

Production rollout vẫn cần bind queue trước nâng writer/replay, drain/error checks,
snapshot operator thật và reconciliation dữ liệu triển khai. Chưa có public DTO
completeness được approve nên chưa tuyên bố historical streak READY, không thêm field.

## Latest weight — Q10 không đổi

[progress.proto](../../contracts/progress/v1/progress.proto) và
[proposal](../../contracts/progress/v1/README.md) vẫn là proposal chờ Bảo/Huy.
Query scaffolding as-of giữ nguyên: LoggedAt <= asOfUtc, LoggedAt DESC/Id DESC,
presence phân biệt no record và unavailable, scale weight_kg_x100.
Corrective không mở runtime RPC hoặc thay hợp đồng Weight/BMR/TDEE của Diet.
