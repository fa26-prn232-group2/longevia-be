# Corrective PR #13 — vận hành đối soát JSON v1

Chỉ dùng snapshot JSON v1 do Diet xuất; không phải CSV hoặc public API.
Canonical contract hiện nằm trên PR #12 (chưa merge); không copy/đổi contract
trong corrective Progress. `capturedAtUtc` là audit timestamp, không phải cursor.

## Thực thi

Operator cấp `ConnectionStrings__Progress` qua môi trường/secret store, trỏ đúng
`progress_db`; không có password mặc định. Không truyền secret trong command line.

```powershell
pwsh scripts/progress/reconcile-meal-activity-snapshot.ps1 -SnapshotPath artifacts/diet/snapshot.json
```

Script launcher chạy CLI `Progress.Api --reconcile-meal-snapshot <path>` trước khi
khởi tạo web host, JWT, broker hoặc migration. Có thể dùng binary đã publish:
`dotnet Progress.Api.dll --reconcile-meal-snapshot <path>`.

1. Parse toàn bộ file JSON, kiểm tra schema=1, UUID khác empty, MealLogId unique,
   UTC timestamp có offset tường minh, DateOnly ISO, count không âm và bằng số entry.
2. SHA-256 tính trên `GetRawText()` nguyên bản UTF-8 của từng entry + một LF, kể cả
   entry cuối. Không serialize lại trước hash; chữ hex uppercase phải khớp chính xác.
   Duplicate JSON property bị từ chối để tránh header/entry mơ hồ.
3. Chỉ sau integrity thành công mới mở `progress_db`, transaction RepeatableRead
   READ ONLY; batch tối đa 500 ID dùng typed Npgsql parameters. Không ghi/repair DDL/DML.
4. Với mỗi entry: contribution duy nhất `Diet.Meal`/SourceLogId=MealLogId, UserId/Date
   khớp; inbox duy nhất EventId=MealLogId và MealLogId/UserId/BusinessDate khớp.
   Query inbox theo cả EventId và MealLogId để phát hiện association mâu thuẫn.
5. Báo count matched, missing từng bảng, user/date mismatch và integrity mismatch.
   Không log user payload/connection string. Bảng DB chỉ đọc theo batch; JSON document
   và tập unique IDs giữ trong memory theo kích thước file, cần RAM phù hợp cho file lớn.

Exit codes: 0=PASS toàn bộ snapshot; 2=projection thiếu/sai; 3=snapshot invalid;
4=DB/config/query không khả dụng hoặc chưa hoàn tất. Không có PASS khi skip DB.

Extra live records ngoài snapshot không được đếm ở phiên bản này (NOT EVALUATED),
không dùng equality toàn bảng/recordCount và không xóa extra. Backdate/replay giữ
BusinessDate, dù CreatedAt/OccurredOn khác ngày snapshot.

## Điều kiện ngoài công cụ

Nâng toàn bộ Diet writer lên producer v1; bind durable Progress queue trước replay;
operator kiểm tra outbox/broker drain và failure queue riêng. PASS đối soát chỉ chứng
minh tập entry của file ở một DB snapshot, không chứng minh outbox đã drain hoặc
production lịch sử hoàn chỉnh. Chưa có DTO completeness được duyệt: đây vẫn là
readiness blocker cho tuyên bố lịch sử streak đầy đủ, không thêm field dashboard.

## Tái lập test producer thật, không merge PR #12

```powershell
git fetch origin
pwsh scripts/progress/test-diet-interoperability.ps1 -DietRef origin/feat/diet-service
```

Script archive Diet ref vào thư mục tạm, chép source Progress hiện tại (bỏ bin/obj),
thêm duy nhất harness test vào môi trường tạm. Không đổi nhánh/source Diet trong
repository. Harness chạy Diet API + publisher/outbox/replay/exporter thật, broker
RabbitMQ và hai PostgreSQL Testcontainers riêng, rồi consumer/projection Progress
và đối soát JSON. Không service nào query database của service khác. Log SHA Diet
và thư mục evidence; Testcontainers chỉ cleanup tài nguyên do test tạo.

Broker suite thường xuyên trong `Progress.IntegrationTests` dùng synthetic publisher,
được gọi đúng là transport integration. Harness producer thật là evidence riêng,
không cộng vào full solution test count và không thay bằng kiểm tra compile.
