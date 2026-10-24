# Progress Service — Phase 2 foundation and persistence

**Tiêu đề**: feat(progress): Phase 2 foundation and persistence
**Base**: main | **Head**: feat/progress-service | **Feature**: qbao/001/progress-service | **Trạng thái**: Ready for Review (`PHASE_2_READY_FOR_REVIEW`)

## Tóm tắt nội dung Phase 2

PR này hoàn thiện toàn diện Phase 2 — Foundation & Persistence cho Progress Service: thiết lập cấu trúc Clean Architecture độc lập, cơ sở dữ liệu riêng `progress_db`, xác thực an toàn public-key-only, cơ chế quan sát (Observability Prometheus & Grafana), cấu hình Gateway và bộ kiểm thử toàn diện.

Toàn bộ 8/8 tasks (T001–T008) đã hoàn tất và vượt qua 100% các cổng kiểm tra (gates). Các quyết định kiến trúc liên service với Identity Service (Q08-A về chính sách xác thực JWT và Q02 về cơ chế đọc chiều cao qua gRPC cho Phase 5) đã được thống nhất trực tiếp giữa Bảo (`qbao`) và Nguyên (`nguyen`). Repo không có vị trí Technical Lead; các quyết định thuộc về sự đồng thuận của các service owners liên quan.

## Các quyết định kiến trúc liên service

1. **Q08-A — Chính sách xác thực JWT nghiêm ngặt: APPROVED (Bảo + Nguyên)**:
   - Thống nhất áp dụng strict JWT validation trên toàn hệ thống: thuật toán ES256, bắt buộc header `kid`, claim `sub` phải là Guid hợp lệ khác empty, fail-start nếu thiếu public key cấu hình hoặc private key bị cấu hình nhầm.
   - Progress Service duy trì các lớp bảo vệ cục bộ nghiêm ngặt đã kiểm chứng (`ProgressJwtOptionsValidator`, validation parameters nghiêm ngặt).
   - Nguyên (owner Identity Service) chịu trách nhiệm nâng cấp `src/Shared/Auth/**` trong một PR riêng biệt của Identity để tránh xung đột mã nguồn.

2. **Q08-B — Resource Ownership và Error Policy: PENDING FOR BUSINESS RESOURCE ENDPOINTS**:
   - Phê duyệt của Nguyên chỉ áp dụng cho chính sách JWT (Q08-A), không bao hàm toàn bộ hành vi ownership và error status của các business endpoint.
   - Các câu hỏi chưa chốt (403 vs 404 ownership hiding, Problem Details error mapping, test isolation 2 user) sẽ được quyết định và kiểm chứng khi triển khai các entity nghiệp vụ cụ thể ở Phase 3–6.
   - Do Phase 2 không có business resource endpoints (chỉ có foundation `/alive`, `/health`, `/metrics`), việc chưa test business-resource ownership không ảnh hưởng đến nghiệm thu Phase 2 foundation.

3. **Q02 — Đọc chiều cao cho chỉ số BMI trong Phase 5: APPROVED HƯỚNG KIẾN TRÚC gRPC READ**:
   - Thống nhất sử dụng gRPC theo mô hình Point-to-Point Synchronous Read: Identity là gRPC Service Provider, Progress là Consumer.
   - Kiểu dữ liệu chiều cao được chốt là `decimal` (tương ứng `numeric(5,2)` đã được merge vào `main` trong PR #10).
   - Snapshot chỉ số BMI là giá trị bất biến theo thời gian (Historical Snapshot), không recalculate lại các bản ghi cũ khi người dùng cập nhật chiều cao mới.
   - **Hiện trạng**: Định nghĩa chi tiết schema Protobuf (`identity.proto`) PENDING trước Phase 5; runtime provider chưa có trong Identity; Phase 5 integration chưa triển khai; không tạo RPC giả hay adapter trong Phase 2.
   - Sự kiện `UserProfileUpdatedEvent` (PR #10) chỉ mang `UserId` và `OccurredOn`, không mang chiều cao và không thay thế cho gRPC read.

## Các hạng mục đã thực hiện

1. **Clean Architecture (T002)**:
   - `Progress.Domain`: Domain layer cô lập, POCO, không phụ thuộc framework hay bên ngoài.
   - `Progress.Application`: Abstractions (`IUnitOfWork`), `TimeProvider`, DI container wiring.
   - `Progress.Infrastructure`: EF Core Npgsql, `ProgressDbContext`, `ProgressDatabaseInitializer`, `ProgressDatabaseOptions`.
   - `Progress.Api`: Host composition root, Problem Details chuẩn RFC 7807, centralized exception handling, Swagger Development, JWT authentication.
   - Đăng ký 6 projects vào `longevia-be.slnx` theo Explicit Developer Authorization từ Bảo.

2. **Xác thực & Bảo mật (T004, T005)**:
   - Xác thực JWT ES256 chỉ dùng public key SPKI PEM P-256 (`Jwt__SigningPublicKey`), hỗ trợ key rotation (`Jwt__PreviousKeyId`/`Jwt__PreviousPublicKey`).
   - Cấm triệt để inject private key vào Progress Service (fail-start ngay lập tức nếu phát hiện cấu hình).
   - `ProgressJwtOptionsValidator` kiểm tra nghiêm ngặt cấu hình tại startup.
   - Token validation từ chối: unsigned, wrong curve/algorithm, expired, missing kid, unknown kid, invalid/missing sub claim (bắt buộc Guid hợp lệ).

3. **Cơ sở dữ liệu & Persistence (T006)**:
   - `ProgressDbContext` kế thừa `DbContext` và thực thi `IUnitOfWork`.
   - Quản lý database riêng biệt `progress_db`, role `progress_user`. Không truy cập database khác.
   - `ProgressDatabaseInitializer`: Kiểm tra kết nối trước khi nhận traffic, chỉ gọi `MigrateAsync` khi có migration thật. Không dùng `EnsureCreated`, không tạo migration rỗng hay bảng giả khi chưa có entity nghiệp vụ.

4. **Hạ tầng & Observability (T007)**:
   - `src/Services/Progress/Dockerfile`: Multi-stage build, runtime chiseled (`nightly/runtime-deps:10.0-noble-chiseled`), tích hợp native probe `--health-check`.
   - `deployments/docker-compose.yml`: Cấu hình service `progress`, network `edge` + `data_net`.
   - Nginx Gateway: Route `/api/progress/` (strip prefix) hoạt động chuẩn, bảo vệ chống duplicate prefix (404).
   - Operational endpoints: `/alive` (200), `/health` (200 khi DB online, 503 khi DB down), `/metrics`.
   - Prometheus & Grafana: Scrape target `progress` UP trên port 8080/metrics, metric `http_server_request_duration_seconds_count` tăng thực tế, Grafana datasource và dashboard `aspnetcore-overview` đã kiểm chứng.
   - Kịch bản `scripts/smoke/progress-smoke.ps1` chạy pass 16/16 assertions.

## Kết quả kiểm chứng & Bằng chứng Gates

### Kiểm chứng Local (Branch `feat/progress-service`)
- **Định dạng (Fast Gate)**: `dotnet format longevia-be.slnx --verify-no-changes` -> Pass (Exit 0)
- **Biên dịch Debug & Release**: 0 Warning, 0 Error (warnings-as-errors)
- **Progress Unit Tests**: 21 passed, 0 failed, 0 skipped
- **Progress Integration Tests**: 34 passed, 0 failed, 0 skipped (chạy trên PostgreSQL Testcontainers thật)
- **Toàn bộ Test Suite cục bộ**: 592 passed, 0 failed, 0 skipped qua 9 test assemblies (kết quả branch-only trước khi có PR #10)
- **Quét lỗ hổng bảo mật**: 27 projects sạch, không có gói có lỗ hổng
- **Team Feature Check**: `pwsh scripts/speckit/team-feature.ps1 check` -> Consistent (6/6 features consistent)
- **Smoke Tests**: `pwsh scripts/smoke/progress-smoke.ps1` -> 16/16 assertions passed

### Kiểm chứng Remote CI trên GitHub Actions
- **Push CI trên feature branch** ([Run 40122303606](https://github.com/fa26-prn232-group2/longevia-be/actions/runs/40122303606)): **SUCCESS** (3m9s).
- **PR Merge-Ref CI** ([Run 40122307276](https://github.com/fa26-prn232-group2/longevia-be/actions/runs/40122307276)): **SUCCESS** (2m50s), Release build 0 warning / 0 error.
- **Minh bạch số lượng kiểm thử**:
  - **592 tests**: Kết quả branch-only độc lập.
  - **610 tests**: Kết quả chạy trên PR Merge-Ref CI. Sau khi PR #10 của Identity (thêm 18 tests) được merge vào `main`, kết quả merge tự động đạt chính xác $592 + 18 = 610$ tests passed, 0 failed, 0 skipped.

## Trạng thái Tasks Phase 2 (T001–T008)

| Task | Trạng thái | Bằng chứng | Ghi chú |
| --- | --- | --- | --- |
| T001 | DONE | research.md, plan.md | Q08-A & Q02 approved; Q08-B deferred |
| T002 | DONE | 6 projects trong longevia-be.slnx | Clean Architecture hoàn tất |
| T003 | DONE | 2 test projects, Testcontainers | 55 Progress tests passed |
| T004 | DONE | AuthTests.cs, HostTests.cs | Public-key ES256 validation nghiêm ngặt (Q08-A); Q08-B deferred |
| T005 | DONE | Host Program.cs, Problem Details | RFC 7807, correlation ID, Swagger |
| T006 | DONE | ProgressDbContext, Initializer | DB isolation hoàn tất |
| T007 | DONE | progress-smoke.ps1 pass 100% | Docker/Gateway/Prometheus UP |
| T008 | DONE | verification.md đầy đủ evidence | Minh bạch 592 branch vs 610 PR merge-ref tests |

## Trạng thái nghiệm thu

- **Trạng thái**: `PHASE_2_READY_FOR_REVIEW`
- **Sẵn sàng cho Human Review**: PR đã sẵn sàng để reviewer tiến hành đánh giá và merge vào nhánh `main`.
