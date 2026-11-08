# Diet Service

Diet là service client-facing qua Nginx; route nội bộ không có prefix, route công
khai là /api/diet. Schema thuộc diet_db và được tạo bởi EF Code First migrations.

## Cấu trúc và dữ liệu

- Domain: FoodItem, DailyDietTarget, MealLog, MealLogItem, NutritionValues.
- Application: use case/validator/DTO và interface repository/UoW.
- Infrastructure: EF/Npgsql, repository, migration, seed demo.
- Api: Minimal API groups, Shared JWT/validation/observability, Problem Details.
- FoodItem chung có UserId null; food custom chỉ truy vấn/log được bởi chủ sở hữu.
- MealLogItem giữ snapshot tên, đơn vị, quantity và dinh dưỡng theo tỷ lệ
  quantity/servingSize; không JOIN catalog để tính lại lịch sử.
- Log và items được ghi cùng SaveChanges transaction. Một ngày/bữa có nhiều log.
- DailyDietTarget có unique UserId. PUT dùng một upsert PostgreSQL có parameter,
  giữ ID và không tạo hai target khi request đầu tiên đến đồng thời.
- Decimal input tối đa 3 số thập phân; snapshot 8 số. Ngày nhật ký DateOnly;
  timestamp UTC, ngày mặc định UTC. Summary dùng target hiện tại; remaining âm
  biểu thị vượt mục tiêu. Không tự đổi đơn vị vật lý.
- Seed gồm 43 món minh họa (3 món demo ban đầu + 40 món Việt phổ biến, migration `ExpandFoodCatalogSeed`), ID cố định trong migration, không nhân bản khi startup. Mọi món seed đều khai báo `allergens` (mảng rỗng nếu không có dị nguyên phổ biến) để được xét làm ứng viên gợi ý AI; tag dị ứng gồm tên tiếng Anh và từ tiếng Việt thường gặp (vd. `peanut`, `đậu phộng`, `lạc`) vì so khớp với dị ứng tự do trong hồ sơ là bằng nhau không phân biệt hoa thường. Giá trị dinh dưỡng là ước lượng tham khảo, không phải catalog sản xuất.

## API và phụ thuộc

Tám operation theo [API §3](../api/api-spec.md).
Target nhập tay có bmr/tdee null; thiếu target trả 404 diet.target_not_found.
BMR/TDEE và AI sử dụng Identity.GetUserProfile (ADR 004) cùng Progress.GetLatestWeight.
Không có lời gọi DB hoặc source nội bộ của Identity/Progress/Recommendation.
Diet cung cấp MealLoggedEvent v1 qua EF bus outbox trong cùng transaction meal.
Contract ở src/Shared/Events/MealLoggedEvent.cs, topic exchange longevia.events,
routing key diet.meal.logged; payload EventId, UserId, MealLogId, BusinessDate,
CreatedAt, OccurredOn. EventId = MealLogId ổn định vì lifecycle hiện create-only.
CreatedAt giữ precision microsecond như PostgreSQL; OccurredOn là thời điểm enqueue.
Consumer Progress do dev 3 triển khai, chưa coi Streak end-to-end đã hoàn thành.

Replay bật có chủ đích bằng Diet:ReplayMealActivitiesOnStart=true. Hosted service
chạy sau bus ready và migration, trước nhận traffic; đọc snapshot RepeatableRead,
batch 100, enqueue mọi meal qua outbox trong một transaction. Giữ EventId/ngày/
CreatedAt, đổi OccurredOn. Bind queue durable trước replay, inbox EventId + unique
MealLogId bền vững; queued count không chứng minh delivered/completeness. Không
suy ngày ăn từ timestamp replay. Không có sửa/xóa meal ở v1. Báo cáo tuần còn hoãn.

## Local và quan sát

Docker Diet join edge + data_net; không publish cổng riêng ra host.
Chỉ inject JWT public keys, không private key hay provider credential.
Shared AddObservability/MapPrometheusScrapingEndpoint và scrape job diet:8080
cung cấp metrics. Grafana ở localhost:3001, Mailpit UI ở localhost:8025.
SMTP local trong compose trỏ mailpit:1025. Diet không gửi email.

Nginx đặt biến upstream trước rewrite break để không bỏ qua lệnh set.
Swagger khai báo server /api/diet để Try it out đi đúng gateway.
Migration chạy trước khi host phục vụ request và fail fast nếu lỗi.
RabbitMQ là dependency runtime; Messaging:UseInMemory chỉ dành test opt-in.

Kiểm thử và phạm vi bàn giao tại
[feature ivorlee](../../specs/ivorlee-001-diet-service/plan.md).

## Metadata và đối soát do Diet cung cấp

FoodItem.Allergens là text[] nullable: null chưa khai báo, [] là người tạo khai
báo không có tag; không phải chứng nhận không dị ứng. Tags được trim/lowercase/
dedup/sort, tối đa 32 tag, 1..50 ký tự. Row cũ/seed giữ null. AI adapter sau này
không được biến unknown thành candidate an toàn; source verification cần chốt.

Chế độ operator --export-meal-activities <path> xuất snapshot JSON v1 của meal
activity từ diet_db sau migrate rồi thoát, không start HTTP/bus. Contract ở
contracts/diet/v1/meal-activity-snapshot.md. Snapshot RepeatableRead chứa ID/user/
businessDate/createdAt, số lượng và checksum SHA-256; không ghi đè file đích,
chỉ publish file sau thành công. Bảo dùng để đối chiếu projection. Đây không phải
REST hay gRPC mới; file lưu dưới artifacts/diet và chuyển theo quyền operator.

## Q10 client cân nặng

Diet.Infrastructure codegen client từ contracts/progress/v1/progress.proto;
ProgressGrpc:Address mặc định http://progress:50051. ADR 003 internal trust,
không token riêng. Deadline 2 giây, không retry; cancellation truyền xuyên.
Chỉ no_record_found=true trả null; unavailable/deadline -> diet.weight_unavailable
(503), response sai/lỗi khác -> diet.weight_integration_failed (502). Snapshot
in-memory giữ kg decimal, measuredAt, sourceMetricId, requestedAsOf; không giả
requestedAsOf là effectiveAsOf server. Target nhập tay chưa dùng client.

## Snapshot phép tính target

CreateCalculated/TargetUseCases.CalculateAsync lưu immutable EnergyCalculation
cùng target trong diet_db: weight, measuredAt, sourceMetricId, requestedAsOf,
height, age, sex, activity factor, calculatedAt, BMR/TDEE. Mifflin-St Jeor dùng
số decimal; kết quả được lưu DB để đổi công thức tương lai không đổi target cũ.
Raw upsert cập nhật atomically và clear snapshot khi PUT target nhập tay.
POST /targets/me/calculate lấy profile Identity và weight Progress, nhận bốn field
mục tiêu như PUT; không nhận profile qua HTTP. Hệ số activity 1.2/1.375/1.55/1.725.
Timeout client trả 504 diet.weight_timeout; unavailable trả 503 diet.weight_unavailable.

## Meal AI adapter

IMealRecommendationProvider/MealRecommendationProvider codegen canonical proto
Recommendation (không import source service). Input nội bộ gồm profile đã resolve,
foods đã kiểm tra ownership, target/consumed macros và mealType/goal wire enums.
Provider loại foods allergens null, foods của user khác, tag dị ứng trùng chính xác;
không coi tags tự khai là chứng nhận y tế hoặc nhận diện alias tự động.
Map food ID về catalog, serving/nutrition lấy từ catalog; citation verified=false
được bảo toàn. Output lạ/thiếu citation/disclaimer -> 502; unavailable503,
deadline504. Deadline min(caller budget, now+2s), không retry. Diet join internal_net
để gọi Recommendation:50051 theo ADR001; không route Recommendation qua gateway.
GET /meals/ai-recommendations yêu cầu mealType/goal; dùng profile/cân nặng hiện tại,
summary UTC và tối đa 100 candidate có metadata thuộc user/catalog chung. Ngân sách
tổng 2.8 giây; thiếu metadata server cũ trả 503, không giả không dị ứng.
DTO HTTP riêng, giữ citation/disclaimer/degraded response từ Recommendation.
