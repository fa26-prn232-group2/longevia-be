# Frontend Guide — Identity Service

## 1. Tóm tắt

- **Feature**: `specs/bao-nguyen-001-identity-service/`
- **Yêu cầu**: `docs/requirements/PRD.md` §2.1 và `specs/bao-nguyen-001-identity-service/spec.md`
- **Trạng thái handoff**: `IN_PROGRESS`
- **Canonical contract**: `docs/api/api-spec.md` (sẽ cập nhật cùng phase API)
- **Counts hiện tại của lát cắt auth**: route mới 0 · route đổi hành vi 3 · route bỏ 0 · DTO đổi 0

## 2. Xác thực và hành vi

- API client-facing đi qua Nginx: `/api/identity/**`; service route bỏ prefix `/api/identity`.
- JWT bearer ký ES256, access token 15 phút. `/auth/logout` yêu cầu Bearer JWT.
- User mới ở trạng thái `PendingVerification`: login trả `401 identity.invalid_credentials`, không cấp token.
- Refresh token không tồn tại/hết hạn/revoked hoặc chủ tài khoản không `Active`: `401 identity.invalid_refresh_token`.
- Logout chỉ revoke refresh token thuộc user trong JWT; token lạ/thuộc user khác trả `401 identity.invalid_refresh_token` và không bị revoke.
- Pagination/realtime/idempotency headers: `N/A` cho các endpoint Identity hiện tại.

## 3. Endpoint delta

| Method | Path | Trạng thái | Before → After |
|---|---|---|---|
| POST | `/api/identity/auth/login` | changed | PendingVerification trước đây được cấp token; nay bị từ chối bằng `401 identity.invalid_credentials`. |
| POST | `/api/identity/auth/refresh` | changed | Trước chỉ chặn Suspended; nay chỉ user `Active` được rotate token. |
| POST | `/api/identity/auth/logout` | changed | Trước có thể revoke token bất kỳ khi biết token; nay phải cùng user với JWT. |

Contract request/response của các route này chưa đổi. Các endpoint verify/change/forgot/reset trong spec sẽ được thêm vào bảng sau khi implementation và canonical API schema hoàn tất.

## 4. DTOs, enums, errors, compatibility

- DTO fields đổi: 0 trong lát cắt hiện tại.
- Enum values đổi: 0.
- Error: login pending dùng cùng `identity.invalid_credentials` để không tiết lộ account state; refresh/logout invalid token dùng `identity.invalid_refresh_token`.
- Tương thích: không đổi route, request hay success response; các client cần xác minh email trước khi dùng login.

## 5. Bằng chứng và sync checklist

- Unit tests: `dotnet test src/Services/Identity/tests/Identity.UnitTests/Identity.UnitTests.csproj --no-restore`.
- Integration test dùng PostgreSQL Testcontainers; cần Docker Engine đang hoạt động.
- [ ] Mọi field JSON client-facing đối chiếu DTO ↔ API spec (count: chưa chốt toàn feature)
- [ ] Enum values đối chiếu constant ↔ API spec hai chiều (count: chưa chốt toàn feature)
- [ ] Mọi route đối chiếu route map ↔ API spec (count: chưa chốt toàn feature)
- [ ] HTTP status/error đối chiếu handler ↔ middleware ↔ API spec (count: chưa chốt toàn feature)
- [ ] E2E lifecycle xác minh email và Mailpit chạy thành công

Không đặt `READY_FOR_FRONTEND` cho tới khi API contract, các endpoint dự kiến, test evidence và toàn bộ counts được đồng bộ.
