# Backend Integration Guide — Identity email verification and password reset

## 1. Summary

- Feature/spec: `specs/bao-nguyen-001-identity-service/spec.md`
- Requirement: `docs/requirements/prd.md` §2.1
- Handoff status: **IN_PROGRESS** (Phase 3–4 email integration đã verified; backend guide chưa READY do profile event publication hiện hữu cần xử lý tại T026).
- Canonical contracts: `src/Shared/Events/UserRegisteredEvent.cs`, `src/Shared/Events/PasswordResetRequestedEvent.cs`, and `contracts/events/v1/README.md` (v1)
- Counts: **0 RPC · 0 messages gRPC · 0 gRPC fields · 2 canonical email events · 9 canonical event fields · 0 enum values**. Ngoài scope đếm contract: Identity hiện còn publish `UserProfileUpdatedIntegrationEvent` (2 fields, không có schema version/consumer); T026 đang theo dõi việc xử lý publication này.

Guide này ghi phần producer topology và evidence của Identity; schema event canonical giữ nguyên.

## 2. Consumers and transport

| Producer | Consumer | Transport | Canonical contract | Sync/async | Network boundary |
|---|---|---|---|---|---|
| Identity | Worker `worker.email` | RabbitMQ event | `UserRegisteredEvent` v1 | async | RabbitMQ trên `data_net` |
| Identity | Worker `worker.email` | RabbitMQ event | `PasswordResetRequestedEvent` v1 | async | RabbitMQ trên `data_net` |

## 3. gRPC

- N/A: Phase 3–4 không thay đổi gRPC surface.

## 4. Events

| Exchange | Routing key | Queue | Event | Producer | Consumer | Status |
|---|---|---|---|---|---|---|
| `longevia.events` (`topic`) | `identity.user.registered` | `worker.email` | `UserRegisteredEvent` v1 | Identity | Worker `UserRegisteredConsumer` | producer topology aligned |
| `longevia.events` (`topic`) | `identity.user.password-reset-requested` | `worker.email` | `PasswordResetRequestedEvent` v1 | Identity | Worker `PasswordResetRequestedConsumer` | producer topology aligned |

- Payload canonical: `src/Shared/Events/UserRegisteredEvent.cs`; các field: `UserId`, `Email`, `FullName`, `VerificationToken`, `OccurredOn` (5). Không đổi payload hoặc schema version.
- Payload reset canonical: `src/Shared/Events/PasswordResetRequestedEvent.cs`; các field: `Email`, `ResetToken`, `ExpiresAt`, `OccurredOn` (4). Không đổi payload hoặc schema version. ResetToken chỉ lưu hash ở `identity_db`; bản rõ chỉ nằm trong outbox/email để Worker gửi mã một lần.
- Ordering: không bảo đảm thứ tự toàn cục; `OccurredOn` là thời điểm thực hiện nghiệp vụ tương ứng (đăng ký hoặc yêu cầu reset).
- Idempotency: Worker dedup email reset theo schema version và SHA-256 reset token; TTL 7 ngày. Cùng token delivery retry không gửi trùng; hai reset request khác token không va nhau dù email và `OccurredOn` giống nhau. Nếu SMTP lỗi, consumer giải phóng claim trước khi ném lỗi để MassTransit retry có thể gửi lại.
- Outbox: Identity EF Core bus outbox trong `identity_db`; event được ghi cùng transaction với user đăng ký hoặc reset token.
- Retry/DLQ: theo Worker: 3 retry với interval 2s/5s/10s, sau đó dead-letter vào `worker.email.dlq`.
- Reset email: Worker gửi mã OTP 64 ký tự hex dùng một lần, hết hạn sau 15 phút; không tạo link GET gọi API reset.
- Versioning/compatibility: v1 giữ nguyên; producer và consumer dùng chung event contract.
- Delta: Identity trước đây dùng topology MassTransit mặc định nên Worker không nhận được event. Producer hiện khai báo exchange topic và routing key đúng với Worker binding.

- Existing profile publication (outside Phase 3–4 email counts): `UpdateProfileUseCase` currently emits `UserProfileUpdatedIntegrationEvent` (2 fields), which lacks a shared versioned contract and has no known consumer. This is tracked in T026 and prevents `READY_FOR_BACKEND` until it is removed or contractually completed.

## 5. Internal HTTP

- N/A: không có HTTP trực tiếp giữa các service.

## 6. Compatibility and rollout

- Schema: không đổi; không cần migration contract hoặc rollout hai phiên bản.
- Before → after: Identity thêm producer cho reset event v1 trong transactional outbox; topology khớp exchange/routing key Worker, email reset dùng OTP và idempotency phân biệt reset token.
- Rollout: deploy Worker có template OTP và dedup theo reset-token hash cùng hoặc trước Identity producer; contract v1 và queue hiện hành tương thích.

## 7. Test evidence

- `dotnet test src/Services/Identity/tests/Identity.IntegrationTests/Identity.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RegistrationEmailE2ETests` — passed; PostgreSQL, RabbitMQ, Redis, Worker và Mailpit chạy bằng Testcontainers. Test xác nhận email nhận ở Mailpit, lấy link, verify và login thành công.
- `AuthAndUserIntegrationTests.Registration_ShouldPersistUserAndOutboxMessageInSameTransaction` — passed; user và outbox cùng transaction, rollback loại bỏ cả hai.
- Phase 4 E2E `RegistrationEmailE2ETests.Register_ShouldSendVerificationEmailThroughRabbitWorkerAndMailpit_ThenVerifyAccount` — passed với PostgreSQL + RabbitMQ + Redis + Worker + Mailpit; xác nhận OTP/hash/expiry/replacement, concurrent reset single-winner, replay rejection, session revoke và login bằng mật khẩu mới. Worker Unit xác nhận SMTP lỗi rồi retry gửi được; negative control giữ claim sau lỗi làm test fail vì không gửi email. Negative control bỏ predicate consumed làm test fail vì hai request cùng trả 204; sau restore test passed.

## 8. Sync checklist

- [x] RPC / gRPC messages / fields: 0 (phase không đổi gRPC).
- [x] Event schema: 2 event, 9 fields, 0 enum values; đối chiếu record và v1 index.
- [x] Event topology: 1 exchange, 2 routing keys, 1 queue, 2 producer/consumer pairs; đối chiếu Identity config với Worker binding.
- [x] Transactional outbox rollback test cho reset và E2E chạy với container thật; rollback không để lại user reset token hoặc event outbox.
- [x] Idempotency/retry/DLQ được đối chiếu với Worker consumer và unit test.
- [x] Phase 4 E2E evidence chạy thực tế với PostgreSQL, RabbitMQ, Worker và Mailpit.
