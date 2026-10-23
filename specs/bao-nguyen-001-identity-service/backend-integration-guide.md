# Backend Integration Guide — Identity email verification

## 1. Summary

- Feature/spec: `specs/bao-nguyen-001-identity-service/spec.md`
- Requirement: `docs/requirements/PRD.md` §2.1
- Handoff status: **READY_FOR_BACKEND**
- Canonical contract: `src/Shared/Events/UserRegisteredEvent.cs` and `contracts/events/v1/README.md` (v1)
- Counts: **0 RPC · 0 messages gRPC · 0 gRPC fields · 1 event · 5 event fields · 0 enum values**

Guide này ghi phần producer topology và evidence của Identity; schema event canonical giữ nguyên.

## 2. Consumers and transport

| Producer | Consumer | Transport | Canonical contract | Sync/async | Network boundary |
|---|---|---|---|---|---|
| Identity | Worker `worker.email` | RabbitMQ event | `UserRegisteredEvent` v1 | async | RabbitMQ trên `data_net` |

## 3. gRPC

- N/A: Phase 3 không thay đổi gRPC surface.

## 4. Events

| Exchange | Routing key | Queue | Event | Producer | Consumer | Status |
|---|---|---|---|---|---|---|
| `longevia.events` (`topic`) | `identity.user.registered` | `worker.email` | `UserRegisteredEvent` v1 | Identity | Worker `UserRegisteredConsumer` | producer topology aligned |

- Payload canonical: `src/Shared/Events/UserRegisteredEvent.cs`; các field: `UserId`, `Email`, `FullName`, `VerificationToken`, `OccurredOn` (5). Không đổi payload hoặc schema version.
- Ordering: không bảo đảm thứ tự toàn cục; event có `OccurredOn` tại lúc đăng ký.
- Idempotency: Worker dedup theo schema version, user id và timestamp; TTL 7 ngày.
- Outbox: Identity EF Core bus outbox trong `identity_db`; event được ghi cùng transaction với user đăng ký.
- Retry/DLQ: theo Worker: 3 retry với interval 2s/5s/10s, sau đó dead-letter vào `worker.email.dlq`.
- Versioning/compatibility: v1 giữ nguyên; producer và consumer dùng chung event contract.
- Delta: Identity trước đây dùng topology MassTransit mặc định nên Worker không nhận được event. Producer hiện khai báo exchange topic và routing key đúng với Worker binding.

## 5. Internal HTTP

- N/A: không có HTTP trực tiếp giữa các service.

## 6. Compatibility and rollout

- Schema: không đổi; không cần migration contract hoặc rollout hai phiên bản.
- Before → after: user registration vẫn publish trong transactional outbox; topology producer được cấu hình khớp exchange/routing key Worker đang consume.
- Rollout: triển khai Identity producer topology trước hoặc cùng Worker hiện tại; contract v1 và Worker queue tương thích.

## 7. Test evidence

- `dotnet test src/Services/Identity/tests/Identity.IntegrationTests/Identity.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RegistrationEmailE2ETests` — passed; PostgreSQL, RabbitMQ, Redis, Worker và Mailpit chạy bằng Testcontainers. Test xác nhận email nhận ở Mailpit, lấy link, verify và login thành công.
- `AuthAndUserIntegrationTests.Registration_ShouldPersistUserAndOutboxMessageInSameTransaction` — passed; user và outbox cùng transaction, rollback loại bỏ cả hai.

## 8. Sync checklist

- [x] RPC / gRPC messages / fields: 0 (phase không đổi gRPC).
- [x] Event schema: 1 event, 5 fields, 0 enum values; đối chiếu record và v1 index.
- [x] Event topology: 1 exchange, 1 routing key, 1 queue, 1 producer/consumer pair; đối chiếu Identity config với Worker binding.
- [x] Outbox, idempotency và retry/DLQ đối chiếu với producer/consumer hiện hành.
- [x] E2E evidence chạy thực tế với RabbitMQ và Mailpit.
