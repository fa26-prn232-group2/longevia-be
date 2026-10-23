# Tasks: Hoàn thiện Identity Service

**Spec**: `specs/bao-nguyen-001-identity-service/spec.md`  
**Plan**: `specs/bao-nguyen-001-identity-service/plan.md`  
**Nguyên tắc**: từng phase review/gate xong mới sang phase tiếp; không commit/push tự động.

## Phase 1 — Auth policy (đã hoàn thành)

- [x] T001 [US2] Thêm unit test chứng minh PendingVerification không login/refresh trong `src/Services/Identity/tests/Identity.UnitTests/Features/LoginUseCaseTests.cs` và `RefreshTokenUseCaseTests.cs`.
- [x] T002 [US2] Chỉ cho `UserStatus.Active` login và refresh trong `src/Services/Identity/Identity.Application/Features/Auth/LoginUseCase.cs` và `RefreshTokenUseCase.cs`; giữ lỗi suspended theo hành vi hiện tại.
- [x] T003 [US2] Ràng buộc logout theo `ICurrentUser.UserId` trong `src/Services/Identity/Identity.Application/Features/Auth/LogoutUseCase.cs`; thêm tests owner/mismatch ở `src/Services/Identity/tests/Identity.UnitTests/Features/LogoutUseCaseTests.cs`.
- [x] T004 [US2] Sửa lifecycle integration test để xác nhận pending login bị 401 trong `src/Services/Identity/tests/Identity.IntegrationTests/AuthAndUserIntegrationTests.cs`.
- [x] T005 [US2] Chạy negative control login: bỏ guard tạm thời → test đỏ vì không có exception → khôi phục guard.
- [x] T006 [US2] Hoàn tất gate scoped Identity thay cho `make` không có trên máy: Release build, format-check, unit tests 31/31 và integration test PostgreSQL/Testcontainers 1/1 đều xanh.

**Checkpoint**: unit tests xanh 31/31; integration test PostgreSQL/Testcontainers xanh 1/1; Release build 0 warning/0 error; format-check xanh.

## Phase 2 — Startup và schema baseline

- [x] T007 [P] Đặt auto-migration mặc định bật trước khi API phục vụ và bỏ ngoại lệ Testing trong `src/Services/Identity/Identity.Api/Program.cs`.
- [x] T008 [P] Để Testcontainers kiểm tra app startup/migration trong `src/Services/Identity/tests/Identity.IntegrationTests/IdentityApiFactory.cs`.
- [x] T009 Xác định model delta gây `PendingModelChangesWarning` giữa model hiện tại và `src/Services/Identity/Identity.Infrastructure/Persistence/Migrations/IdentityDbContextModelSnapshot.cs` mà không sửa tay snapshot: drift nằm ở MassTransit outbox (`OutboxState.BusName`/index và index `OutboxMessage` mới).
- [x] T010 **Gate phối hợp**: registry đã đóng `mavil-001-infra-observability` theo xác nhận của developer; chỉ đổi `status` trong `specs/mavil-001-infra-observability/meta.json`, không sửa code/tài liệu khác của Mavil.
- [x] T011 Tạo và review migration Code First `ReconcileMassTransitOutboxModel` cùng snapshot; `has-pending-model-changes` xác nhận model sạch. Migration bỏ cột legacy `OutboxState.BusName`, nên trước khi áp dụng lên DB có dữ liệu cần bảo đảm outbox đã drain.
- [x] T012 Thêm integration test PostgreSQL cho database trống, migration idempotent và schema/constraints/indexes Identity cùng Outbox.

**Checkpoint — Phase 2 hoàn tất**: migration drift MassTransit Outbox đã có migration Code First; `IdentityApiFactory` khởi động database Testcontainers qua startup migration; kiểm thử xác nhận gọi migration lặp lại an toàn, các bảng/index mong đợi tồn tại, và pending-model warning bằng 0. Migration token verification/reset và profile height nằm trong phase tương ứng với model/use case.

## Phase 3 — Đăng ký và xác minh email (US1)

- [x] T013 [P] Thêm test domain/use case token verify hash, TTL 24h, consumed once, invalid/expired/replay trong `src/Services/Identity/tests/Identity.UnitTests/Features/Verification/`.
- [x] T014 Thêm hash/expiry/consumed lifecycle và phương thức verify vào entity/repository Identity; tạo migration cho schema token; token raw chỉ có trong payload event/outbox và email; không lưu ở user/domain, không ghi log.
- [x] T015 Thêm use case + validator + endpoint `GET /auth/verify?token=` trong `src/Services/Identity/Identity.Application/Features/Verification/` và `src/Services/Identity/Identity.Api/Endpoints/AuthEndpoints.cs`.
- [x] T016 Kiểm thử transactional outbox: tạo user + `UserRegisteredEvent` cùng transaction; rollback không để lại user/outbox lệch nhau.
- [x] T017 Thêm integration test verify trên PostgreSQL thật, gồm token sai/replay và login chỉ thành công sau verify; kiểm thử domain bao phủ token hết hạn.
- [x] T018 Thêm E2E PostgreSQL + RabbitMQ + Worker, publish email đăng ký và xác nhận email xuất hiện tại Mailpit `http://localhost:8025` rồi verify thành công.

**Checkpoint — Phase 3 hoàn tất**: token xác minh chỉ lưu hash trong user, TTL 24 giờ và compare-and-set một lần; GET endpoint khớp link Worker; contract event v1 không đổi; integration PostgreSQL xác nhận token sai/hết hạn/dùng lại và user chỉ login sau verify; E2E Testcontainers xác nhận PostgreSQL + RabbitMQ + Worker + Mailpit nhận email và kích hoạt account. E2E phát hiện và sửa topology producer Identity để publish vào `longevia.events` với routing key `identity.user.registered` như Worker bind.

## Phase 4 — Đổi/quên/đặt lại mật khẩu (US4)

- [ ] T019 [P] Viết unit tests cho change/forgot/reset, policy mật khẩu, enumeration resistance, hash/expiry/single-use và revoke session.
- [ ] T020 Cập nhật canonical endpoint/request/response/error schemas trong `docs/api/api-spec.md` trước implementation và giữ API error codes ổn định.
- [ ] T021 Thêm use cases/validators/endpoints change, forgot, reset trong `src/Services/Identity/Identity.Application/Features/Password/` và `src/Services/Identity/Identity.Api/Endpoints/AuthEndpoints.cs`.
- [ ] T022 Phát `PasswordResetRequestedEvent` qua transactional outbox cùng thao tác tạo token; test event payload không log token.
- [ ] T023 Integration test PostgreSQL + RabbitMQ + Worker/Mailpit; reset token sai/hết hạn/đã dùng và session cũ đều được assert rõ.

## Phase 5 — Profile, response và contract (US3)

- [ ] T024 [P] Bổ sung test profile cho tuổi 13, biên height 50/250, decimal height, enum sai, allergies và user ownership.
- [ ] T025 Chuyển `HeightCm` sang decimal tại request/domain/DTO/validator/EF; tạo migration có kiểm tra giữ nguyên dữ liệu height hiện có; không thêm `avatarUrl` khi không có yêu cầu/model.
- [ ] T026 Quyết định profile event theo requirement/consumer; nếu publish, thêm shared versioned event schema + producer qua outbox + consumer compatibility tests. Không dùng event unversioned tùy ý.
- [x] T027 Cập nhật `docs/api/api-spec.md` và `specs/bao-nguyen-001-identity-service/frontend-guide.md`; đồng bộ route xác minh email/status/error theo source.
- [x] T028 Đối chiếu `contracts/events/v1/README.md`; giữ nguyên event v1 vì contract đã đúng, tạo `specs/bao-nguyen-001-identity-service/backend-integration-guide.md` cho producer topology và kiểm thử E2E topology/consumer.

## Phase 6 — Close-out và CI

- [ ] T029 [P] Cập nhật `src/Services/Identity/README.md`, cấu hình cần thiết và docs Identity để không còn mô tả stub/TODO đã lỗi thời.
- [ ] T030 Chạy `pwsh scripts/speckit/team-feature.ps1 check`; xác nhận `meta.json` khai báo `Identity`, `Shared`, `Worker` đúng scope và không thay đổi feature khác.
- [ ] T031 Chạy `make lint` và `make test PROJECTS=src/Services/Identity`; báo riêng test nào cần Docker/RabbitMQ.
- [ ] T032 Chạy đường E2E thật DB + RabbitMQ + Worker + Mailpit và ghi evidence cụ thể.
- [ ] T033 Hoàn tất counted sync frontend/backend guides; trạng thái READY chỉ khi implementation, contract, tests và counts hoàn chỉnh.
- [ ] T034 Trước khi developer tự push, chạy `make check` toàn repo; không commit/push thay developer.

### Handoff integration

- [x] T035 **Có đổi client surface**: feature đổi hành vi HTTP và thêm endpoint theo spec; cần frontend guide.
- [x] T036 **Có cập nhật hành vi tích hợp producer**: `UserRegisteredEvent` v1/payload và Worker consumer không đổi; cấu hình producer Identity được sửa để khớp exchange/routing key mà Worker bind. Có backend guide và E2E evidence.
- [ ] T037 **Đồng bộ frontend guide khi close-out**: route/status/DTO/enum/error counts được đếm và đối chiếu thật.
- [x] T038 **Đồng bộ backend guide cho Phase 3**: 1 event, 5 field, 1 exchange, 1 routing key, 1 queue và E2E evidence được đối chiếu; còn các phase sau sẽ cập nhật guide nếu thay đổi backend surface.

### Dependencies

`Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6`. Tác vụ Identity không đụng schema/contract có thể tiến hành độc lập; T010 là hard gate trước migration/contract shared.
