# Frontend Guide — Identity Service

## 1. Tóm tắt

- **Feature**: `specs/bao-nguyen-001-identity-service/`
- **Yêu cầu**: `docs/requirements/prd.md` §2.1 và `specs/bao-nguyen-001-identity-service/spec.md`
- **Trạng thái handoff**: `IN_PROGRESS`
- **Canonical contract**: `docs/api/api-spec.md`
- **Counts hiện tại của lát cắt auth**: route mới 4 · route đổi hành vi 3 · route bỏ 0 · request fields mới 5 · response fields đổi 0

## 2. Xác thực và hành vi

- API client-facing đi qua Nginx: `/api/identity/**`; service route bỏ prefix `/api/identity`.
- JWT bearer ký ES256, access token 15 phút. `/auth/logout` yêu cầu Bearer JWT.
- User mới ở trạng thái `PendingVerification`: login trả `401 identity.invalid_credentials`, không cấp token.
- Refresh token không tồn tại/hết hạn/revoked hoặc chủ tài khoản không `Active`: `401 identity.invalid_refresh_token`.
- Logout chỉ revoke refresh token thuộc user trong JWT; token lạ/thuộc user khác trả `401 identity.invalid_refresh_token` và không bị revoke.
- Đổi mật khẩu yêu cầu mật khẩu hiện tại và user đang Active; thành công revoke mọi refresh token của user. Access JWT stateless đã phát hành còn hạn tối đa 15 phút.
- Forgot-password nhận email và luôn trả `202 Accepted` không body, không tiết lộ email có account hay không. Email gửi tới account Active chứa mã reset 64 ký tự hex.
- Reset-password nhận mã và mật khẩu mới; mã lưu SHA-256, TTL 15 phút, dùng một lần. Thành công trả `204`; mã sai/hết hạn/đã dùng/bị thay thế trả `400 identity.invalid_password_reset_token` và không đổi mật khẩu/session.
- Pagination/realtime/idempotency headers: `N/A` cho các endpoint Identity hiện tại.

## 3. Endpoint delta

| Method | Path | Trạng thái | Before → After |
|---|---|---|---|
| POST | `/api/identity/auth/login` | changed | PendingVerification trước đây được cấp token; nay bị từ chối bằng `401 identity.invalid_credentials`. |
| POST | `/api/identity/auth/refresh` | changed | Trước chỉ chặn Suspended; nay chỉ user `Active` được rotate token. |
| POST | `/api/identity/auth/logout` | changed | Trước có thể revoke token bất kỳ khi biết token; nay phải cùng user với JWT. |
| GET | `/api/identity/auth/verify?token={token}` | added | Kích hoạt tài khoản pending bằng token một lần, hạn 24 giờ; thành công trả `204`, token sai/hết hạn/đã dùng trả `400 identity.invalid_verification_token`. |
| POST | `/api/identity/auth/change-password` | added | Bearer; kiểm tra mật khẩu hiện tại, đổi mật khẩu và revoke toàn bộ refresh token; trả `204`. |
| POST | `/api/identity/auth/forgot-password` | added | Public; luôn trả `202` không body; chỉ account Active phát event reset. |
| POST | `/api/identity/auth/reset-password` | added | Public; token + mật khẩu mới; thành công `204`, token không hợp lệ `400 identity.invalid_password_reset_token`. |

Route xác minh email dùng GET trực tiếp như link trong email Worker hiện có. Trình quét link email có thể tự kích hoạt tài khoản khi mở link; đây là hành vi đã được chọn.

## 4. DTOs, enums, errors, compatibility

- DTO fields đổi: 0 trong lát cắt hiện tại.
- Enum values đổi: 0.
- Phase 4 request fields: change-password 2, forgot-password 1, reset-password 2 (tổng 5); response fields thêm: 0.
- Error: login pending dùng cùng `identity.invalid_credentials` để không tiết lộ account state; refresh/logout invalid token dùng `identity.invalid_refresh_token`; verify token sai/hết hạn/đã dùng trả `400 identity.invalid_verification_token`; change password sai current password trả `400 identity.invalid_current_password`; reset token sai/hết hạn/đã dùng trả `400 identity.invalid_password_reset_token`.
- Tương thích: không đổi route, request hay success response; các client cần xác minh email trước khi dùng login.

## 5. Bằng chứng và sync checklist

- Unit tests: `dotnet test src/Services/Identity/tests/Identity.UnitTests/Identity.UnitTests.csproj --no-restore`.
- Integration: Identity PostgreSQL/RabbitMQ/Worker/Mailpit Testcontainers 6/6 passed; password-reset flow xác nhận mã email, expired/replaced/concurrent/replay, revoke refresh session và login bằng mật khẩu mới.
- [ ] Mọi field JSON client-facing đối chiếu DTO ↔ API spec (count: chưa chốt toàn feature)
- [ ] Enum values đối chiếu constant ↔ API spec hai chiều (count: chưa chốt toàn feature)
- [ ] Mọi route đối chiếu route map ↔ API spec (count: chưa chốt toàn feature)
- [ ] HTTP status/error đối chiếu handler ↔ middleware ↔ API spec (count: chưa chốt toàn feature)
- [x] E2E lifecycle xác minh email và Mailpit chạy thành công (PostgreSQL + RabbitMQ + Worker + Mailpit Testcontainers)

Không đặt `READY_FOR_FRONTEND` cho tới khi API contract, các endpoint dự kiến, test evidence và toàn bộ counts được đồng bộ.
