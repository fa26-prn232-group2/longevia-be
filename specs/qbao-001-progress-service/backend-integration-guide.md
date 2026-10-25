# Hướng dẫn tích hợp Backend — Progress PR #13

Trạng thái: **PENDING_EXTERNAL_INTEGRATION**. Không READY_FOR_BACKEND toàn feature.
Không công bố lịch sử streak production đầy đủ chỉ vì test fixture xanh.

## Q02 hiện hành — Identity Height gRPC (2026-10-26)

PR #14 đã merge: source 72555e139e31e94bc014be4ada97532cd486974c,
main merge c679b6f174a3cfcdfc0097b03ee81b3729bb72e2. Nhánh Progress merge main
không rewrite 14 commit cũ; Rabbit/inbox/reconciliation và Q01 UTC giữ nguyên.

**Q02 FUNCTIONAL INTEGRATION: DONE / VERIFIED** — client thật đã chạy với actual
Identity process, HTTP/2 và hai PostgreSQL riêng. **Authenticated service-to-service
trust: PENDING; secure production readiness: BLOCKED**. Đây không phải service
flow đã authenticated chỉ vì frontend JWT pass.

Canonical: longevia.identity.v1.IdentityService/GetUserHeight, C# namespace
Longevia.Identity.Contracts.V1. Request user_id field 1 (GUID); response optional
height_cm_x100 field 1 (int32 cm × 100). Một RPC, hai message, hai field; không thêm
GetBodyProfile hoặc wire DTO khác. HasHeightCmX100=false khác present zero.
17525 / 100m = 175.25 cm, validate 50.00–250.00, không integer division.

Progress.Infrastructure codegen Client từ canonical proto bằng Grpc.Tools;
Grpc.Net.Client và singleton channel/client do DI dispose, không reference
Identity implementation. IdentityGrpc:Address bắt buộc (Compose http://identity:50051);
DeadlineMilliseconds mặc định 2000, cho phép 50–10000. Không application retry;
deadline tổng mỗi call hữu hạn; truyền incoming cancellation tới HTTP/2.

| Kết quả Identity | HTTP Progress | Code / xử lý |
| --- | --- | --- |
| Height hợp lệ | 201 | Tính/lưu BMI snapshot hiện hữu |
| Optional unset | 422 | progress.height_unavailable |
| NOT_FOUND | 404 | progress.profile_not_found |
| UNAVAILABLE / DEADLINE_EXCEEDED | 503 | progress.identity_unavailable |
| Caller cancellation | Hủy request | Không tiếp tục ghi |
| INVALID_ARGUMENT / INTERNAL / auth fault / returned height sai | 502 | progress.identity_integration_failure |

ProfileHeightResult.GrpcStatus và structured log giữ status gRPC nội bộ, không đưa
URL, RpcException detail, credentials hoặc profile data vào Problem Details.

### Bảo mật và follow-up

ADR 001 §2.2 và microservices/02-grpc-and-events hiện chấp nhận Compose network trust:
không caller auth, không mTLS/token service. Shared ES256 JWT là frontend user
authentication, không service identity. Identity Program.MapGrpcService và service
không RequireAuthorization; test gọi không credentials thành công xác nhận gap.
Network isolation là defense-in-depth, không authenticated identity.

Theo yêu cầu Bảo trong lượt này, secure readiness vẫn BLOCKED. Nguyên và Bảo cần
ghi quyết định trust model cho Height: triển khai service identity/token hoặc mTLS
với authorization provider + contract cấp/rotate/revoke credential và negative tests,
hoặc phê duyệt tường minh mô hình network-trust hiện tại cho phạm vi MVP. Không có
phê duyệt chung mới được suy diễn; không forward user JWT làm service credential.
Progress không sửa Identity hoặc thêm API key giả.

17 gRPC scenarios và 9 options unit cases đã pass ở scoped run. Success tests dùng
Identity entry point/repository/migrations thật; seed/update qua REST của chính
Identity trong fixture, không DB/source import chéo. Controlled server chỉ cho
unset, malformed response, fault, deadline/cancellation. Full gates/Docker/CI mới
được báo ở verification.md và PR #13, không dùng lại số 733 của commit cũ.
Q10 Weight và Huy Q01 acknowledgment/Diet PR #12 giữ trạng thái riêng.
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
