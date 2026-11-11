# Event-Driven Design (RabbitMQ)

Sự kiện bất đồng bộ giữa các service đi qua **RabbitMQ**. Producer publish event;
consumer xử lý idempotent qua contract.

## Topology

- **Exchange**: `topic` cho nghiệp vụ (routing key có cấu trúc), `fanout` khi mọi
  consumer đều cần, `direct` cho định tuyến điểm-điểm.
- **Queue**: mỗi consumer một queue, đặt tên `<service>.<purpose>`; bind vào
  exchange bằng routing key cụ thể.
- **Routing key** theo dạng `<domain>.<entity>.<action>` (ví dụ
  `identity.user.created`).
- **Dead-letter**: queue có DLX để bắt event xử lý lỗi; không retry vô hạn.
- **Prefetch** giới hạn để tránh một consumer ngốn hết.

Topology khai báo tường minh (code hoặc script), không dựa vào auto-declare ngầm.

## Transactional Outbox

Producer MUST publish event qua outbox:

1. Trong **cùng transaction** với thay đổi dữ liệu, ghi bản ghi outbox.
2. Một tiến trình nền đọc outbox và publish lên RabbitMQ.
3. Đánh dấu đã publish sau khi broker xác nhận.

MUST NOT publish trực tiếp "sau khi commit" ở đường đi rời rạc — mất event khi
process chết giữa commit và publish.

## Idempotency

- Consumer MUST xử lý trùng event mà không tạo hiệu ứng phụ lần hai (lưu event id
  đã xử lý, hoặc thao tác có tính lũy đẳng).
- Claim dùng để khóa xử lý có thể được tạo trước side effect, nhưng chỉ được giữ
  lại sau khi side effect thành công. Nếu xử lý lỗi và broker sẽ retry, consumer
  MUST giải phóng claim trước khi ném lỗi; nếu không, lần retry có thể bị nhận
  nhầm là event đã xử lý và bị bỏ qua.
- Consumer MUST bỏ qua an toàn field lạ khi contract thêm field mới.

## Thứ tự & đồng hồ

Timestamp mang ý nghĩa thứ tự MUST được đóng tại **thời điểm xử lý**, không phải
thời điểm tạo/đăng ký bản ghi. Timestamp "tạo lúc đăng ký" luôn cũ hơn mốc consumer
đã xử lý → bị coi là stale.

## Contract event

- Event schema có **version**; producer và consumer chia sẻ contract.
- Thay đổi tương thích ngược (thêm field optional) được khuyến khích; thay đổi phá
  vỡ phải version mới.
- Chi tiết ranh giới: [contract-purity-pattern.md](contract-purity-pattern.md).

## Background Worker

- Là consumer hạ tầng dùng chung; MUST NOT chứa nghiệp vụ riêng của một service.
- Ví dụ hiện tại: consume event → gửi email qua **Brevo** (adapter).

## Liên quan

- [microservices/02-grpc-and-events.md](microservices/02-grpc-and-events.md)
- [contract-purity-pattern.md](contract-purity-pattern.md)

## Cập nhật 2026-11-11 - MealLogRemovedEvent

- Diet phát `MealLogRemovedEvent` (routing key `diet.meal.removed`) khi người dùng xoá bữa; publish trong cùng transaction qua EF outbox.
- Progress dùng chung queue `progress.meal-activity`, thêm consumer xoá `ActivityContribution` và tombstone `processed_meal_removals`.
- Một `MealLoggedEvent` đến trễ cho cùng `MealLogId` bị bỏ qua để không hồi sinh streak (out-of-order safe).
- Đã có sửa bữa: `PUT /diet/meals` thay thế trong một transaction, phát removal cho log cũ và logged cho log mới.

## Cập nhật 2026-11-11 - Đổi email

- Identity phát `EmailChangeRequestedEvent` (routing key `identity.user.email-change-requested`)
  khi lưu `PendingEmail`; Worker queue `worker.email` gửi link xác nhận tới địa chỉ mới và
  email cảnh báo tới địa chỉ cũ.
- Sau khi xác nhận, Identity đổi email, thu hồi toàn bộ refresh token và phát
  `EmailChangedEvent` (routing key `identity.user.email-changed`); Worker gửi thông báo
  hoàn tất tới địa chỉ cũ.
- Idempotency: consumer email-change dedup theo hash token (7 ngày) hoặc `UserId + OccurredOn`;
  claim được giải phóng nếu gửi email lỗi để broker retry.

