# Progress Service — Phase 2 foundation and persistence

**Tiêu đề**: feat(progress): Phase 2 foundation and persistence
**Base**: main | **Head**: feat/progress-service | **Feature**: qbao/001/progress-service | **Trạng thái**: Draft PR (PHASE_2_PARTIAL_DRAFT_PR)

## Tóm tắt nội dung Phase 2

PR này triển khai Phase 2 — Foundation & Persistence cho Progress Service: thiết lập cấu trúc Clean Architecture độc lập, cơ sở dữ liệu riêng `progress_db`, xác thực an toàn public-key-only, cơ chế quan sát (Observability Prometheus & Grafana), cấu hình Gateway và bộ kiểm thử toàn diện.

Toàn bộ các yêu cầu của Phase 2 độc lập đã hoàn thành và vượt qua 100% các cổng kiểm tra (gates). Do Q08 (đồng thuận Identity / Shared JWT) vẫn đang PENDING giữa Bảo và Nguyên, PR được mở dưới dạng Draft PR để đảm bảo an toàn kiến trúc.

## Các hạng mục đã thực hiện

1. **Clean Architecture (T002)**:
   - `Progress.Domain`: Domain layer cô lập, POCO, không framework.
   - `Progress.Application`: Abstractions (`IUnitOfWork`), `TimeProvider`, DI container wiring.
   - `Progress.Infrastructure`: EF Core Npgsql, `ProgressDbContext`, `ProgressDatabaseInitializer`, `ProgressDatabaseOptions`.
   - `Progress.Api`: Host composition root, Problem Details, centralized exception handling, Swagger Development, JWT authentication.
   - Đăng ký 6 projects vào `longevia-be.slnx` theo Explicit Developer Authorization từ Bảo.

2. **Xác thực & Bảo mật (T004, T005)**:
   - Xác thực JWT ES256 chỉ dùng public key SPKI PEM P-256 (`Jwt__SigningPublicKey`), hỗ trợ rotation (`Jwt__PreviousKeyId`/`Jwt__PreviousPublicKey`).
   - Cấm inject private key vào Progress Service (fail-start nếu phát hiện).
   - `ProgressJwtOptionsValidator` kiểm tra nghiêm ngặt cấu hình tại startup.
   - Token validation từ chối: unsigned, wrong curve/algorithm, expired, missing kid, unknown kid, invalid/missing sub claim (bắt buộc Guid hợp lệ).
   - Q08: PENDING Bảo + Nguyên. Không tự ý sửa Shared hay Identity.

3. **Cơ sở dữ liệu & Persistence (T006)**:
   - `ProgressDbContext` kế thừa `DbContext` và thực thi `IUnitOfWork`.
   - Quản lý database `progress_db`, role `progress_user`. Không truy cập database khác.
   - `ProgressDatabaseInitializer`: Kiểm tra kết nối trước khi nhận traffic, chỉ gọi `MigrateAsync` khi có migration. Không dùng `EnsureCreated`, không tạo migration rỗng hay bảng giả khi chưa có entity nghiệp vụ.

4. **Hạ tầng & Observability (T007)**:
   - `src/Services/Progress/Dockerfile`: Multi-stage build, runtime chiseled (`nightly/runtime-deps:10.0-noble-chiseled`), tích hợp native probe `--health-check`.
   - `deployments/docker-compose.yml`: Cấu hình service `progress`, network `edge` + `data_net`.
   - Nginx Gateway: Route `/api/progress/` (strip prefix) hoạt động chuẩn, bảo vệ chống duplicate prefix (404).
   - Operational endpoints: `/alive` (200), `/health` (200 khi DB online, 503 khi DB down), `/metrics`.
   - Prometheus & Grafana: Scrape target `progress` UP trên port 8080/metrics, metric `http_server_request_duration_seconds_count` tăng thực tế (32 -> 45), Grafana datasource và dashboard `aspnetcore-overview` đã kiểm chứng.
   - Kịch bản `scripts/smoke/progress-smoke.ps1` chạy pass 100%.

## Kết quả kiểm chứng & Bằng chứng Gates

- **Định dạng (Fast Gate)**: `dotnet format longevia-be.slnx --verify-no-changes` -> Pass (Exit 0)
- **Biên dịch Debug & Release**: 0 Warning, 0 Error (warnings-as-errors)
- **Progress Unit Tests**: 21 passed, 0 failed, 0 skipped
- **Progress Integration Tests**: 34 passed, 0 failed, 0 skipped (chạy trên PostgreSQL Testcontainers thật)
- **Toàn bộ Test Suite hệ thống**: 592 passed, 0 failed, 0 skipped qua 9 test assemblies trong Release mode
- **Quét lỗ hổng bảo mật**: 27 projects sạch, không có gói có lỗ hổng
- **Team Feature Check**: `pwsh scripts/speckit/team-feature.ps1 check` -> Consistent
- **Smoke Tests**: `pwsh scripts/smoke/progress-smoke.ps1` -> 16/16 assertions passed

## Trạng thái Tasks Phase 2 (T001–T008)

| Task | Trạng thái | Bằng chứng | Ghi chú |
| --- | --- | --- | --- |
| T001 | PARTIAL | Q01/Q08 tracked | Q08 PENDING Bảo + Nguyên |
| T002 | DONE | 6 projects trong longevia-be.slnx | Clean Architecture hoàn tất |
| T003 | DONE | 2 test projects, Testcontainers | 55 Progress tests passed |
| T004 | PARTIAL | AuthTests.cs, HostTests.cs | Chờ Q08 cross-service |
| T005 | PARTIAL | Host Program.cs, Problem Details | Chờ Q08 cross-service |
| T006 | DONE | ProgressDbContext, Initializer | DB isolation hoàn tất |
| T007 | DONE | progress-smoke.ps1 pass 100% | Docker/Gateway/Prometheus UP |
| T008 | PARTIAL | verification.md đầy đủ evidence | Chờ Q08 hoàn tất nghiệm thu an toàn |

## Blockers & Đề xuất tiếp theo

- **Q08 (Bảo + Nguyên)**: Cần thống nhất chính sách JWT / claim contract giữa Identity và Shared Authentication. Sau khi có đồng thuận, sẽ chuyển Draft PR sang Ready for Review.
- **Q01**: Chặn date/timezone logic cho Phase 4–6, không chặn Phase 3 Fasting.
