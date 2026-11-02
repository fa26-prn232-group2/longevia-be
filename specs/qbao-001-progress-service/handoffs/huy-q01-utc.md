# Bảo gửi Huy — xác nhận Q01 UTC MVP

Huy ơi, Bảo đã chốt Progress MVP dùng lịch UTC cho ngày server tự tính: water/today,
ngày thức dậy của sleep, ngày LoggedAt metric, today/streak và tuần bắt đầu thứ Hai
00:00 UTC. Không theo giờ máy hay ngày local Việt Nam; ví dụ 00:30 VN ngày 25/10
vẫn là ngày UTC 24/10.

Progress giữ nguyên `MealLoggedEvent.BusinessDate` Diet gửi, kể cả meal backdate và
replay; không đổi theo CreatedAt/OccurredOn. Frontend tự chọn ngày hiện tại khi tạo
meal nên gửi UTC date, còn người dùng chọn ngày lịch sử thì gửi đúng ngày họ chọn.
Không sửa dữ liệu lịch sử, không cần timezone Identity trong MVP.

Huy xác nhận giúp cách Diet/frontend hiểu ngày tự chọn như vậy có khớp không nha.
Nếu đang kỳ vọng local midnight, ghi rõ chỗ khác để hai bên chốt trước rollout.
Hiện Bảo đã approve nội bộ Progress, **chưa ghi nhận acknowledgment của Huy**.

Evidence isolated PR #12: actual publisher/outbox → RabbitMQ → Progress consumer/PG,
replay 102 meals và snapshot JSON reconciliation đã pass. Điều này không chứng minh
broker/outbox production đã drain hoặc lịch sử môi trường triển khai đã đầy đủ.
Hướng dẫn tái lập: [corrective-reconciliation](../corrective-reconciliation.md).
