# Gửi Bảo — Progress Service (dev 3)

Huy (dev 2) bổ sung producer meal activity để gỡ phần Diet chặn Streak.

## Contract Diet cung cấp

Canonical: src/Shared/Events/MealLoggedEvent.cs, namespace Longevia.Shared.Events,
SchemaVersion = 1. Sáu field:

| Field | Kiểu | Ý nghĩa |
| --- | --- | --- |
| EventId | Guid | Bằng MealLogId; ổn định qua retry/replay |
| UserId | Guid | Chủ nhật ký, do JWT phía Diet xác định |
| MealLogId | Guid | Một lần tạo meal, khóa contribution |
| BusinessDate | DateOnly | Ngày ăn client đã cung cấp, không suy từ timestamp |
| CreatedAt | DateTimeOffset UTC | Thời điểm tạo log gốc |
| OccurredOn | DateTimeOffset UTC | Thời điểm enqueue lần này, có thể mới hơn khi replay |

SchemaVersion là constant trong contract, không phải property JSON thứ bảy.
Exchange topic longevia.events; routing key diet.meal.logged. Diet publish bằng
EF bus outbox cùng transaction log/items; HTTP trả 201 không có nghĩa Progress
đã nhận. Payload không có calorie/profile vì Streak không cần.

## Phần Bảo cần làm

- Bind durable queue riêng trước khi bật producer/replay; tên đề xuất
  progress.meal-activity. ConfigureConsumeTopology=false, bind topic/routing key
  nêu trên; retry hữu hạn và DLQ. Thống nhất topology với Duy.
- Trong cùng transaction progress_db: inbox dedup EventId + projection có unique
  MealLogId; nhiều log cùng BusinessDate vẫn một active day. Không lấy OccurredOn
  làm ngày ăn, không bỏ event backdate vì thời điểm nhận muộn.
- Tính Streak từ các business date; gộp với metric activity mà không double-count.
  Policy ngày metric/timezone vẫn cần Bảo/lead duyệt, Diet không áp UTC thay cho
  BusinessDate client gửi. GET summary không truyền date hiện dùng UTC, frontend
  muốn ngày local phải truyền date rõ.
- v1 create-only. Diet chưa có edit/delete/change-date. Nếu mở lifecycle đó, phải
  version/revision/tombstone và kế hoạch consumer mới; không tự giả là được hỗ trợ.

## Bootstrap meal cũ và rollout

1. Deploy/bind queue durable và inbox trước.
2. Bật DIET_REPLAY_MEAL_ACTIVITIES=true cho lần start Diet; producer đọc snapshot
   PostgreSQL RepeatableRead và enqueue mọi meal cũ qua outbox theo batch 100.
3. Log count là queued, không phải delivered. Chờ outbox/broker drain, kiểm tra
   projection đối chiếu meal ID/ngày; chỉ công bố lịch sử đủ khi reconcile xong.
4. Trả flag về false và recreate Diet. Retry replay an toàn chỉ khi inbox bền vững.
   Không dùng Redis TTL ngắn làm nguồn dedup duy nhất cho replay lịch sử.

Test phía Huy dùng PostgreSQL/RabbitMQ thật và receiver kiểm tra contract; chưa
phải consumer Progress. Cần E2E producer + inbox/projection thực của Bảo trước khi
báo Streak READY. Queue chưa tồn tại thì exchange không giữ event đã phát; phải
chạy replay sau khi queue bind.

## Phần Diet chờ Bảo

Weight provider gRPC: chốt ADR chiều Diet → Progress; canonical proto cho latest
weight/asOf. Đề xuất WeightKg + MeasuredAt + SourceMetricId + presence, loại future
measurement, tie-break ổn định, thiếu khác unavailable. Bảo sở hữu provider;
Huy sở hữu BMR/TDEE, snapshot target và adapter. Không bắt phần Streak chờ weight.

Rollout nhiều instance: nâng tất cả Diet writer sang phiên bản có producer trước
khi snapshot replay; writer cũ không phát event có thể tạo khoảng trống sau snapshot.
Replay dùng một transaction/snapshot toàn lịch sử; cần đủ dung lượng outbox và
thời gian startup cho dữ liệu lớn, không chạy đồng thời nhiều replay không cần thiết.

## Đã bổ sung bộ đối soát, không cần Bảo đọc diet_db

Huy cung cấp CLI --export-meal-activities <path>, xuất JSON snapshot v1 gồm toàn
bộ MealLogId/UserId/BusinessDate/CreatedAt trong một snapshot PostgreSQL, count
và SHA-256. Contract: contracts/diet/v1/meal-activity-snapshot.md. Verifier:
scripts/diet/verify-meal-activity-snapshot.ps1 -Path <file>.
Xem quickstart.md để chạy bằng compose volume vào artifacts/diet. File đã verify
là tập ID/ngày Huy cung cấp để Bảo đối soát; queued count không còn là bằng chứng
producer duy nhất. Bind queue trước export/replay; đối chiếu đủ snapshot IDs rồi
xử lý live events tiếp theo. capturedAtUtc chỉ audit, không dùng lọc event muộn.
Không có public download hoặc RPC mới; chỉ chuyển file qua operator được phép.
