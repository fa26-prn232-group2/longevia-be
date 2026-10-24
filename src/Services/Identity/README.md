# Identity Service

Identity là service client-facing qua Nginx và sở hữu `identity_db`. Service quản lý vòng đời tài khoản, xác thực JWT và hồ sơ người dùng. Schema được quản lý bằng EF Core Code First; migration của Identity được áp dụng trước khi API bắt đầu nhận request (mặc định bật, có thể tắt bằng `Database:AutoMigrate=false` khi môi trường deploy quản lý migration riêng).

## API

Các route được khai báo trong `Identity.Api/Endpoints/` và công khai qua Gateway với prefix `/api/identity`:

| Method | Route | Xác thực | Mục đích |
| --- | --- | --- | --- |
| `POST` | `/auth/register` | Public | Đăng ký tài khoản và gửi email xác minh qua event |
| `GET` | `/auth/verify?token=...` | Public | Xác minh email và kích hoạt tài khoản |
| `POST` | `/auth/login` | Public | Cấp access/refresh token cho tài khoản Active |
| `POST` | `/auth/refresh` | Public | Rotate refresh token |
| `POST` | `/auth/logout` | Bearer | Thu hồi refresh token của user hiện tại |
| `POST` | `/auth/change-password` | Bearer | Đổi mật khẩu và thu hồi refresh sessions |
| `POST` | `/auth/forgot-password` | Public | Yêu cầu email đặt lại mật khẩu, không tiết lộ trạng thái account |
| `POST` | `/auth/reset-password` | Public | Đặt mật khẩu mới bằng token dùng một lần |
| `GET` | `/users/me` | Bearer | Lấy thông tin tài khoản và profile |
| `PUT` | `/users/me/profile` | Bearer | Tạo hoặc cập nhật profile thể trạng |

Validation trả `422 identity.validation_failed`; lỗi nghiệp vụ trả RFC 7807 Problem Details theo [`docs/api/api-spec.md`](../../../docs/api/api-spec.md). Swagger được bật trong Development; service expose `/health` và `/metrics`.

## Event và ranh giới service

Identity publish các event versioned qua RabbitMQ và transactional outbox trong database riêng:

- `UserRegisteredEvent` → Worker gửi email xác minh.
- `PasswordResetRequestedEvent` → Worker gửi email đặt lại mật khẩu.
- `UserProfileUpdatedEvent` → không chứa dữ liệu sức khỏe; hiện chưa có consumer được xác định.

Identity không gọi SMTP trực tiếp và không truy cập database của service khác. Contract nằm trong [`contracts/events/v1/README.md`](../../../contracts/events/v1/README.md); kiến trúc tổng thể ở [`docs/system-design/architecture.md`](../../../docs/system-design/architecture.md).

## Cấu trúc

- `Identity.Api/`: Minimal API endpoints, middleware, filters và composition root.
- `Identity.Application/`: use case, validator, DTO, mapper và abstraction.
- `Identity.Domain/`: entity, quy tắc nghiệp vụ và repository abstraction.
- `Identity.Infrastructure/`: EF Core persistence/migrations và adapter RabbitMQ/outbox.
- `tests/Identity.UnitTests/`: unit tests domain/use case/validation.
- `tests/Identity.IntegrationTests/`: API, PostgreSQL, messaging và E2E bằng Testcontainers.

Các layer tuân theo `Api → Application → Domain`; Infrastructure implement abstraction của Application/Domain. Chi tiết xem [`docs/system-design/design-pattern.md`](../../../docs/system-design/design-pattern.md).

## Chạy và kiểm tra

Từ thư mục gốc repo, khởi tạo cấu hình môi trường theo [`docs/development/local-setup.md`](../../../docs/development/local-setup.md), khởi chạy hạ tầng cần thiết rồi chạy:

```powershell
dotnet run --project src/Services/Identity/Identity.Api
```

API mặc định kết nối PostgreSQL tại `localhost` theo cấu hình fallback của Infrastructure; cần cấu hình connection string Identity và JWT signing keys qua app configuration/environment trước khi dùng authentication. Test scoped và gate:

```powershell
make lint
make test PROJECTS=src/Services/Identity
make check
```

`make check` là close-out gate toàn repository trước khi push. Test integration cần Docker/Testcontainers; hướng dẫn chung ở [`docs/development/testing.md`](../../../docs/development/testing.md).
