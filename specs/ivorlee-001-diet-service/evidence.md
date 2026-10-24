# Bằng chứng xác minh Diet độc lập

Môi trường: Windows, .NET SDK 10.0.402, Docker Desktop, PostgreSQL 17.
Nhánh feat/diet-service, owner ivorlee. Developer đã cho phép push và tạo PR.

## Gate đã chạy

Máy không có make; dùng các lệnh dotnet tương đương target Makefile.

| Kiểm tra | Kết quả |
| --- | --- |
| dotnet format longevia-be.slnx --verify-no-changes --verbosity quiet | Exit 0 |
| dotnet build longevia-be.slnx --no-restore -warnaserror | 0 warning, 0 error |
| dotnet build longevia-be.slnx -c Release --no-restore -warnaserror | 0 warning, 0 error |
| dotnet test longevia-be.slnx -c Release --no-restore | 537 passed, 0 failed, 0 skipped |
| dotnet test longevia-be.slnx -c Release (gate trước push) | 537 passed, 0 failed, 0 skipped |
| dotnet list longevia-be.slnx package --vulnerable --include-transitive | 21 project không có package dễ tổn thương theo nguồn hiện tại |
| EF has-pending-model-changes (Diet Infrastructure, startup Diet API) | Không có thay đổi chưa migration |
| pwsh scripts/speckit/team-feature.ps1 check | 5 feature, owner ivorlee, layout hợp lệ |
| pwsh scripts/smoke/diet-smoke.ps1 | 12 kiểm tra qua trên image vừa rebuild |
| pwsh scripts/smoke/infra-smoke.ps1 | 6/6 qua |

537 test gồm Diet unit 11 + integration 16, Identity unit 52 + integration 6,
Recommendation unit 372 + integration 64, Worker unit 16. Integration Diet dùng
PostgreSQL thật qua Testcontainers và token ES256 ký bằng key test riêng.

## Những hành vi được kiểm chứng

- Catalog chung/custom, paging, không dùng được món riêng của tài khoản khác.
- Sáu operation đều yêu cầu đăng nhập; sub không hợp lệ trả 401.
- Target nhập tay, macro ±5%, cập nhật và concurrent upsert giữ nguyên ID.
- Meal nhiều lần/ngày được cộng; batch có food sai không lưu một phần.
- Lịch sử giữ snapshot dinh dưỡng dù catalog đổi; user khác không đọc được log.
- 400 validation có errors/traceId, null items và macro sai bị từ chối.
- Swagger: 6 operation, 13 DTO nghiệp vụ, tổng 73 property khai báo.
- Gateway thật → Diet → PostgreSQL: 150g món mẫu lưu 247,5 kcal / 46,5g protein.
- Prometheus job Diet up; Grafana localhost:3001 healthy; Mailpit localhost:8025
  nhận email thử qua SMTP localhost:1025. Mỗi lần chạy Diet smoke gửi một email
  local tới example.invalid, không dùng relay bên ngoài.

## Kiểm tra đối chứng

- Test tính khẩu phần ban đầu thất bại trước khi có implementation, sau đó qua.
- Tạm bỏ điều kiện owner ở GetVisibleAsync: test isolation thất bại đúng với
  expected 404 / actual 201. Đã khôi phục điều kiện, chạy lại toàn suite xanh.
- Infra smoke với ForceFail thất bại một assertion như dự kiến; chạy thường qua.

## Giới hạn

Chưa kiểm chứng login Identity end-to-end: smoke dùng JWT ký bằng key local.
Chưa triển khai BMR/TDEE, AI, event streak/digest hay weekly plan phía Diet;
xem [deferred.md](deferred.md). Không đổi contract gRPC/event. Đã chạy đầy đủ các
lệnh tương đương make check trước push, bao gồm vulnerability scan.
Seed ba món là dữ liệu minh họa, chưa phải catalog dinh dưỡng sản xuất.
