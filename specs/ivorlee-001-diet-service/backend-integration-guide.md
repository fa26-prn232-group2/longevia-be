# Hướng dẫn backend — Diet

Trạng thái: PROVIDER_IMPLEMENTED_PENDING_CONSUMER. Không đặt READY_FOR_BACKEND
cho toàn bộ Diet/Streak trước khi có consumer/projection và kiểm chứng với Bảo.

## Inventory

0 RPC mới / 0 message gRPC mới / 1 event mới / 6 event fields / 0 enum.
Event MealLoggedEvent v1 ở src/Shared/Events/MealLoggedEvent.cs, namespace
Longevia.Shared.Events. SchemaVersion=1 là constant, không phải property JSON.

| Field | Type | Semantics |
| --- | --- | --- |
| EventId | Guid | MealLogId, ổn định qua retry/replay |
| UserId | Guid | Chủ meal |
| MealLogId | Guid | Contribution key |
| BusinessDate | DateOnly | Ngày ăn client cung cấp |
| CreatedAt | DateTimeOffset UTC | Thời điểm log, precision microsecond |
| OccurredOn | DateTimeOffset UTC | Thời điểm enqueue hiện tại |

Producer Diet, consumer dự kiến Progress/Bảo. Topic longevia.events, routing key
diet.meal.logged; queue progress.meal-activity là đề xuất thuộc Bảo/Duy, Diet
không tự declare queue consumer. ConfigureConsumeTopology=false + explicit bind.

## Transaction và tính đúng

Meal/items/outbox lưu cùng transaction diet_db, rollback loại bỏ cả meal/event.
HTTP 201 không chứng minh consumer đã nhận. Broker unavailable không được làm mất
outbox đã commit. Nhiều meal cùng ngày phải dedup thành active day phía Progress;
consumer inbox cùng transaction projection, unique MealLogId, không chỉ cache TTL.
DateOnly khác UTC timestamp; không suy ngày nghiệp vụ từ OccurredOn/receive time.
Create-only v1; update/delete cần version/lifecycle design riêng.

## Bootstrap

Bind durable queue trước, sau đó DIET_REPLAY_MEAL_ACTIVITIES=true khi recreate
Diet. Startup sau migration/bus ready enqueue snapshot tất cả meal cũ theo batch
100 trong RepeatableRead transaction. Giữ EventId/ngày/CreatedAt qua replay. Đưa
flag về false sau lần chạy; queue drain + reconciliation phía consumer mới chứng
minh completeness. Mất queue hoặc thêm consumer mới phải replay lại sau bind.
Không có admin REST route mới hoặc gRPC chiều chưa được ADR duyệt.

## Phần chờ và bàn giao

Hồ sơ/cân nặng cho BMR/TDEE/AI còn thiếu contract provider/ADR; weekly digest chưa
có schema/scheduler. [Bốn file gửi từng dev](handoffs/README.md).
Diet không gửi email. Mailpit localhost:8025, Grafana localhost:3001.
Sáu HTTP operation/13 DTO giữ nguyên; metadata allergens thêm 2 field optional/
nullable, tổng 75 field. Xem frontend-guide.md.
Evidence sự kiện/rollback/replay và giới hạn: evidence.md.

Rollout nhiều instance: nâng tất cả Diet writer sang phiên bản có producer trước
khi snapshot replay; writer cũ không phát event có thể tạo khoảng trống sau snapshot.
Replay dùng một transaction/snapshot toàn lịch sử; cần đủ dung lượng outbox và
thời gian startup cho dữ liệu lớn, không chạy đồng thời nhiều replay không cần thiết.

Đường hợp lệ Identity → Diet đã kiểm chứng bằng token thật qua script
scripts/smoke/identity-diet-smoke.ps1 (6 assertion, Mailpit local). Compose đã map
đúng tên key options JWT. Không thay đổi policy missing kid của Shared.

## Snapshot đối soát do Diet cung cấp

CLI --export-meal-activities <path> đọc một snapshot PostgreSQL RepeatableRead,
xuất JSON v1 nguyên tử, không ghi đè, không start HTTP/bus. Contract operator ở
contracts/diet/v1/meal-activity-snapshot.md: 6 root fields, entry có 4 field;
không phải event hoặc RPC mới. File chứa danh sách MealLogId/UserId/BusinessDate/
CreatedAt cùng count/checksum. Verifier scripts/diet/verify-meal-activity-snapshot.ps1
xác nhận count, ID distinct, UTC/date và SHA-256 raw compact entry bytes + LF.
Bảo dùng bộ ID/ngày này để reconcile projection; capturedAtUtc không phải cursor,
không dùng chặn event đến muộn. Chỉ chuyển snapshot cho operator được phép.

Catalog allergens nullable đã có; không map unknown thành candidate an toàn hoặc
ngầm nhận diện alias. Candidate/profile provenance vẫn cần thống nhất trước AI.

## Q10 consumer Diet — 2026-11-02

Đã có IWeightProvider/ProgressWeightProvider, deadline 2s, không retry, lấy
immutable WeightSnapshot; request clamp về thời gian hiện tại phía Diet, server
vẫn là authority cutoff. Snapshot ghi requestedAsOf, không bịa effectiveAsOf
vì response proto không có field đó. Manual target không phát sinh dependency.
Counts proto canonical không đổi: 1 RPC / 3 messages / 7 fields, chỉ codegen client.
Chưa READY_FOR_BACKEND cho BMR: chưa có profile đầy đủ, calculate-target flow
và DB snapshot của phép tính. Tests client dùng CallInvoker, chưa chứng minh
HTTP/2 hoặc deployment E2E.

## Snapshot đã triển khai — 2026-11-02

Đã có factory calculated target, Mifflin-St Jeor và CalculateAsync nối IWeightProvider
với repository; profile là input nội bộ đã xác thực, chưa nhận từ REST client.
Migration AddTargetEnergySnapshot thêm nullable columns, giữ target cũ manual.
Lưu cả input và kết quả bất biến; manual PUT clear snapshot cùng atomic upsert.
Phần HTTP profile integration vẫn chờ Identity cung cấp đủ BirthDate/Sex/ActivityLevel.
Timeout -> 504 diet.weight_timeout; unavailable -> 503 diet.weight_unavailable;
missing -> null ở provider, CalculateAsync trả 422 diet.weight_not_found.

## Meal AI adapter — 2026-11-02

Đã thực hiện IMealRecommendationProvider/MealRecommendationProvider theo
GetMealRecommendations v1. Không sửa proto Đức. Mapping serving size dùng
InvariantCulture, decimal calories round AwayFromZero vào int32 wire; kết quả
REST-ready nội bộ lấy serving/nutrition gốc Diet, không lấy AI estimate để log.
Codegen file có các RPC khác nhưng adapter chỉ dùng GetMealRecommendations.
Input profile phải được xác thực và resolve trước; còn chờ Identity full profile.
Caller truyền deadline còn lại của request; adapter cap2s, chưa chứng minh SLO<3s
end-to-end. Không có profile/goal mặc định. Chỉ trong mạng nội bộ Compose.
Empty/capped response giữ safety note/disclaimer, không tạo food ID cho lời khuyên.
Provider trả 503 diet.ai_unavailable,504 diet.ai_timeout,502 diet.ai_integration_failed.
Chưa READY cho public meal-AI endpoint và chưa kiểm chứng server AI thật.

## Trạng thái hiện tại — 2026-11-02: profile và facade

Các ghi chú chưa có profile ở phase trước là lịch sử. Diet nay gọi
Identity.GetUserProfile theo ADR 004; codegen canonical proto gồm optional
field 5 dietary_preference và repeated field 6 allergies. Field 5 absent nghĩa
server cũ: BMR vẫn dùng bốn trường cũ, AI fail closed 503 diet.profile_incomplete.
Provider Identity thay đổi chỉ mapping hai trường đang có, không migration DB.

Diet → Identity: http://identity:50051, config IdentityGrpc:Address; 2 giây,
không retry, cancellation truyền xuyên. NOT_FOUND →422 diet.profile_not_found;
UNAVAILABLE →503 diet.profile_unavailable; DEADLINE_EXCEEDED →504
diet.profile_timeout; dữ liệu sai/lỗi còn lại →502 diet.profile_integration_failed.
Diet → Progress vẫn Q10 canonical, as-of/snapshot và lỗi riêng giữ nguyên.

POST calculate lưu inputs + BMR/TDEE cùng target; GET AI resolve profile/weight,
summary ngày UTC, candidate tối đa100 có metadata và đúng ownership; RPC
Recommendation nhận mealType/goal explicit. Các field calories wire int32 làm
tròn AwayFromZero; response còn lại decimal giữ summary chính xác. Tổng budget
AI 2.8s, từng RPC tối đa2s, hủy xuyên mọi await. Không tự retry hoặc fake metadata.
Response AI phải giữ citations/disclaimer/safetyNote, cho phép abstain rỗng.

Counted sync mới: Identity 2 RPC / 4 message / 9 field (height request1 + response1,
profile request1 + response6 =9 field tổng; riêng GetUserProfile 7 field request+response).
REST Diet 8 operation / 6 path / 16 DTO / 100 field (Swagger test).
Chưa đặt READY_FOR_BACKEND toàn feature khi rollout/live provider chưa kiểm chứng.
