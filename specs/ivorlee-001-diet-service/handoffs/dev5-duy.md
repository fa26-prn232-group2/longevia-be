# Gửi Duy — Background Worker và Gateway (dev 5)

Huy (dev 2) bổ sung Diet producer MealLoggedEvent v1 phục vụ Progress Streak.

## Hạ tầng cần đối chiếu

- Exchange topic longevia.events, routing key diet.meal.logged. Contract 6 field
  ở src/Shared/Events/MealLoggedEvent.cs. Không bind event này vào worker.email;
  Progress/Bảo sở hữu consumer và inbox/projector của mình.
- Diet thêm MassTransit EF bus outbox trong diet_db, migration AddMealActivityOutbox.
  Compose Diet thêm RabbitMQ config/dependency; không expose cổng Diet ra host,
  không thay route Progress hoặc cherry-pick code từ PR #11 đang mở.
- Thống nhất với Bảo durable queue progress.meal-activity, binding, finite retry,
  prefetch và DLQ/retention. Producer không tự tạo queue của Progress.
- Bootstrap phải bind queue trước rồi DIET_REPLAY_MEAL_ACTIVITIES=true khi start
  Diet. Queued count không chứng minh delivered. Sau drain/reconcile trả flag
  false. Giám sát broker/outbox/DLQ trước khi báo integration ready.

## JWT local và smoke

Huy đã sửa mapping JWT của Identity Compose: key container
Jwt__SigningPrivateKey/Jwt__SigningPublicKey/Jwt__KeyId, issuer/audience và RabbitMQ
credentials; tên .env bên ngoài giữ nguyên. Nhờ Duy/Nguyên review config này khi
merge. scripts/smoke/identity-diet-smoke.ps1 đã qua 6 assertion trên token thật:
register → RabbitMQ → Worker → Mailpit → verify → login → đọc/ghi Diet.
Không in key/token/password. Đây không phải bằng chứng missing-kid policy đã chốt.
SMTP local vẫn mailpit:1025, SSL=false; UI localhost:8025, Grafana localhost:3001.
Không relay email thử ra Brevo. Chốt policy missing kid/rotation với Nguyên/Bảo;
Progress hiện stricter hơn Shared. Huy không tự sửa Shared auth trong phần event.

## Weekly digest còn chờ

Worker có template gửi mail, chưa có contract báo cáo tuần. Cần Duy/Huy/Bảo chốt
producer schedule, timezone/week window, reportId, recipient/consent source,
summary payload, dedup/correction và DLQ. Diet/Progress tổng hợp nghiệp vụ;
Worker chỉ render/deliver, không query DB của service khác. Không coi template
hiện có là digest end-to-end.

Rollout nhiều instance: nâng tất cả Diet writer sang phiên bản có producer trước
khi snapshot replay; writer cũ không phát event có thể tạo khoảng trống sau snapshot.
Replay dùng một transaction/snapshot toàn lịch sử; cần đủ dung lượng outbox và
thời gian startup cho dữ liệu lớn, không chạy đồng thời nhiều replay không cần thiết.
