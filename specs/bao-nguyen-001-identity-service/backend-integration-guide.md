# Backend Integration Guide — Identity email verification and password reset

## 1. Summary

- Feature/spec: `specs/bao-nguyen-001-identity-service/spec.md`
- Requirement: `docs/requirements/prd.md` §2.1
- Handoff status: **IN_PROGRESS** (contract/provider RPC chiều cao và kiểm thử PostgreSQL đang được bổ sung; profile event vẫn chưa có consumer được xác định).
- Canonical contracts: `src/Shared/Events/UserRegisteredEvent.cs`, `src/Shared/Events/PasswordResetRequestedEvent.cs`, `src/Shared/Events/UserProfileUpdatedEvent.cs`, and `contracts/events/v1/README.md` (v1)
- Counts đã đối chiếu source/contract: **1 RPC · 2 gRPC messages · 2 gRPC fields · 3 canonical events · 11 event fields · 0 enum values**. Trong đó 2 event email có consumer Worker (9 fields); 1 event profile có 2 field, hiện chưa có consumer được xác định.

Guide này ghi producer topology và evidence Identity. Hai email event giữ nguyên; `UserProfileUpdatedEvent` là contract mới trong v1.

## 2. Consumers and transport

| Producer | Consumer | Transport | Canonical contract | Sync/async | Network boundary |
|---|---|---|---|---|---|
| Identity | Worker `worker.email` | RabbitMQ event | `UserRegisteredEvent` v1 | async | RabbitMQ trên `data_net` |
| Identity | Worker `worker.email` | RabbitMQ event | `PasswordResetRequestedEvent` v1 | async | RabbitMQ trên `data_net` |
| Identity | Progress | gRPC | `GetUserHeight` v1 | sync | Network nội bộ Compose; không publish cổng ra host |
| Identity | Undetermined | RabbitMQ event | `UserProfileUpdatedEvent` v1 | async | RabbitMQ trên `data_net` |

## 3. gRPC

| RPC | Request | Response | Semantics |
|---|---|---|---|
| `GetUserHeight` | `user_id` (GUID khác rỗng) | `height_cm_x100` tùy chọn (`int32`, cm × 100) | Thiếu user/profile → `NOT_FOUND`; GUID sai → `INVALID_ARGUMENT`; height lưu không hợp lệ → `INTERNAL`; height chưa có được biểu diễn bằng field tùy chọn không được set. |

- Biểu diễn decimal: `175.00 cm` → `height_cm_x100 = 17500`; cột nguồn có kiểu `numeric(5,2)`.
- Hiện `HeightCm` là bắt buộc trong domain và database nên profile tồn tại luôn có height; `optional` giữ contract biểu diễn rõ trường hợp không có nếu model cho phép trạng thái này trong tương lai. Không dùng `0` làm sentinel.
- Lỗi transport/deadline không bị đổi thành `NOT_FOUND`; status do gRPC transport phát ra được giữ nguyên.
- Tin cậy caller nội bộ theo ADR 001 §2.2: các service cùng trust domain Compose, cổng gRPC không publish ra host; không xác thực caller, không mTLS và không shared-secret header.

## 4. Events

| Exchange | Routing key | Queue | Event | Producer | Consumer | Status |
|---|---|---|---|---|---|---|
| `longevia.events` (`topic`) | `identity.user.registered` | `worker.email` | `UserRegisteredEvent` v1 | Identity | Worker `UserRegisteredConsumer` | producer topology aligned |
| `longevia.events` (`topic`) | `identity.user.password-reset-requested` | `worker.email` | `PasswordResetRequestedEvent` v1 | Identity | Worker `PasswordResetRequestedConsumer` | producer topology aligned |
| `longevia.events` (`topic`) | `identity.user.profile-updated` | N/A | `UserProfileUpdatedEvent` v1 | Identity | Chưa xác định | Chưa có consumer |

- Payload canonical: `src/Shared/Events/UserRegisteredEvent.cs`; các field: `UserId`, `Email`, `FullName`, `VerificationToken`, `OccurredOn` (5). Không đổi payload hoặc schema version.
- Payload reset canonical: `src/Shared/Events/PasswordResetRequestedEvent.cs`; các field: `Email`, `ResetToken`, `ExpiresAt`, `OccurredOn` (4). Không đổi payload hoặc schema version. ResetToken chỉ lưu hash ở `identity_db`; bản rõ chỉ nằm trong outbox/email để Worker gửi mã một lần.
- Ordering: không bảo đảm thứ tự toàn cục; `OccurredOn` là thời điểm thực hiện nghiệp vụ tương ứng (đăng ký hoặc yêu cầu reset).
- Idempotency: Worker dedup email reset theo schema version và SHA-256 reset token; TTL 7 ngày. Cùng token delivery retry không gửi trùng; hai reset request khác token không va nhau dù email và `OccurredOn` giống nhau. Nếu SMTP lỗi, consumer giải phóng claim trước khi ném lỗi để MassTransit retry có thể gửi lại.
- Outbox: Identity EF Core bus outbox trong `identity_db`; event được ghi cùng transaction với user đăng ký hoặc reset token.
- Retry/DLQ: theo Worker: 3 retry với interval 2s/5s/10s, sau đó dead-letter vào `worker.email.dlq`.
- Reset email: Worker gửi mã OTP 64 ký tự hex dùng một lần, hết hạn sau 15 phút; không tạo link GET gọi API reset.
- Versioning/compatibility: v1 giữ nguyên; producer và consumer dùng chung event contract.
- Delta: Identity trước đây dùng topology MassTransit mặc định nên Worker không nhận được event. Producer hiện khai báo exchange topic và routing key đúng với Worker binding.

- `UserProfileUpdatedEvent` payload chỉ gồm `UserId`, `OccurredOn`; không gửi dữ liệu sức khỏe. Identity ghi event qua MassTransit EF bus outbox cùng transaction cập nhật/tạo profile. Hiện chưa có consumer được xác định; backend handoff vẫn `IN_PROGRESS`.

## 5. Internal HTTP

- N/A: không có HTTP trực tiếp giữa các service.

## 6. Compatibility and rollout

- Schema DB: migration Identity chuyển `user_profiles.HeightCm` integer sang `numeric(5,2)`; PostgreSQL integration đã xác nhận giá trị integer cũ được giữ nguyên. Identity gRPC thêm `GetUserHeight`; event contract không đổi.
- Before → after: Identity phát event profile nội bộ không version thành shared v1 event qua outbox; event email reset giữ OTP và idempotency hiện có.
- Rollout: hai event email tiếp tục cần Worker hiện hành. Profile event chưa có consumer, vì vậy không được giả định consumer/side effect tồn tại; backend handoff giữ `IN_PROGRESS`.

## 7. Test evidence

- `dotnet test src/Services/Identity/tests/Identity.IntegrationTests/Identity.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RegistrationEmailE2ETests` — passed; PostgreSQL, RabbitMQ, Redis, Worker và Mailpit chạy bằng Testcontainers. Test xác nhận email nhận ở Mailpit, lấy link, verify và login thành công.
- `dotnet test src/Services/Identity/tests/Identity.IntegrationTests/Identity.IntegrationTests.csproj` — **9/9 passed**; bao gồm PostgreSQL migration giữ nguyên `175` thành `175.00`, decimal height round-trip, validation contract `422 identity.validation_failed`, profile/outbox rollback, và E2E PostgreSQL + RabbitMQ + Redis + Worker + Mailpit.
- `AuthAndUserIntegrationTests.Registration_ShouldPersistUserAndOutboxMessageInSameTransaction` — passed; user và outbox cùng transaction, rollback loại bỏ cả hai.
- Phase 4 E2E `RegistrationEmailE2ETests.Register_ShouldSendVerificationEmailThroughRabbitWorkerAndMailpit_ThenVerifyAccount` — passed với PostgreSQL + RabbitMQ + Redis + Worker + Mailpit; xác nhận OTP/hash/expiry/replacement, concurrent reset single-winner, replay rejection, session revoke và login bằng mật khẩu mới. Worker Unit xác nhận SMTP lỗi rồi retry gửi được; negative control giữ claim sau lỗi làm test fail vì không gửi email. Negative control bỏ predicate consumed làm test fail vì hai request cùng trả 204; sau restore test passed.

## 8. Sync checklist

- [x] RPC / gRPC messages / fields: 1 RPC, 2 messages, 2 fields; codegen server/client và integration tests đối chiếu contract.
- [x] Event schema: 3 event, 11 fields, 0 enum values; đối chiếu record và v1 index.
- [x] Event topology: 1 exchange, 3 routing keys, 1 Worker queue, 2 producer/consumer pairs và 1 event chưa có consumer; xác minh cấu hình Identity.
- [x] Transactional outbox rollback test cho reset và E2E chạy với container thật; rollback không để lại user reset token hoặc event outbox.
- [x] Idempotency/retry/DLQ được đối chiếu với Worker consumer và unit test.
- [x] Phase 4 E2E evidence chạy thực tế với PostgreSQL, RabbitMQ, Worker và Mailpit.
- [x] Phase 5 PostgreSQL test chứng minh decimal height round-trip, migration giữ nguyên integer data, validation contract và profile/outbox rollback cùng nhau (9/9 integration tests passed).
