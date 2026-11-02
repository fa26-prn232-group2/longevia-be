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
