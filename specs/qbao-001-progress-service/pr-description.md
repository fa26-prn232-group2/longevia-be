# Progress Business Capabilities, Identity Height gRPC & Weight gRPC Provider (Q10)

PR #13 hoàn thiện toàn bộ phạm vi sở hữu của Progress Service (Dev 3 - Bảo), bao gồm các business domains Fasting, Water, Sleep, Body Metrics, Dashboard, cùng hệ thống xử lý bất đồng bộ Meal Consumer / Durable Inbox / Snapshot Reconciliation / Q01 UTC Boundaries, và hai chiều tích hợp gRPC liên microservice:
1. **Progress -> Identity (Client)**: Lấy chiều cao người dùng qua `GetUserHeight` để tính BMI chính xác và lưu immutable snapshot.
2. **Diet -> Progress (Provider - Q10)**: Cung cấp endpoint gRPC `GetLatestWeight` nội bộ để Diet Service tính toán BMR/TDEE.

## 1. Đồng bộ và Kiến trúc Hợp nhất

- **Main Sync**: Merge commit từ `origin/main` (`4c62273`, merge Diet PR #12) vào `feat/progress-service`. Giữ nguyên 100% lịch sử commit đã publish, không rebase hay force-push.
- **Canonical Contracts**:
  - `MealLoggedEvent`: Sử dụng canonical version từ `main`.
  - `contracts/progress/v1/progress.proto`: Chuẩn hóa canonical proto cho `longevia.progress.v1.ProgressService/GetLatestWeight`.
- **ADR 003**: Ghi nhận quyết định kiến trúc Diet -> Progress gRPC tại `docs/decisions/003-diet-to-progress-weight-grpc.md` (Approved bởi Bảo và Huy).

## 2. Chi tiết Triển khai Q10 Weight gRPC Provider

- **Service Implementation**: `ProgressGrpcService` triển khai `ProgressServiceBase` kế thừa từ protobuf codegen trong `Progress.Api.csproj`.
- **Wire Semantics**:
  - `user_id`: Bắt buộc định dạng GUID hợp lệ, trả `INVALID_ARGUMENT` nếu sai.
  - `as_of_utc`: Hỗ trợ snapshot thời điểm quá khứ; nếu trống hoặc ở tương lai, tự động clamp về `server UtcNow`.
  - Deterministic tie-break: `ORDER BY LoggedAt DESC, Id DESC`.
  - Precision: `weight_kg_x100` định dạng `int32` (ví dụ: 70.50 kg -> 7050).
  - Explicit Absence: Người dùng chưa có cân nặng trả kết quả `no_record_found` trong `oneof`, tuyệt đối không trả 0 giả lập.
  - Fault Isolation: Lỗi kết nối CSDL trả gRPC `UNAVAILABLE` với log an toàn, không swallow thành missing record.
- **Dual-port Hosting**:
  - Kestrel lắng nghe đồng thời HTTP/1.1 trên cổng 8080 (`PROGRESS:HTTP_PORT` cho REST APIs, Health, Metrics) và HTTP/2 trên cổng 50051 (`PROGRESS:GRPC_PORT` cho internal gRPC).
  - Cổng 50051 chỉ lưu thông nội bộ trong mạng Compose, không publish ra host và không route qua Nginx.

## 3. Bằng chứng Kiểm thử & Chất lượng (Evidence)

- **Unit Tests Scoped Progress**: **96/96 PASS** (100% pass, 0 fail, 0 skip). Bổ sung 12 unit tests cho `WeightQueryService` và `ProgressGrpcService`.
- **Configuration & Integration Tests**:
  - `ProgressWeightConfigurationTests`: 2/2 PASS cục bộ (xác minh cấu hình cổng Kestrel mặc định và override).
  - `ProgressWeightGrpcTests`: 20 kịch bản bao phủ toàn bộ wire semantics, fault handling, user data isolation, boundary values, và luồng E2E Identity Height -> Progress BMI -> Weight gRPC. Đã biên dịch 100% thành công sẵn sàng cho CI.
- **Docker Local Note**: Do Docker daemon không chạy trên máy phát triển cục bộ, các bài test Testcontainers sẽ chạy đầy đủ trong môi trường GitHub Actions CI.
- **Full Quality Gates**:
  - `dotnet build -c Release`: Full solution Release build PASS (0 warning, 0 error).
  - Scoped format verify PASS.
  - Vulnerability scan 27 projects không có package bị lỗ hổng bảo mật.

## 4. Tình trạng Bàn giao & Sẵn sàng

- **Huy (Diet Owner - Dev 2)**:
  - Phía Progress đã hoàn thành và sẵn sàng 100% phần server.
  - Huy triển khai Weight gRPC client và snapshot target bên Diet theo tài liệu bàn giao `specs/qbao-001-progress-service/handoffs/dev2-huy-q10.md`.
  - Cung cấp script kiểm thử tích hợp `scripts/progress/test-diet-interoperability.ps1` và runbook `specs/qbao-001-progress-service/corrective-reconciliation.md`.
- **Nguyên (Identity Owner - Dev 1)**:
  - Height gRPC client đã tích hợp functional hoàn tất. Authenticated service-to-service trust đang chờ theo quy hoạch chung của nhóm.
- **Trạng thái PR #13**: Giữ trạng thái **OPEN / DRAFT**. Tuyệt đối không tự động merge PR #13.

**Trạng thái nghiệm thu:** `PROGRESS_IMPLEMENTED_PENDING_EXTERNAL_ROLLOUT`
