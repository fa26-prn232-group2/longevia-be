# Kế hoạch Diet Service — dev 2

Owner `ivorlee`, nhánh `feat/diet-service`. Developer chốt triển khai phần độc
lập: catalog, target nhập tay, nhật ký, hạ tầng local và test.

## Nguồn và phạm vi

[Constitution](../../.specify/memory/constitution.md),
[thiết kế](../../docs/system-design/design-pattern.md),
[PRD §2.2](../../docs/requirements/prd.md), [API §3](../../docs/api/api-spec.md).
Tạo bốn layer Diet và hai project xUnit. Sáu REST endpoint: GET/POST foods,
GET/PUT targets/me, POST meals, GET meals/daily-summary. BMR/TDEE và AI chưa xong.

## Quyết định

- Tái sử dụng Shared auth/validation/errors/pagination/observability; map
  PagedResult nội bộ sang pageNumber/totalCount, không sửa Shared.
- Food chung hoặc thuộc một UserId; mọi query/log kiểm tra ownership từ JWT.
- Dùng decimal; quantity cùng servingUnit, tỷ lệ quantity/servingSize. Input tối
  đa 3 chữ số thập phân, snapshot làm tròn 8 chữ số và giữ tên/đơn vị/dinh dưỡng.
- GET target thiếu trả 404 diet.target_not_found. Bmr/tdee null cho target nhập
  tay, không trả số giả. Target hợp lệ khi 4P+4C+9F trong ±5% TargetCalories.
- DateOnly cho nhật ký, ngày mặc định UTC theo quy ước repo. Summary dùng target
  hiện tại; remaining có thể âm. Không tính lại snapshot từ catalog.
- Mỗi POST meal tạo log mới; summary gộp theo MealType. Log + items ghi nguyên tử.
- Seed nhỏ, ID cố định, idempotent phục vụ local; không coi là catalog đầy đủ.
- Không tạo event khi chưa có contract/consumer; không gắn broker rỗng vào Diet.

## Constitution Check

| Nguyên tắc | Cách đáp ứng |
| --- | --- |
| I | Chỉ diet_db, không import nội bộ service khác |
| II | Không thêm chiều gọi liên service chưa được duyệt |
| III | Chưa publish event; khi thêm phải version/outbox/idempotency |
| IV | Sáu REST endpoint theo API, ghi rõ nullable và missing target |
| V | Test đỏ trước code; unit, PostgreSQL integration và gateway smoke thật |

Gate theo AGENTS.md §2. Máy thiếu make dùng lệnh dotnet tương đương Makefile.
Không tự commit/push. Workflow Spec-kit tự động chưa được yêu cầu.

## Shared Files & Conflict Hotspots

| Path | Thay đổi | Phối hợp |
| --- | --- | --- |
| longevia-be.slnx | Thêm sáu project Diet | Không sửa project Identity |
| Directory.Packages.props | Khai báo EF Relational 10.0.12 giống EF hiện có; Diet tham chiếu tường minh để test không resolve 10.0.4 từ Npgsql | Không đổi phiên bản package đang dùng |
| deployments/docker-compose.yml | Thêm diet, chỉ public JWT key; giữ SMTP Mailpit | Dev 5 |
| deployments/prometheus/prometheus.yml | Static scrape diet:8080 | Dev 5 |
| deployments/nginx/nginx.conf | Đặt set upstream trước rewrite break; sửa lỗi URL rỗng ở cả bốn location cùng pattern | Dev 5, giữ route contract |
| scripts/smoke/infra-smoke.ps1 | Không giả định Diet chưa triển khai; kiểm tra upstream thiếu với Progress hiện chưa có | Dev 5 |
| scripts/smoke/diet-smoke.ps1 | E2E qua gateway và local monitoring/email catcher | Dev 2 |
| docs/api/api-spec.md | Làm rõ target nhập tay, summary | Dev 2/frontend |
| docs/system-design/diet-service.md | Thiết kế Diet mới | Dev 2 |
| docs/system-design/architecture.md | Liên kết thiết kế Diet mới | Dev 2 |
| src/Services/Diet/Diet.Api/Program.cs | Composition root mới | Feature này |
| src/Services/Diet/Diet.Infrastructure/Persistence/Migrations | EF migration mới | Chưa có feature Diet khác |

Không sửa Shared hay contract liên service hiện hữu.

## Local và kiểm chứng

.env riêng, SMTP mailpit:1025 trong compose; UI localhost:8025, Grafana
localhost:3001. Không inject Brevo credential. Startup MigrateAsync trước request,
lỗi migration làm startup fail. Unit test tính khẩu phần, macro đúng biên ±5%,
quantity lỗi. PostgreSQL thật test auth, isolation, paging, target upsert, tổng
meal chính xác, atomicity và snapshot lịch sử. Smoke Nginx + Prometheus thật.

Không đặt READY toàn bộ Diet khi [phần hoãn](deferred.md) chưa tích hợp thật.
