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

Giới hạn ở giai đoạn độc lập trước đây: smoke dùng JWT ký bằng key local.
Giai đoạn 2026-10-25 bên dưới đã bổ sung kiểm chứng login Identity thật.
Chưa triển khai BMR/TDEE, AI, digest hay weekly plan phía Diet; consumer Streak
Progress còn thiếu, Diet producer đã có ở giai đoạn bên dưới;
xem [deferred.md](deferred.md). Không đổi contract gRPC/event. Đã chạy đầy đủ các
lệnh tương đương make check trước push, bao gồm vulnerability scan.
Seed ba món là dữ liệu minh họa, chưa phải catalog dinh dưỡng sản xuất.

## Giai đoạn cung cấp tích hợp — 2026-10-25

Base main 2e249a2. Feature vẫn open vì còn các dependency đã ghi rõ.

| Kiểm tra | Kết quả hiện tại |
| --- | --- |
| dotnet format longevia-be.slnx --verify-no-changes --verbosity quiet | Exit 0 |
| dotnet build longevia-be.slnx --no-restore -warnaserror | 0 warning, 0 error |
| dotnet test longevia-be.slnx -c Release --no-restore | 560 passed, 0 failed, 0 skipped |
| EF has-pending-model-changes, Diet | Không có model chưa migration |
| team-feature.ps1 check | 6 feature, owner ivorlee, layout consistent |
| Docker Diet image rebuild + diet-smoke.ps1 | 12/12 assertion qua |
| Startup replay với durable probe queue đã bind | Đúng 3 event lịch sử, distinct EventId=MealLogId, có BusinessDate |
| identity-diet-smoke.ps1 trên Compose config đã sửa | 6/6 assertion qua |

560 test gồm Diet unit 12 + integration 20, Identity unit 67 + integration 9,
Recommendation unit 372 + integration 64, Worker unit 16. Không tính source
Progress ở PR #11 chưa merge. Không chạy vulnerability scan lại trong phase này;
gate trước push tương lai phải theo AGENTS.md.

### Evidence event và phục hồi

- Contract round-trip BusinessDate, đúng 6 property, bỏ qua unknown field.
- HTTP meal → diet_db/outbox → RabbitMQ thật → receiver đúng EventId/UserId/
  MealLogId/BusinessDate; replay giữ EventId và CreatedAt gốc.
- Rollback xóa cả meal và outbox, receiver không nhận event.
- 101 legacy meal qua hai batch replay, đủ 101 ID distinct và đúng ngày cũ.
- Ngắt dịch vụ RabbitMQ bằng rabbitmqctl stop_app: HTTP vẫn commit meal/outbox;
  start_app phục hồi, receiver nhận đúng log/ngày. Queue durable, auto-delete=false.
- Negative control bỏ PublishAsync trong MealUseCases: tạo meal thành công nhưng
  test delivery đỏ vì không có event; khôi phục publish và suite xanh.
- Test phát hiện precision .NET ticks khác PostgreSQL microseconds; đã chuẩn hóa
  CreatedAt của MealLog để original/replay không lệch timestamp.
- Fixture outage từng đỏ khi restart container/queue; cuối cùng mô phỏng ngắt
  dịch vụ trên cùng node/port với durable queue để kiểm tra đúng reconnect.

### Token Identity thật và email local

Sửa mapping JWT và RabbitMQ credentials trong Identity Compose, không sửa source
Identity/Shared auth policy. Smoke chạy qua Nginx: register, email từ Worker vào
Mailpit, verify 204, login token thật, Diet catalog và target write 2000 kcal.
Không in key/token/password; chỉ SMTP local. Script có readiness polling và lọc
recipient rõ từng phần tử (không dùng property Address trực tiếp trên array).

### Giới hạn bàn giao

Receiver test không phải consumer/projection Progress. Chưa READY_FOR_BACKEND cho
Streak đầy đủ; cần Bảo inbox/dedup/reconciliation, Duy queue/DLQ review. Startup
replay phải bind queue trước; queued count không chứng minh projection complete.
Profile/weight provider, JWT missing-kid agreement và AI/digest còn chờ.
[Bốn file gửi từng người](handoffs/README.md).

## Hoàn tất nghĩa vụ Diet không bị block — 2026-10-25

- Full Release suite: **563 passed, 0 failed, 0 skipped**. Diet unit 12 +
  integration 23; Identity 67 + 9; Recommendation 372 + 64; Worker 16.
- Whole-solution format verify exit 0; Debug build warnings-as-errors:
  0 warning, 0 error. Make không được cài, dùng lệnh dotnet tương đương.
- PostgreSQL snapshot test: empty export, 101 meal nhiều user/ngày, checksum,
  identity/timestamp đúng, không overwrite và không để file partial.
- Metadata test: null khác [], trim/lowercase/dedup/sort được persist, isolation;
  reject tag trắng/null/quá dài/quá số lượng, không persist request lỗi.
- Negative control mapper đổi null thành [] làm test đỏ; khôi phục mapper và
  toàn suite xanh. Swagger counted sync: 6 operation / 13 DTO / **75 field**.
- Migration AddFoodAllergenMetadata: Up chỉ thêm nullable text[] và giữ seed
  unknown; không xóa dữ liệu hiện có. Docker CLI áp dụng migration thành công.
- Rebuild image Diet thành công; CLI export trên DB local xuất **3 distinct
  meal** trước khi chạy smoke mới, HTTP/bus không khởi động trong lệnh export.
  Verifier file gốc PASS; sửa businessDate không sửa checksum bị reject đúng
  checksum mismatch; file gốc vẫn PASS. Artifact nằm ở artifacts/diet (ignored).
- Recreate Diet trên image mới; diet-smoke **12/12**, infra-smoke **6/6**.
  Grafana localhost:3001 và Mailpit localhost:8025 hoạt động, email chỉ local.
- Registry: 6 feature, owner ivorlee, layout consistent; git diff --check sạch.

Snapshot là file operator để đối soát, không thêm HTTP/gRPC. Checksum kiểm tra
integrity, không xác thực nguồn gửi; chuyển file bằng kênh được nhóm cho phép.
Không claim consumer Streak, BMR/TDEE hoặc meal AI hoàn tất. Dependency còn chờ
ở [remaining-blockers.md](handoffs/remaining-blockers.md); file gửi từng người ở
[handoffs/README.md](handoffs/README.md). Gate trước push được ghi riêng bên dưới.

## Gate trước push — 2026-10-25

Developer cho phép commit/push lên nhánh feature. Dùng nhánh đã bind
feat/diet-service (một feature, một nhánh), không tạo nhánh trùng feature.
Do máy không có make, chạy đủ các lệnh tương đương make check:

- dotnet format longevia-be.slnx --verify-no-changes --verbosity quiet: exit 0.
- dotnet build longevia-be.slnx -c Release --no-restore -warnaserror:
  0 warning, 0 error.
- Full Release suite chạy lại với --no-build --no-restore trên source không đổi:
  563 passed, 0 failed, 0 skipped (Diet integration 23).
- dotnet list longevia-be.slnx package --vulnerable --include-transitive:
  không phát hiện vulnerable package ở mọi project theo nguồn NuGet hiện tại.
- team-feature check: 6 feature consistent; git diff --check sạch.

Không stage .env, owner.json hoặc snapshot dữ liệu trong artifacts/diet.
Các dependency ngoài Diet vẫn giữ nguyên trạng thái open ở handoffs.
