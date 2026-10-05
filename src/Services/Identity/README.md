# Identity Service

Service client-facing (qua Nginx). Sở hữu `identity_db`: đăng ký/đăng nhập, hồ sơ
người dùng, phát hành JWT.

> Đây là **service mẫu** — hiện chỉ có cấu trúc project + stub `Program.cs`
> (Swagger + `/health`). Nghiệp vụ và endpoint sẽ được thêm dần.

## Cấu trúc & vai trò từng layer

```
Identity.Api/              Presentation: host, endpoint, filter, middleware, DI
├── Endpoints/             nhóm endpoint theo feature
├── Extensions/            đăng ký DI, Swagger, options
└── Middlewares/           exception handler, correlation id

Identity.Application/      Use case / orchestration (không biết EF/HTTP)
├── Abstractions/          interface dùng bởi use case (repository, provider...)
├── Common/Dtos/           request/response DTO
├── Common/Mappers/        entity ⇄ DTO
├── Common/Validators/     FluentValidation
├── Common/Errors/         lỗi domain (compose AppException)
└── Features/<Feature>/    use case theo feature: Auth, Users, Registration

Identity.Domain/           Nghiệp vụ thuần, zero dependency framework
├── Entities/              thực thể
├── ValueObjects/
├── Constants/             enum/const của domain
├── Events/                domain event
└── Repositories/          interface repository

Identity.Infrastructure/   Chi tiết kỹ thuật (impl interface của trên)
├── Persistence/Configurations/   EF Core entity config
├── Persistence/Repositories/     EF Core repository
├── Messaging/             MassTransit consumer/publisher, outbox
└── Adapters/              gRPC client, provider ngoài

tests/
├── Identity.UnitTests/         test use case/domain
└── Identity.IntegrationTests/  test API + DB (Testcontainers)
```

## Quy ước

- Phụ thuộc một chiều: `Api → Application → Domain`, `Infrastructure → Application/Domain`.
- Domain không tham chiếu framework; Application không chạm EF/MassTransit trực tiếp.
- Chi tiết pattern: [`docs/system-design/design-pattern.md`](../../../docs/system-design/design-pattern.md).

## Chạy

```powershell
dotnet run --project src/Services/Identity/Identity.Api
# Swagger: http://localhost:<port>/swagger  (Development)
```
