# Adapter & Composition

## Adapter cho dịch vụ ngoài

Mọi dịch vụ ngoài MUST được bọc sau một interface trong Application; implementation
nằm ở Infrastructure.

| Dịch vụ ngoài | Dùng cho | Ai gọi |
| --- | --- | --- |
| Cloudinary | Lưu media | Service cần media (Signed Upload từ client) |
| Google AI | Sinh gợi ý | Recommendation |
| Brevo | Gửi email | Background Worker |

Quy tắc:
- Application/Domain chỉ biết interface (ví dụ `IMediaStore`, `IAiProvider`,
  `IEmailSender`), không biết SDK/HTTP chi tiết.
- Adapter chịu trách nhiệm retry/timeout/circuit-breaker cho lời gọi ngoài.
- Không rải lời gọi provider trực tiếp khắp domain.
- Secret của provider lấy từ biến môi trường, không hardcode.

## Composition root

- Mỗi service có một `Program.cs` là composition root duy nhất: đăng ký DI,
  cấu hình, middleware, endpoint.
- Đăng ký theo nhóm: infrastructure (EF Core, RabbitMQ), application (use case),
  presentation (controllers/endpoints/consumers).
- Không resolve service bằng service locator trong code nghiệp vụ.

## Cấu hình

- Đọc từ `IConfiguration`/options pattern; bind vào strongly-typed options và
  validate khi khởi động (fail fast).
- Connection string, RabbitMQ, JWT, provider key đều từ env/secret.

## Liên quan

- [../design-pattern.md](../design-pattern.md)
- [../../development/local-setup.md](../../development/local-setup.md)
