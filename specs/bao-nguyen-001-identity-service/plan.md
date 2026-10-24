# Kế hoạch triển khai: Identity Service

**Feature**: `bao-nguyen-001-identity-service`  
**Nhánh**: `feat/identity-service`  
**Ngày**: 2026-10-24  
**Spec**: `specs/bao-nguyen-001-identity-service/spec.md`

## Tóm tắt

Hoàn thiện vòng đời tài khoản Identity: xác minh email, chỉ cho `Active` login/refresh, logout đúng chủ sở hữu, credential recovery/change, profile theo API, bootstrap EF, transactional outbox và kiểm thử thật. Giữ ES256, database-per-service, Worker là nơi gửi mail. Không sửa code/tài liệu feature Mavil; chỉ cập nhật đúng trường `status` trong `meta.json` theo xác nhận của developer.

## Bối cảnh kỹ thuật

- **Ngôn ngữ/runtime**: C# / ASP.NET Core .NET 10.
- **Lưu trữ**: PostgreSQL 17, `identity_db`, EF Core Code First.
- **Tích hợp**: RabbitMQ/MassTransit transactional outbox tới Worker; Worker gửi SMTP. Local email kiểm tra tại Mailpit `http://localhost:8025`; Grafana `http://localhost:3001`.
- **Kiểm thử**: xUnit; unit, Testcontainers PostgreSQL/RabbitMQ; E2E qua Docker Compose khi cần gateway/Worker/Mailpit.
- **Gate**: `make lint` + `make test PROJECTS=src/Services/Identity` cho phase; `make check` trước push. Người dùng tự commit/push.
- **Performance/security**: access JWT 15 phút ES256; refresh 7 ngày; BCrypt hiện có; không log secret/token/password.

## Kiểm tra Constitution

- **Đạt**: Identity chỉ sở hữu/ghi `identity_db`; không import code nghiệp vụ service khác; giao tiếp email bất đồng bộ qua RabbitMQ; EF Code First; mọi event publish phải qua outbox; event consumer phải idempotent; thêm test cho thay đổi hành vi; observability hiện đã có.
- **Đạt có điều kiện**: migration mới chỉ được tạo sau khi xác nhận registry không còn feature khác đang mở khai báo Identity. Registry đã được developer xác nhận hoàn tất; chỉ trường `status` trong `specs/mavil-001-infra-observability/meta.json` được đổi từ `open` sang `closed`. Không sửa code hoặc tài liệu khác của Mavil. Migration Identity hiện đã ghi nhận drift MassTransit Outbox; thay đổi schema nghiệp vụ tiếp theo cần có model và test tương ứng trước khi sinh migration.
- **Handoff bắt buộc**: có thay đổi HTTP/DTO nên phải cập nhật `frontend-guide.md`; nếu cập nhật event payload/topology/contract phải cập nhật `backend-integration-guide.md`. Chỉ đặt trạng thái READY sau counted sync và test evidence.

## Quyết định thiết kế

1. **Trạng thái truy cập**: chỉ `UserStatus.Active` được cấp access/refresh token. Login pending trả `identity.invalid_credentials`; refresh cho non-active trả `identity.invalid_refresh_token`; suspended tiếp tục trả forbidden ở login.
2. **Token xác minh/reset**: sinh bằng CSPRNG; chỉ lưu hash + expiry + consumed/invalidated state; consume nguyên tử. Verification TTL 24h theo email template; reset TTL 15 phút theo PRD/event contract.
3. **Outbox**: `UserRegisteredEvent` và `PasswordResetRequestedEvent` giữ nguyên schema v1 hiện có; ghi bằng EF outbox cùng transaction với thay đổi user/token. `UserRegisteredEvent` phải publish qua topic exchange `longevia.events` với routing key `identity.user.registered` để khớp Worker binding. Không publish trực tiếp sau commit.
4. **Xác minh email**: dùng GET query token để khớp link Worker hiện tại; link GET kích hoạt trực tiếp nên trình quét email có thể xác minh sớm. Token ngẫu nhiên một lần, DB chỉ lưu SHA-256, hết hạn sau 24 giờ.
5. **Refresh token**: plan yêu cầu hash-at-rest và rotation concurrency an toàn. Cần migration nếu phải đổi cấu trúc hiện tại; xác định chính xác cùng schema phase.
6. **Profile height**: API spec mô tả decimal và ví dụ `175.5`; chuyển application/domain/storage sang kiểu decimal, migration integer→numeric với backfill giữ giá trị cũ.
7. **Avatar**: không đưa `avatarUrl` vào response vì PRD không yêu cầu, entity không có field/lifecycle quản lý avatar; API example hiện chưa có nguồn dữ liệu.
8. **Password flows**: API canonical chốt `change-password` (Bearer, mật khẩu hiện tại + mật khẩu mới), `forgot-password` (Public, luôn trả 202 không body), và `reset-password` (Public, token + mật khẩu mới). Reset chỉ cho user Active; mọi refresh session bị revoke trong cùng transaction. Access JWT stateless còn hiệu lực tối đa 15 phút. Forgot-password không tiết lộ account tồn tại; reset token chỉ lưu SHA-256, TTL 15 phút, thay token mới sẽ vô hiệu token cũ.
9. **Reset email UX**: Worker gửi mã hex một lần để người dùng nhập trong ứng dụng. Không đưa link thẳng tới API reset vì endpoint POST cần cả token và mật khẩu mới; API GET không được phép làm thay đổi mật khẩu.
10. **Profile event**: source hiện publish `UserProfileUpdatedIntegrationEvent` 2-field nhưng không có shared versioned contract/consumer. Phase 5 phải đưa về tuân thủ Constitution: ngừng publish nếu không có consumer được duyệt, hoặc tạo contract versioned + consumer tương thích nếu có yêu cầu được xác nhận. Không được đánh dấu backend handoff READY trước khi xử lý.
11. **Migrations lúc start**: mặc định bật theo `migration.md`; `Database:AutoMigrate=false` là override production có kiểm soát; migrate fail thì startup fail. Model drift MassTransit Outbox đã được giải quyết bằng migration Code First; kiểm thử startup Testcontainers đã xác nhận áp dụng được. Migration bỏ cột legacy `OutboxState.BusName`; trước khi áp dụng lên DB có dữ liệu, cần xác nhận outbox đã drain để không mất trạng thái xử lý đang hoạt động.

## Model dữ liệu dự kiến

- **User**: email canonical, password hash, status, verification token hash/expiry/consumed state, reset token hash/expiry/consumed state, timestamps.
- **RefreshToken**: user id, token hash, expiry, revoke timestamp, replacement relation; unique hash; concurrency/race semantics rõ ràng.
- **UserProfile**: birth date, biological sex, decimal height cm, activity level, dietary preference, allergies, updated timestamp.
- **MassTransit outbox**: các bảng outbox hiện có thuộc `identity_db`, cùng transaction với user/token thay đổi.

## Shared Files & Conflict Hotspots

Các file sau là shared/client integration surface, phải được review trước khi sửa. Không sửa code hoặc tài liệu của Mavil; ngoại lệ phối hợp registry đã được developer xác nhận chỉ đổi `status` trong `meta.json`.

| File | Lý do | Kế hoạch |
|---|---|---|
| `docs/api/api-spec.md` | Canonical HTTP contract chung; hiện thiếu verify/change/forgot/reset, sai lệch avatar/height | Chỉ cập nhật sau khi schema/semantics được chốt; cập nhật frontend guide cùng phase |
| `contracts/events/v1/README.md` | Canonical event index dùng chung | Giữ nguyên event v1; Phase 4 kích hoạt producer reset đã được Worker tiêu thụ. API spec khớp index và topology Worker |
| `src/Shared/Events/**` | DTO event dùng chung Identity/Worker | Giữ nguyên hai event v1 trừ khi có quyết định contract; không đổi payload ngầm |
| `src/Services/Worker/Consumers/**`, `src/Services/Worker/Idempotency/**`, `src/Services/Worker/tests/Worker.UnitTests/**` | Worker consumer/idempotency dùng chung cho email Identity | Reset email cần consumer + retry semantics khớp contract; thay đổi dedup phải giữ hành vi event đăng ký |
| `docs/system-design/event-driven-design.md` | Quy tắc kiến trúc event/idempotency dùng chung | Cập nhật retry claim semantics sau review phát hiện SMTP failure có thể làm mất email |
| `src/Services/Identity/Identity.Infrastructure/Persistence/Migrations/**` | Schema/migration Identity | Chỉ một feature tạo migration; đã có migration `ReconcileMassTransitOutboxModel` để đồng bộ model MassTransit |
| `src/Services/Identity/Identity.Infrastructure/Persistence/IdentityDbContextModelSnapshot.cs` | EF snapshot sinh tự động | Chỉ cập nhật bằng `dotnet ef migrations add`, không sửa tay |

## Cấu trúc triển khai

```text
src/Services/Identity/
├── Identity.Api/Endpoints/                 # verify, password endpoints, status/auth
├── Identity.Application/Features/           # use cases, validators, errors
├── Identity.Domain/Entities/                # token lifecycle, account state
├── Identity.Infrastructure/Persistence/     # EF config/repositories/migrations
└── tests/
    ├── Identity.UnitTests/                  # state, token, ownership, validation
    └── Identity.IntegrationTests/           # API + PostgreSQL thật
src/Shared/Events/                            # chỉ sửa nếu contract được duyệt
contracts/events/v1/README.md                # canonical index nếu event surface đổi
specs/bao-nguyen-001-identity-service/         # plan, tasks, guides, evidence
```

## Trình tự phase và cổng

1. **Auth policy (đã hoàn thành)**: login/refresh chỉ Active; logout owner-check; unit negative control; scoped unit tests.
2. **Bootstrap + schema reconciliation (đã hoàn thành)**: auto-migration mặc định và fail-fast; migration drift MassTransit đã được xử lý; PostgreSQL Testcontainers xác nhận startup trên database mới, migration idempotent, schema/indexes và không còn pending-model warning.
3. **Registration + verification**: token hash/TTL, verify endpoint khớp email, transactional outbox; PostgreSQL + RabbitMQ + Worker + Mailpit E2E.
4. **Credential lifecycle**: change/forgot/reset endpoint + contract; hashed reset token, single-use, revoke sessions, non-enumeration.
5. **Profile + response contract**: decimal height migration/backfill, response/avatar contract, validation, event contract decision.
6. **Close-out**: guides/sync counts, `make lint`, scoped tests, E2E thật, `make check` trước khi developer tự push.

## Rủi ro và cách xử lý

- **EF snapshot/model drift**: đã xử lý bằng migration `ReconcileMassTransitOutboxModel`; `has-pending-model-changes` xác nhận không còn drift. Migration bỏ cột legacy `OutboxState.BusName`, nên cần xác nhận outbox đã drain trước khi áp dụng lên DB có dữ liệu.
- **API thiếu password routes**: contract trước code, mô tả rõ status/error và đồng bộ frontend guide.
- **Testcontainers phụ thuộc Docker**: chạy được trên CI/Docker host; nếu local Docker vắng thì báo skipped/blocked, không coi là test xanh.
- **Height integer→decimal**: migration chuyển đổi cần giữ nguyên dữ liệu hiện có và kiểm thử trên PostgreSQL thật.
