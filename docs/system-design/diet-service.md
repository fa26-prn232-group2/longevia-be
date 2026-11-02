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
- Seed gồm ba món demo, ID cố định trong migration, không nhân bản khi startup.

## API và phụ thuộc

Sáu endpoint độc lập theo [API §3](../api/api-spec.md).
Target nhập tay có bmr/tdee null; thiếu target trả 404 diet.target_not_found.
Tích hợp BMR/TDEE và AI cần contract hồ sơ/cân nặng/dị ứng trước khi code.
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
