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

- [x] T019 [P] Viết unit tests cho change/forgot/reset, policy mật khẩu, enumeration resistance, hash/expiry/single-use và revoke session.
- [x] T020 Cập nhật canonical endpoint/request/response/error schemas trong `docs/api/api-spec.md` trước implementation; đồng bộ event v1 với `contracts/events/v1/README.md` và Worker. Semantics: change/reset `204`, forgot luôn `202` không body, reset token lỗi `400 identity.invalid_password_reset_token`.
- [x] T021 Thêm use cases/validators/endpoints change, forgot, reset trong `src/Services/Identity/Identity.Application/Features/Password/` và `src/Services/Identity/Identity.Api/Endpoints/AuthEndpoints.cs`.
- [x] T022 Phát `PasswordResetRequestedEvent` qua transactional outbox cùng thao tác tạo token; reset token lưu hash, Worker chỉ gửi mã một lần và không log token.
- [x] T023 Integration PostgreSQL + RabbitMQ + Worker/Mailpit: reset token hết hạn/bị thay thế, concurrent reset chỉ một request thành công, replay bị từ chối, refresh session cũ bị revoke; toàn bộ 6/6 integration tests passed. Worker Unit bổ sung kiểm tra SMTP lỗi rồi retry có thể claim lại và gửi email.

**Checkpoint — Phase 4 hoàn tất**: Identity unit 52/52, Worker unit 16/16, Identity integration 6/6 (PostgreSQL + RabbitMQ + Redis + Worker + Mailpit). Close-out Release toàn repo: build 0 warning/error; 510/510 tests pass (Identity Integration 6, Identity Unit 52, Recommendation Integration 64, Recommendation Unit 372, Worker Unit 16); vulnerability scan không tìm thấy package dễ tổn thương; format-check, team-feature check, EF model check và git diff check đạt. `make` chưa cài nên chạy trực tiếp các lệnh tương đương; Docker/Testcontainers và Roslyn format chạy ngoài sandbox sau khi sandbox chặn Docker/named-pipe. Negative control bỏ điều kiện compare-and-set làm E2E đỏ vì cả hai reset request trả 204; negative control giữ claim sau SMTP lỗi làm retry không gửi email; khôi phục cả hai thì test xanh.

## Phase 5 — Profile, response và contract (US3) — hoàn thành

- [x] T024 [P] Bổ sung unit test profile cho tuổi 13, biên height 50/250, decimal height/precision, enum sai, allergies và user ownership; Identity Unit 67/67 xanh. Negative control trước sửa 422 xác nhận contract test đỏ trên `400 BadRequest`, sau sửa test xanh.
- [x] T025 Chuyển `HeightCm` sang decimal tại request/domain/DTO/validator/EF; migration Code First và PostgreSQL integration xác nhận dữ liệu integer cũ `175` được giữ thành `175.00`, decimal round-trip; không thêm `avatarUrl` khi không có yêu cầu/model.
- [x] T026 Theo PRD, thay publication nội bộ bằng shared `UserProfileUpdatedEvent` v1 chỉ chứa user ID/timestamp, cấu hình routing key `identity.user.profile-updated`, PostgreSQL integration xác nhận transactional outbox rollback; ghi rõ chưa có consumer được xác định.
- [x] T027 Cập nhật `docs/api/api-spec.md` và `specs/bao-nguyen-001-identity-service/frontend-guide.md`; đồng bộ route, DTO height decimal/precision, status/error và counted sync theo source.
- [x] T028 Đối chiếu `contracts/events/v1/README.md`; thêm event profile v1 theo PRD, đồng bộ producer topology/outbox và backend guide; PostgreSQL integration xác nhận event/outbox và rollback.

## Phase 6 — Close-out và CI

- [x] T029 [P] Cập nhật `src/Services/Identity/README.md`: bỏ mô tả stub; ghi trách nhiệm, đủ 10 route, validation/errors, migration, observability, ba event và consumer hiện có/chưa xác định, cấu trúc layer, cấu hình chạy và lệnh kiểm tra. Đã rà các liên kết tương đối trong README; bổ sung RPC GetUserHeight nội bộ.
- [x] T030 Chạy `pwsh scripts/speckit/team-feature.ps1 check`; xác nhận `meta.json` khai báo `Identity`, `Progress`, `Shared`, `Worker` đúng scope và không thay đổi feature khác.
- [x] T031 Chạy gate Phase 5 tương đương `make lint` và `make test PROJECTS=src/Services/Identity`: `make lint` (solution format-check sạch + solution build 0 warning/0 error), Identity Unit 67/67 và Integration 16/16 (Linux SDK 10 container, PostgreSQL/Testcontainers thật, bao gồm 7 test gRPC).
- [x] T032 Chạy đường E2E thật DB + RabbitMQ + Worker + Mailpit và ghi evidence cụ thể trong backend guide.
- [x] T033 Hoàn tất counted sync guides; frontend handoff READY_FOR_FRONTEND sau implementation, contract, tests và counts. Backend counts/evidence đã đồng bộ, nhưng backend handoff vẫn IN_PROGRESS vì profile event consumer chưa xác định.
- [x] T034 Chạy full close-out tương đương `make check` trong Linux SDK 10 container: format-check sạch; Release build 0 warning/0 error; full Release tests 617/617 (Diet Integration 16, Diet Unit 11, Identity Integration 16, Identity Unit 67, Progress Integration 34, Progress Unit 21, Recommendation Integration 64, Recommendation Unit 372, Worker Unit 16); vulnerability scan không tìm thấy package dễ tổn thương. `make` không có trên máy, đã chạy chính xác các lệnh dotnet trong Makefile.

### Handoff integration

- [x] T035 **Có đổi client surface**: feature đổi hành vi HTTP và thêm endpoint theo spec; cần frontend guide.
- [x] T036 **Có cập nhật hành vi tích hợp producer/consumer**: hai email event giữ schema v1 và Worker behavior; thêm profile event v1 không có consumer hiện được xác định. Contract, producer topology và outbox test đã được xác minh.
- [x] T037 **Đồng bộ frontend guide khi close-out**: route/status/DTO/enum/error counts được đếm và đối chiếu thật; frontend handoff READY_FOR_FRONTEND.
- [x] T038 Đồng bộ backend guide cho Phase 3–6: đếm event/field/topology và gRPC surface sau implementation; ghi consumer profile event chưa xác định, test evidence thực tế và giữ trạng thái IN_PROGRESS đến khi handoff đủ điều kiện.
- [x] T039 Bổ sung contract `IdentityService.GetUserHeight` với request `user_id` và optional `height_cm_x100` (`int32`, cm × 100); Identity sở hữu contract theo Q02.
- [x] T040 Implement provider: validate GUID và persisted height, trả `NOT_FOUND` khi thiếu user/profile, `INVALID_ARGUMENT` khi GUID sai, `INTERNAL` nếu height persisted ngoài miền; deadline/cancel đi theo gRPC context; listener HTTP/2 nội bộ theo ADR 001.
- [x] T041 Thêm integration tests PostgreSQL thật cho scaled height, user/profile thiếu, invalid ID và stored height không hợp lệ.
- [x] T042 Đồng bộ backend guide, plan/shared-file ownership, progress-service design và counts; codegen/test evidence xác nhận 1 RPC, 2 messages, 2 fields.


### Dependencies

`Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6`. Tác vụ Identity không đụng schema/contract có thể tiến hành độc lập; T010 là hard gate trước migration/contract shared.
