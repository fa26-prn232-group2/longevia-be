# Frontend Guide — Identity Service

## 1. Tóm tắt

- **Feature**: `specs/bao-nguyen-001-identity-service/`
- **Yêu cầu**: `docs/requirements/prd.md` §2.1 và `specs/bao-nguyen-001-identity-service/spec.md`
- **Trạng thái handoff**: `READY_FOR_FRONTEND`
- **Canonical contract**: `docs/api/api-spec.md`
- **Phase 5 delta**: request và response `heightCm` đổi `integer` → `decimal`, giới hạn 50–250 cm và tối đa 2 chữ số thập phân; endpoint/tên field giữ nguyên. Count delta: route thêm/đổi/bỏ 0 · request field đổi kiểu 1 · response field đổi kiểu 1. `ProfileResponse` được dùng trực tiếp ở `PUT /users/me/profile` và lồng trong `GET /users/me`.
- **Count hiện tại đã đối chiếu source ↔ API spec**: 10 route · 19 request fields (18 body + 1 query) · 22 property trong 3 response DTO · 14 enum values · 10 HTTP status codes · 10 error codes.

## 2. Xác thực và hành vi

- API client-facing đi qua Nginx: `/api/identity/**`; service route bỏ prefix `/api/identity`.
- JWT bearer ký ES256, access token 15 phút. `/auth/logout` yêu cầu Bearer JWT.
- User mới ở trạng thái `PendingVerification`: login trả `401 identity.invalid_credentials`, không cấp token.
- Refresh token không tồn tại/hết hạn/revoked hoặc chủ tài khoản không `Active`: `401 identity.invalid_refresh_token`.
- Logout chỉ revoke refresh token thuộc user trong JWT; token lạ/thuộc user khác trả `401 identity.invalid_refresh_token` và không bị revoke.
- Đổi mật khẩu yêu cầu mật khẩu hiện tại và user đang Active; thành công revoke mọi refresh token của user. Access JWT stateless đã phát hành còn hạn tối đa 15 phút.
- Forgot-password nhận email và luôn trả `202 Accepted` không body, không tiết lộ email có account hay không. Email gửi tới account Active chứa mã reset 64 ký tự hex.
- Reset-password nhận mã và mật khẩu mới; mã lưu SHA-256, TTL 15 phút, dùng một lần. Thành công trả `204`; mã sai/hết hạn/đã dùng/bị thay thế trả `400 identity.invalid_password_reset_token` và không đổi mật khẩu/session.
- Request validation của Identity trả `422 identity.validation_failed`; tên field trong `errors` dùng camelCase theo JSON contract.
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

- DTO field Phase 5 đổi kiểu: request `UpdateProfileRequest.heightCm` và response `ProfileResponse.heightCm` integer → decimal; giới hạn 50.00–250.00cm, tối đa 2 chữ số thập phân.
- Enum values hiện tại: BiologicalSex 2 · ActivityLevel 4 · DietaryPreference 5 · UserStatus 3 (tổng 14); Phase 5 không thêm/bỏ value.
- Phase 4 request fields: change-password 2, forgot-password 1, reset-password 2 (tổng 5); response fields thêm: 0.
- Error codes: `identity.email_taken`, `identity.invalid_verification_token`, `identity.user_suspended`, `identity.invalid_credentials`, `identity.invalid_refresh_token`, `identity.unauthorized`, `identity.invalid_password_reset_token`, `identity.invalid_current_password`, `identity.user_not_found`, `identity.validation_failed` (10). Login pending dùng cùng `identity.invalid_credentials` để không tiết lộ account state; verify/reset token lỗi trả `400`; validation trả `422`.
- Tương thích: JSON field và route profile giữ nguyên; `heightCm` nay nhận/trả số thực. Client phải hỗ trợ số thập phân thay vì giả định integer. Các client cần xác minh email trước khi dùng login.

## 5. Bằng chứng và sync checklist

- Unit tests: `dotnet test src/Services/Identity/tests/Identity.UnitTests/Identity.UnitTests.csproj --no-restore`.
- Unit tests: 67 tests passed; Phase 5 profile coverage gồm biên height, precision, enums, allergies, age 13 và user ownership.
- Integration: 9 tests passed với PostgreSQL Testcontainers; xác nhận migration giữ số nguyên cũ, decimal round-trip, validation error contract và profile/outbox rollback. Registration E2E chạy PostgreSQL + RabbitMQ + Redis + Worker + Mailpit.
- [x] JSON client-facing DTO đối chiếu: 22 properties trong 3 response DTO; 19 request fields; Phase 5 đổi kiểu 1 request field và 1 response field.
- [x] Enum values đối chiếu constant ↔ API spec: tổng 14 value.
- [x] Route đối chiếu route map ↔ API spec: 10/10.
- [x] HTTP status/error đối chiếu handler ↔ middleware ↔ API spec: 10 status codes, 10 error codes; validation là 422 `identity.validation_failed`.
- [x] E2E lifecycle xác minh email và Mailpit chạy thành công (PostgreSQL + RabbitMQ + Worker + Mailpit Testcontainers)

Backend handoff có thể tiếp tục `IN_PROGRESS` khi consumer event chưa được xác định; việc này không chặn frontend handoff.
