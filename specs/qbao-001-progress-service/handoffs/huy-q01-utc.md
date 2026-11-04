# Bảo gửi Huy — triển khai Q01 UTC MVP đã được duyệt

Huy ơi, Bảo đã chốt Progress MVP dùng lịch UTC cho ngày server tự tính: water/today,
ngày thức dậy của sleep, ngày LoggedAt metric, today/streak và tuần bắt đầu thứ Hai
00:00 UTC. Không theo giờ máy hay ngày local Việt Nam; ví dụ 00:30 VN ngày 25/10
vẫn là ngày UTC 24/10.

Progress giữ nguyên `MealLoggedEvent.BusinessDate` Diet gửi, kể cả meal backdate và
replay; không đổi theo CreatedAt/OccurredOn. Frontend tự chọn ngày hiện tại khi tạo
meal nên gửi UTC date, còn người dùng chọn ngày lịch sử thì gửi đúng ngày họ chọn.
Không sửa dữ liệu lịch sử, không cần timezone Identity trong MVP.

Ngày 2026-11-04 Bảo đã thực hiện quyền quyết định Q01 do Huy ủy quyền và chốt
**Q01 — APPROVED FOR MVP CROSS-SERVICE UTC POLICY**, theo
[ADR005](../../../docs/decisions/005-mvp-utc-business-calendar.md).
Không ghi Huy trực tiếp ký ADR hoặc review PR. Huy phụ trách alignment Diet/frontend:
UI tự chọn hôm nay dùng UTC, ngày user chọn chủ động giữ nguyên. Việt Nam05/11
00:30(+07) vẫn04/11UTC;07:00(+07) mới sang05/11UTC. Kiểm tra/rollout frontend
vẫn là việc cần làm, nhưng Q01 approval không còn pending.

Evidence isolated PR #12: actual publisher/outbox → RabbitMQ → Progress consumer/PG,
replay 102 meals và snapshot JSON reconciliation đã pass. Điều này không chứng minh
broker/outbox production đã drain hoặc lịch sử môi trường triển khai đã đầy đủ.
Hướng dẫn tái lập: [corrective-reconciliation](../corrective-reconciliation.md).
