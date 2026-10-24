# Đặc tả: Hoàn thiện Identity Service

**Nhánh**: `feat/identity-service`  
**Ngày tạo**: 2026-10-24  
**Trạng thái**: Draft — đang triển khai theo phase  
**Phạm vi**: sửa Identity theo requirements hiện hành; không thay đổi dữ liệu hay file thuộc feature khác.

## Bối cảnh và phát hiện gốc

Identity đã có đăng ký, login, refresh, logout và profile, nhưng các luồng chưa khép kín: tài khoản `PendingVerification` có thể login/refresh; event đăng ký có token nhưng Identity không lưu/tiêu thụ token và thiếu endpoint verify; logout không ràng buộc refresh token với chủ thể JWT; bootstrap EF migration không chạy mặc định; migration snapshot hiện không khớp model. Feature này hoàn thiện các luồng đó và các capability Identity đã được yêu cầu trong MVP.

## User stories

### US1 — Đăng ký và kích hoạt tài khoản (P1)

Là người dùng mới, tôi muốn đăng ký và xác minh email để kích hoạt tài khoản.

**Kiểm thử độc lập**: đăng ký tạo `PendingVerification`, ghi event vào outbox cùng transaction, login/refresh bị từ chối trước xác minh; token đúng và còn hạn kích hoạt đúng một lần; token sai/hết hạn/đã dùng không đổi trạng thái.

### US2 — Xác thực, refresh và logout an toàn (P1)

Là người dùng đã kích hoạt, tôi muốn đăng nhập, xoay refresh token và đăng xuất chỉ phiên của mình.

**Kiểm thử độc lập**: chỉ `Active` nhận JWT; refresh token hợp lệ được rotate một lần; token revoked/hết hạn và tài khoản không `Active` không dùng được; user A không thể revoke refresh token của user B.

### US3 — Hồ sơ thể trạng (P1)

Là người dùng đã xác thực, tôi muốn đọc/cập nhật hồ sơ nền để cá nhân hóa dinh dưỡng.

**Kiểm thử độc lập**: dữ liệu được lưu đúng user; tuổi từ 13 trở lên; chiều cao 50–250 cm; enum và allergies đúng contract. Kiểu chiều cao theo ví dụ API hiện hành là số thập phân.

### US4 — Đổi và đặt lại mật khẩu (P1)

Là người dùng, tôi muốn đổi mật khẩu khi đăng nhập hoặc đặt lại bằng email khi không truy cập được tài khoản.

**Kiểm thử độc lập**: mật khẩu mới tuân policy; token reset một lần và có hạn 15 phút; mọi refresh session cũ bị vô hiệu; access JWT stateless hết hạn tự nhiên trong tối đa 15 phút; request quên mật khẩu không tiết lộ email có tồn tại.

## Yêu cầu chức năng

- **FR-001** Chuẩn hóa email trước khi kiểm tra/tạo tài khoản và xử lý trùng email thành `409 identity.email_taken`, kể cả khi hai request đua nhau.
- **FR-002** Mật khẩu đăng ký/đổi/reset tối thiểu 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt (đồng nhất policy đã thực thi ở đăng ký); lưu bằng BCrypt/Argon2id, không log mật khẩu.
- **FR-003** Đăng ký tạo `PendingVerification`; lưu hash token xác minh và thời hạn 24 giờ; phát `UserRegisteredEvent` bằng transactional outbox trong cùng transaction.
- **FR-004** `GET /auth/verify?token=...` chỉ kích hoạt token hợp lệ, chưa hết hạn, chưa tiêu thụ; thao tác tiêu thụ token phải nguyên tử.
- **FR-005** Login chỉ phát token khi user `Active`; pending trả cùng lỗi xác thực chung để tránh lộ trạng thái account; suspended giữ lỗi bị khóa theo contract.
- **FR-006** Access token ES256 sống 15 phút, có `kid`; refresh token ngẫu nhiên sống 7 ngày, rotation/revoke đúng hạn; API trả `expiresIn: 900`.
- **FR-007** Refresh chỉ cấp cặp token mới cho token còn hạn/chưa revoke và user `Active`; mỗi refresh token chỉ rotate thành công một lần.
- **FR-008** Logout yêu cầu JWT và chỉ revoke refresh token thuộc đúng user trong JWT.
- **FR-009** Profile gồm ngày sinh, giới tính sinh học, chiều cao, activity level, dietary preference và allergies; ngày sinh phải đạt tối thiểu 13 tuổi; height 50–250 cm.
- **FR-010** Đổi mật khẩu cần mật khẩu hiện tại; forgot-password luôn trả phản hồi không tiết lộ email; reset token lưu SHA-256 hash, sống 15 phút, chỉ dùng một lần. Đổi/reset thành công revoke toàn bộ refresh token; access JWT stateless hết hạn tự nhiên trong tối đa 15 phút.
- **FR-011** Mọi email đi qua event RabbitMQ/outbox để Worker gửi; Identity không gọi SMTP/Brevo trực tiếp.
- **FR-012** Identity tự áp dụng migration của `identity_db` trước khi nhận request khi `Database:AutoMigrate` bật; mặc định bật theo local/development, cấu hình production có thể tắt có chủ đích; lỗi migration làm startup fail.
- **FR-013** Không trả `avatarUrl` khi chưa có yêu cầu/model/lifecycle avatar được duyệt; không thêm endpoint avatar trong feature này.
- **FR-014** Event liên service phải có contract version, transactional outbox và consumer idempotent; không phát event profile không có consumer/contract được chấp thuận.

## API dự kiến

Gateway prefix `/api/identity`; service định nghĩa route không có prefix.

| Method | Service route | Auth | Mục đích |
|---|---|---|---|
| POST | `/auth/register` | Public | Tạo tài khoản pending |
| GET  | `/auth/verify?token=` | Public | Kích hoạt tài khoản |
| POST | `/auth/login` | Public | Phát hành token cho user Active |
| POST | `/auth/refresh` | Public | Rotate token |
| POST | `/auth/logout` | Bearer | Revoke token của chính user |
| POST | `/auth/change-password` | Bearer | Đổi mật khẩu hiện tại |
| POST | `/auth/forgot-password` | Public | Yêu cầu email reset, phản hồi không tiết lộ account |
| POST | `/auth/reset-password` | Public | Đặt mật khẩu bằng token |
| GET | `/users/me` | Bearer | Lấy user hiện tại |
| PUT | `/users/me/profile` | Bearer | Cập nhật profile |

Các route mật khẩu được đưa vào phạm vi dựa trên MVP và event/template reset đã có; API spec hiện tại chưa khai báo chúng. Trước khi mở API contract, plan phải cập nhật canonical API spec và frontend guide.

## Edge cases

- Email trùng do race-condition vẫn trả 409, không thành 500.
- Verify/reset token sai, hết hạn, đã dùng hoặc đã thay thế không kích hoạt/đổi mật khẩu.
- User pending/suspended không login; refresh của user không Active bị từ chối.
- Hai refresh/reset request đồng thời không được dùng cùng token hai lần.
- Logout token lạ/thuộc user khác không bị revoke.
- Request forgot-password cho email không tồn tại vẫn không xác nhận account có/không.
- Database trống được migrate trước khi API phục vụ; migration lỗi không chạy service tiếp.
- Không log token, mật khẩu, private key hay payload nhạy cảm.

## Tiêu chí thành công

- **SC-001**: 100% test state machine xác nhận chỉ `Active` login/refresh.
- **SC-002**: 100% test chứng minh token verify/reset một lần, đúng hạn và lưu hash.
- **SC-003**: PostgreSQL + RabbitMQ thật chứng minh đăng ký commit user/outbox, Worker gửi email tới Mailpit và verify kích hoạt user.
- **SC-004**: Refresh token của user khác không thể bị logout; token đã rotate/revoke không dùng lại được.
- **SC-005**: Identity khởi động trên DB trống bằng migration; `make lint` và test scoped xanh; `make check` xanh trước push.
- **SC-006**: API spec, frontend guide, event contract, code và tests đồng bộ; guide chỉ `READY_FOR_FRONTEND`/`READY_FOR_BACKEND` sau counted sync.

## Quyết định và giả định phạm vi

- Chiều cao dùng decimal theo `docs/api/api-spec.md` (ví dụ `175.5`); cần migration từ integer và tương thích dữ liệu.
- Avatar bị loại khỏi response cho tới khi có yêu cầu, entity và luồng cập nhật chính thức; API sample hiện tại là bằng chứng không nhất quán, không đủ để tạo tính năng avatar.
- Đổi mật khẩu thuộc MVP; quên/reset thuộc luồng email đã được PRD/Worker đặc tả. Endpoint semantics sẽ được ghi canonical trong API spec trước implementation.
- `UpdateProfileUseCase` hiện publish `UserProfileUpdatedIntegrationEvent` (2 fields), nhưng đây chưa phải event contract dùng chung và chưa có consumer được xác định. Phase 5 phải đối chiếu Constitution/US3: bỏ publication nếu không có consumer được duyệt, hoặc chuẩn hóa contract versioned và consumer compatibility nếu có; profile update vẫn lưu dữ liệu.
- Không có gRPC hay cross-database call trong feature.
