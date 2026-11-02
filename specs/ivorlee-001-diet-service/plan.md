# Kế hoạch Diet Service — dev 2

Owner `ivorlee`, nhánh `feat/diet-service`. Developer chốt triển khai phần độc
lập: catalog, target nhập tay, nhật ký, hạ tầng local và test.

## Trạng thái hiện tại — 2026-11-02

Phạm vi đã mở rộng theo yêu cầu developer: profile Identity + weight Progress,
BMR/TDEE snapshot và REST meal AI. Phần mô tả độc lập bên dưới là lịch sử; thiết
kế hiện tại ở các mục bổ sung cuối file và API §3.7–3.8.

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

## Giai đoạn cung cấp tích hợp — 2026-10-25

Developer giao Huy thực hiện phần Diet cung cấp cho các service và soạn file
Markdown riêng gửi Nguyên/Bảo/Đức/Duy. Tiếp tục feature hiện tại, không tự Git.
Giai đoạn này thay quyết định chưa publish event ở trên: bổ sung contract mới
MealLoggedEvent v1 (create-only), MassTransit EF bus outbox và replay có chủ đích.
Không đổi payload event Identity, không implement code Progress/Worker.

### Shared Files & Conflict Hotspots — bổ sung

| Path | Thay đổi | Phối hợp |
| --- | --- | --- |
| src/Shared/Events/MealLoggedEvent.cs | Contract mới 6 field, EventId ổn định bằng MealLogId | Bảo consumer; không sửa event Identity |
| contracts/events/v1/README.md | Đăng ký contract meal mới | Nguyên đang thêm profile event ở file này đã merge main |
| src/Services/Diet/Diet.Infrastructure/DependencyInjection.cs | Producer RabbitMQ + EF bus outbox | Chỉ composition Diet |
| src/Services/Diet/Diet.Infrastructure/Persistence/DietDbContext.cs và Migrations/** | Outbox schema của diet_db | Feature Diet duy nhất đang mở |
| src/Services/Diet/Diet.Infrastructure/Messaging/MealActivityReplayService.cs | Replay hosted service sau migrate/bus ready, trước traffic | Không REST admin route mới |
| deployments/docker-compose.yml | RabbitMQ config/dependency Diet, replay flag mặc định false | Duy; không chạm Progress PR #11 |
| docs/system-design/diet-service.md | Event, replay và giới hạn completeness | Bảo/Duy |

Contract-first: create-only MealLoggedEvent có EventId, UserId, MealLogId,
BusinessDate, CreatedAt, OccurredOn. EventId = MealLogId do chỉ có một loại
sự kiện tạo; OccurredOn là thời điểm publish/replay được enqueue, không thay ngày
nghiệp vụ. SchemaVersion = 1. Routing key diet.meal.logged, exchange topic
longevia.events. Không tự tạo queue Progress trong producer.

Replay bật bằng Diet:ReplayMealActivitiesOnStart, trước nhận request, đọc snapshot
RepeatableRead theo batch 100, ghi outbox cùng một transaction. Mỗi lần chạy có
thể phát lại cùng EventId; consumer phải inbox/dedup bền vững và unique MealLogId.
Phải bind queue trước replay. Count queued không phải count delivered hoặc chứng
nhận Progress complete; consumer/Bảo xác minh đủ projection sau drain/reconcile.
Chưa có update/delete meal; không hứa hỗ trợ lifecycle chưa triển khai.

Constitution: chỉ diet_db; event versioned, outbox cùng transaction; không thêm
chiều gRPC chưa có ADR; kiểm thử PostgreSQL + RabbitMQ thật và rollback/replay.

## Local và kiểm chứng

.env riêng, SMTP mailpit:1025 trong compose; UI localhost:8025, Grafana
localhost:3001. Không inject Brevo credential. Startup MigrateAsync trước request,
lỗi migration làm startup fail. Unit test tính khẩu phần, macro đúng biên ±5%,
quantity lỗi. PostgreSQL thật test auth, isolation, paging, target upsert, tổng
meal chính xác, atomicity và snapshot lịch sử. Smoke Nginx + Prometheus thật.

Không đặt READY toàn bộ Diet khi [phần hoãn](deferred.md) chưa tích hợp thật.

### Kiểm chứng token Identity thật → Diet

Sửa mapping cấu hình JWT trong deployments/docker-compose.yml của Identity
(chỉ tên key container, giữ .env bên ngoài), truyền issuer/audience và credentials
RabbitMQ đồng nhất. Không sửa code/migration Identity hoặc Shared auth policy.
Thêm scripts/smoke/identity-diet-smoke.ps1 (shared hotspot mới) để kiểm chứng
register → RabbitMQ → Worker → Mailpit → verify → login → Diet, chỉ local Mailpit.
Phối hợp Nguyên/Duy review config; không có REST liên service trong runtime.

## Hoàn thiện nghĩa vụ producer — snapshot đối soát

Developer yêu cầu làm hết phần Huy nợ, chỉ giữ việc bị chặn. Bổ sung export
snapshot meal activity cho operator để Bảo có bộ ID/ngày/số lượng và checksum
đối chiếu, không đọc diet_db hay mở gRPC/REST mới. Định dạng versioned tại
contracts/diet/v1/meal-activity-snapshot.md (shared hotspot mới, Diet sở hữu).
Program.cs thêm chế độ --export-meal-activities <path> chạy một lần sau migrate,
không start HTTP/bus. Diet.Infrastructure đăng ký MealActivitySnapshotExporter.
Snapshot JSON: schemaVersion, snapshotId, capturedAtUtc, entries (MealLogId,
UserId, BusinessDate, CreatedAt), recordCount, entriesSha256. SHA-256 của từng
JSON entry compact nguyên văn UTF-8 + LF theo thứ tự Id. RepeatableRead snapshot,
stream file tạm cùng thư mục, chỉ rename sang đích sau khi hoàn tất; không ghi đè.
Không có dữ liệu tên món, email, token. File chứa user IDs/ngày: lưu trong
artifacts/diet (gitignored) và chỉ chuyển cho operator/consumer được phép.

Shared hotspots thêm: src/Services/Diet/Diet.Api/Program.cs (CLI),
scripts/diet/verify-meal-activity-snapshot.ps1 (verifier count/checksum/ID),
contracts/diet/v1/meal-activity-snapshot.md. Không sửa task/source Progress.
Kiểm chứng PostgreSQL snapshot rỗng và 101 log/nhiều user, checksum/ID chính xác,
không overwrite; negative control sửa entry làm verifier thất bại. Completeness
producer file không thay consumer reconciliation hoặc policy timezone của Bảo.

## Metadata dị ứng do Diet sở hữu

CandidateFood.allergens trong proto Recommendation đã có. Bổ sung optional
allergens vào POST /foods và FoodResponse: null = chưa khai báo, [] = người tạo
khai báo không có tag, không phải kiểm định an toàn. Tối đa 32 tag, mỗi tag 1..50
ký tự, trim/lowercase/dedup/sort; không suy alias, thành phần hoặc dị ứng từ tên.
Seed/các row cũ giữ null. EF migration AddFoodAllergenMetadata (diet_db duy nhất),
không đổi proto hay source Đức. AI chưa mở cho đến khi profile/đầu vào và nguồn
metadata đủ theo contract; unknown không được map ngầm thành danh sách an toàn.
Shared hotspots bổ sung docs/api/api-spec.md (2 field optional, counts 75 thay 73),
FoodItemConfiguration.cs/model snapshot/migration và frontend guide.

## Q10 client — 2026-11-02

Shared Files & Conflict Hotspots: Diet.Infrastructure DI/csproj, Dockerfile;
contracts/progress/v1/progress.proto chỉ đọc/codegen, không sửa contract của Bảo.
Client trả immutable weight snapshot (kg/measuredAt/sourceMetricId/requestedAsOf).
Manual target không gọi Progress: chưa có profile/calculate-target contract để
lưu snapshot thể hiện cân nặng thực sự dùng tính toán. Không gắn cân nặng tùy ý
vào target nhập tay rồi gọi đó là snapshot BMR. DB migration sẽ cùng phase tính target.

## Snapshot target và công thức — 2026-11-02

Shared Files & Conflict Hotspots: Diet target entity/config/repository, migration
và model snapshot; TargetResponse giữ số field, trả BMR/TDEE đã lưu nếu có.
Tạo factory calculated target nội bộ nhận dữ liệu profile đã được xác thực;
chưa mở HTTP input profile tự khai vì contract Identity chỉ cung cấp chiều cao.
Lưu weight/source/measured/asOf, height/age/sex/activity factor và kết quả ở cùng
row target; PUT nhập tay clear snapshot. Tuổi tính đủ năm ở UTC date phép tính.
Activity factor là input đã xác thực (1..3), chưa tự map enum Identity khi thiếu contract.

## Adapter meal AI — 2026-11-02

Shared Files & Conflict Hotspots: Diet.Infrastructure csproj/DI/Dockerfile;
canonical recommendation.proto chỉ read/codegen. Diet compose thêm internal_net
để gọi Recommendation theo ADR001 (không expose service AI qua gateway).
Caller nội bộ phải cung cấp profile đã resolve, catalog đã kiểm tra ownership,
target và consumed calories. Adapter loại candidate allergens unknown; không coi
user-declared tags là chứng nhận y tế. Không fake profile để mở public REST flow.
Mapper chỉ chấp nhận food ID có trong request, trả serving/nutrition catalog và
citation/disclaimer đầy đủ; calorie cap suggestion ngoài catalog không log thành food.

## Hoàn thiện profile và public facade — 2026-11-02

Developer yêu cầu bổ sung phần Identity thiếu và hoàn thiện Diet. Shared Files &
Conflict Hotspots bổ sung: contracts/identity/v1/identity.proto + README (thêm
optional dietary_preference, repeated allergies, giữ nguyên số field cũ),
IdentityGrpcService.cs và IdentityGrpcTests.cs (chỉ mapping/validation, không schema).
Phối hợp Nguyên review; không sửa tasks/meta của feature Nguyên. Diet codegen,
DI/Dockerfile, API spec, hai endpoint và guides cùng cập nhật.

POST /targets/me/calculate nhận cùng bốn field mục tiêu như PUT; profile lấy từ
Identity và cân nặng Progress. Không tự đặt deficit/surplus hay tỷ lệ macro chưa
được sản phẩm quy định. Hệ số Sedentary=1.2, LightlyActive=1.375,
ModeratelyActive=1.55, VeryActive=1.725; tuổi đủ năm theo UTC.
GET /meals/ai-recommendations yêu cầu mealType và goal (LoseWeight/Maintain/
BuildMuscle/Longevity), dùng summary ngày UTC, profile/cân nặng hiện tại và tối đa
100 candidate có metadata, cùng ownership. Deadline tổng 2.8 giây, gRPC tối đa
2 giây trong ngân sách còn lại; hủy request truyền xuyên. Profile server cũ thiếu
optional dietary_preference không được coi allergies rỗng là đã khai báo.

Shared hotspots phase này còn gồm docs/decisions/004-diet-to-identity-profile-grpc.md,
docs/system-design/microservices/01-service-boundaries.md và 02-grpc-and-events.md
để ghi đúng chiều gọi mới đã được developer yêu cầu. Public DTO AI tách khỏi
record adapter; Swagger inventory mới 8 operation/6 path/16 DTO/100 field.
