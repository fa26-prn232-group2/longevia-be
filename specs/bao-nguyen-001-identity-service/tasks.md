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
- [x] T011a Tạo migration Code First `ReconcileMassTransitOutboxModel` để đồng bộ drift outbox và cập nhật snapshot; `has-pending-model-changes` xác nhận không còn drift. Migration bỏ cột legacy `OutboxState.BusName`, cần bảo đảm outbox không còn trạng thái đang xử lý trước khi áp dụng trên DB có dữ liệu.\n- [ ] T011 Tạo migration Code First cho token verification/reset, refresh token hash/concurrency và `HeightCm` decimal trong `src/Services/Identity/Identity.Infrastructure/Persistence/Migrations/`; review upgrade/backfill.
- [ ] T012 Thêm integration test PostgreSQL cho database trống, migration idempotent, schema/constraints/indexes và nâng cấp dữ liệu height hiện có.

**Checkpoint**: migration drift MassTransit Outbox đã có migration Code First; `IdentityApiFactory` khởi động database Testcontainers qua startup migration; pending-model warning bằng 0. Các migration cho token/profile vẫn chờ implementation tương ứng.

## Phase 3 — Đăng ký và xác minh email (US1)

- [ ] T013 [P] Thêm test domain/use case token verify hash, TTL 24h, consumed once, invalid/expired/replay trong `src/Services/Identity/tests/Identity.UnitTests/Features/Verification/`.
- [ ] T014 Thêm hash/expiry/consumed lifecycle và phương thức verify vào entity/repository Identity; token raw chỉ đi trong event/email, không lưu/log.
- [ ] T015 Thêm use case + validator + endpoint `POST /auth/verify?token=` trong `src/Services/Identity/Identity.Application/Features/Verification/` và `src/Services/Identity/Identity.Api/Endpoints/AuthEndpoints.cs`.
- [ ] T016 Kiểm thử transactional outbox: tạo user + `UserRegisteredEvent` cùng transaction; rollback không để lại user/outbox lệch nhau.
- [ ] T017 Thêm integration test verify trên PostgreSQL thật, gồm sai/hết hạn/dùng lại và login chỉ thành công sau verify.
- [ ] T018 Thêm E2E PostgreSQL + RabbitMQ + Worker, publish email đăng ký và xác nhận email xuất hiện tại Mailpit `http://localhost:8025` rồi verify thành công.

## Phase 4 — Đổi/quên/đặt lại mật khẩu (US4)

- [ ] T019 [P] Viết unit tests cho change/forgot/reset, policy mật khẩu, enumeration resistance, hash/expiry/single-use và revoke session.
- [ ] T020 Cập nhật canonical endpoint/request/response/error schemas trong `docs/api/api-spec.md` trước implementation và giữ API error codes ổn định.
- [ ] T021 Thêm use cases/validators/endpoints change, forgot, reset trong `src/Services/Identity/Identity.Application/Features/Password/` và `src/Services/Identity/Identity.Api/Endpoints/AuthEndpoints.cs`.
- [ ] T022 Phát `PasswordResetRequestedEvent` qua transactional outbox cùng thao tác tạo token; test event payload không log token.
- [ ] T023 Integration test PostgreSQL + RabbitMQ + Worker/Mailpit; reset token sai/hết hạn/đã dùng và session cũ đều được assert rõ.

## Phase 5 — Profile, response và contract (US3)

- [ ] T024 [P] Bổ sung test profile cho tuổi 13, biên height 50/250, decimal height, enum sai, allergies và user ownership.
- [ ] T025 Chuyển `HeightCm` sang decimal tại request/domain/DTO/validator/EF; không thêm `avatarUrl` khi không có yêu cầu/model.
- [ ] T026 Quyết định profile event theo requirement/consumer; nếu publish, thêm shared versioned event schema + producer qua outbox + consumer compatibility tests. Không dùng event unversioned tùy ý.
- [ ] T027 Cập nhật `docs/api/api-spec.md` và `specs/bao-nguyen-001-identity-service/frontend-guide.md`; xác minh mọi route/status/field/enum/error theo source.
- [ ] T028 Nếu event surface thay đổi, cập nhật `contracts/events/v1/README.md`, `specs/bao-nguyen-001-identity-service/backend-integration-guide.md` và test topology/consumer idempotency.

## Phase 6 — Close-out và CI

- [ ] T029 [P] Cập nhật `src/Services/Identity/README.md`, cấu hình cần thiết và docs Identity để không còn mô tả stub/TODO đã lỗi thời.
- [ ] T030 Chạy `pwsh scripts/speckit/team-feature.ps1 check`; xác nhận `meta.json` khai báo `Identity`, `Shared`, `Worker` đúng scope và không thay đổi feature khác.
- [ ] T031 Chạy `make lint` và `make test PROJECTS=src/Services/Identity`; báo riêng test nào cần Docker/RabbitMQ.
- [ ] T032 Chạy đường E2E thật DB + RabbitMQ + Worker + Mailpit và ghi evidence cụ thể.
- [ ] T033 Hoàn tất counted sync frontend/backend guides; trạng thái READY chỉ khi implementation, contract, tests và counts hoàn chỉnh.
- [ ] T034 Trước khi developer tự push, chạy `make check` toàn repo; không commit/push thay developer.

### Handoff integration

- [x] T035 **Có đổi client surface**: feature đổi hành vi HTTP và thêm endpoint theo spec; cần frontend guide.
- [ ] T036 **Có đổi backend integration surface**: hoàn tất backend guide nếu thay đổi event contract/payload/topology hoặc producer behavior.
- [ ] T037 **Đồng bộ frontend guide khi close-out**: route/status/DTO/enum/error counts được đếm và đối chiếu thật.
- [ ] T038 **Đồng bộ backend guide khi close-out**: event/routing/topology/compatibility/evidence counts được đếm nếu surface thay đổi.

### Dependencies

`Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6`. Tác vụ Identity không đụng schema/contract có thể tiến hành độc lập; T010 là hard gate trước migration/contract shared.
