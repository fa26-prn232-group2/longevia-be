# Gửi Duy — Background Worker và Gateway (dev 5)

Huy (dev 2) bổ sung Diet producer MealLoggedEvent v1 phục vụ Progress Streak.

## Hạ tầng cần đối chiếu

- Exchange topic longevia.events, routing key diet.meal.logged. Contract 6 field
  ở src/Shared/Events/MealLoggedEvent.cs. Không bind event này vào worker.email;
  Progress/Bảo sở hữu consumer và inbox/projector của mình.
- Diet thêm MassTransit EF bus outbox trong diet_db, migration AddMealActivityOutbox.
  Compose Diet thêm RabbitMQ config/dependency; không expose cổng Diet ra host,
  giữ cấu hình Progress foundation đã merge PR #11; PR #13 business capabilities
  của Bảo còn draft, không tự đưa code draft vào rollout.
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
Không in key/token/password. Smoke đường hợp lệ không chứng minh Shared Auth
đã thực hiện policy strict.
SMTP local vẫn mailpit:1025, SSL=false; UI localhost:8025, Grafana localhost:3001.
Không relay email thử ra Brevo. Q08-A đã được Nguyên/Bảo đồng ý kid hợp lệ bắt
buộc, sub Guid khác rỗng và fail-start khi thiếu public key; Nguyên làm Shared Auth
remediation trong PR riêng. Nhờ Duy review cấu hình và phối hợp test rotation/
rollout sau PR đó, không hỏi lại quyết định đã approved.

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

## Nhờ Duy phản hồi bàn giao

Review thay đổi Compose của Diet trong PR #12; cùng Bảo xác nhận queue/binding,
retry/DLQ, bước bind → replay → drain → snapshot reconciliation → tắt replay.
Phản hồi cấu hình/PR triển khai và checklist vận hành; không đưa logic Streak vào
Worker. Với digest, phản hồi contract/schedule/recipient còn thiếu để nhóm chốt
trước khi Huy cung cấp nutrition summary.

## Trạng thái mới nhất — 2026-11-02

Diet có POST /targets/me/calculate và GET /meals/ai-recommendations, vẫn cùng
prefix Gateway /api/diet; không thêm route Recommendation ra Nginx. Diet join
internal_net để gọi Recommendation, dùng edge/data_net hiện có để gọi Identity
và Progress; default ports nội bộ50051, không publish host. Nhờ Duy review
compose/network và deploy Identity có hai field dietary/allergies trước AI.
Local SMTP vẫn Mailpit, không gọi Brevo thật. Phối hợp Bảo rollout/reconcile
broker; không coi provider Q10/meal consumer còn thiếu. Digest vẫn scope riêng.

### Lỗi glob artifact phát hiện khi chạy test

Nhờ Duy xem Worker.csproj: hiện chỉ Compile Remove="tests/**/*.cs", Web SDK
vẫn có thể đưa tests/**/bin/** và obj/** vào Content và copy lặp sang output
Identity/Probe qua project reference. Log local có MSB3026 với đường dẫn tests/
Worker.UnitTests/bin lồng nhiều cấp; build sau restore vẫn qua, không phải lỗi
Diet runtime. Đề nghị loại tests/** khỏi Content/None của Worker và kiểm chứng
build/publish không chứa test output. Huy chưa đổi project Worker trong phase này.
