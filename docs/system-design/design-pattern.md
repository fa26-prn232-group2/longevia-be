# Design Patterns & Conventions

Tài liệu này quy định **cách viết code** trong từng service: layering, pattern,
convention theo tên, và các thành phần dùng chung. Quy tắc kiến trúc (ranh giới
service, contract, event) nằm ở [`architecture.md`](architecture.md) và
[`constitution.md`](../../.specify/memory/constitution.md).

> Structural limits (độ dài file/hàm, một-type-một-file) ở
> [`../development/code-hygiene.md`](../development/code-hygiene.md) — tài liệu
> **duy nhất** cho ngưỡng cấu trúc. Tài liệu này quy định *viết thế nào*; tài liệu
> kia quy định *giới hạn mà code phải nằm trong*.

---

## I. Kiến trúc layer

Mỗi service là một ASP.NET Core app độc lập, tổ chức **Clean Architecture** với
phụ thuộc hướng vào trong:

```
      ┌──────────────────────────┐
      │ Api (Presentation)       │  endpoint/filter/middleware, composition root
      ├──────────────────────────┤
      │ Application              │  use case, DTO, validator, mapper, abstraction
      ├──────────────────────────┤
      │ Domain                   │  entity, value object, constant, domain event
      ├──────────────────────────┤
      │ Infrastructure           │  EF Core, MassTransit, adapter ngoài
      └──────────────────────────┘

  Api ──► Application ──► Domain
                 ▲
                 └──── Infrastructure (implements interfaces of Application/Domain)

  Dependency rule: mọi mũi tên trỏ vào trong. Domain không biết gì bên ngoài.
```

**Ràng buộc bất biến:**

- `Domain` MUST NOT reference `Application`, `Infrastructure`, hay framework
  (EF Core, ASP.NET Core). Domain là C# thuần.
- `Application` định nghĩa **interface** cho thế giới bên ngoài (repository,
  publisher, provider) và chỉ phụ thuộc `Domain`.
- `Infrastructure` implements các interface đó; là nơi **duy nhất** chạm EF Core,
  MassTransit, HTTP client.
- `Api` mỏng: gắn DI, middleware, endpoint; không chứa nghiệp vụ.

---

## II. Cấu trúc project mỗi service

Mỗi service gồm các project sau (xem service mẫu `src/Services/Identity/`):

```
src/Services/<Service>/
├── <Service>.Domain/            # entity, VO, constant, domain event, repo interface
├── <Service>.Application/       # use case, DTO, validator, mapper, abstraction
│   ├── Abstractions/            # interface dùng bởi Application
│   ├── Common/{Dtos,Mappers,Validators,Errors}/
│   └── Features/<Feature>/      # use case theo feature
├── <Service>.Infrastructure/    # EF Core, messaging, adapter
│   ├── Persistence/{Configurations,Repositories}/
│   ├── Messaging/
│   └── Adapters/
├── <Service>.Api/               # host, endpoint, filter, middleware
│   ├── Endpoints/
│   ├── Extensions/
│   └── Middlewares/
└── tests/
    ├── <Service>.UnitTests/
    └── <Service>.IntegrationTests/
```

Không phải thư mục nào cũng bắt buộc. `Application/Features`,
`Domain/Repositories` (hoặc `Application/Abstractions`), `Infrastructure/Persistence`,
`Api/Endpoints` có mặt ở mọi service.

---

## III. Error Pattern

Lỗi nghiệp vụ dùng **exception có kiểu**, được map tập trung ở một chỗ thành
Problem Details. Không service nào tự `return Results.Problem(...)` rải rác.

### 3.1 Kiểu lỗi

```csharp
public sealed class AppException : Exception
{
    public string Code { get; }          // mã ổn định, ví dụ "identity.user_not_found"
    public int Status { get; }           // HTTP status
    public string Title { get; }         // tiêu đề ngắn
    public IReadOnlyList<FieldError>? Errors { get; } // lỗi theo field (validation)

    // Factory — HTTP status + tiêu đề nhất quán
    public static AppException BadRequest(string detail, ...);
    public static AppException Unauthorized(string detail, ...);
    public static AppException Forbidden(string detail, ...);
    public static AppException NotFound(string detail, ...);
    public static AppException Conflict(string detail, ...);
    public static AppException Unprocessable(string detail, ...);
    public static AppException Internal(string detail, ...);
    public static AppException ServiceUnavailable(string detail, ...);
}

public sealed record FieldError(string Field, string Message);
```

- `Code` là **hợp đồng ổn định** cho client; không đổi giá trị đã phát hành.
- `Detail` là thông điệp người dùng thấy được (tiếng Việt), **không** lộ chi tiết
  kỹ thuật hay lỗi DB.

### 3.2 Lỗi theo domain

Mỗi feature khai báo factory riêng trong `Application/Common/Errors/`, compose từ
factory chung:

```csharp
public static class UserErrors
{
    public static AppException NotFound(Guid id) =>
        AppException.NotFound($"Không tìm thấy người dùng {id}.") with { Code = "identity.user_not_found" };

    public static AppException EmailTaken(string email) =>
        AppException.Conflict($"Email {email} đã được sử dụng.") with { Code = "identity.email_taken" };
}
```

### 3.3 Nơi ném lỗi

- **Use case** ném `AppException` (qua factory domain), không ném từ endpoint hay
  repository.
- Repository trả lỗi kỹ thuật thô; use case dịch sang lỗi domain.
- **Không bao giờ** để lỗi DB (Npgsql/EF) rò ra response.

### 3.4 Global exception handler

Đăng ký một `IExceptionHandler` (hoặc middleware) duy nhất:

1. Bắt mọi exception.
2. `AppException` → map nguyên `Status`/`Title`/`Code`/`Errors`.
3. Lỗi khác → `500` với `Code = "system.internal"`; log đầy đủ (stack), trả về
   thông điệp chung.
4. Gắn `traceId`; trong Development có thể kèm stack, **production thì không**.

```csharp
try { await next(context); }
catch (AppException ex) { await WriteProblem(context, ex); }
catch (Exception ex)    { logger.LogError(ex, "Unhandled"); await WriteProblem(context, AppException.Internal("Lỗi hệ thống.")); }
```

Mã HTTP dùng theo [`../api/README.md`](../api/README.md).

---

## IV. Validation Pattern

### 4.1 FluentValidation

Mỗi request có một validator riêng, đặt cạnh DTO trong
`Application/Common/Validators/` (hoặc trong `Features/<Feature>/`):

```csharp
public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]");
    }
}
```

### 4.2 Auto-validation

Validate tự động trước khi vào endpoint bằng một endpoint filter:

```csharp
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var validator = ctx.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is null) return await next(ctx);
        var result = await validator.ValidateAsync((TRequest)ctx.Arguments[0]!);
        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => new FieldError(e.PropertyName, e.ErrorMessage)).ToList();
            throw AppException.BadRequest("Dữ liệu không hợp lệ.") with { Errors = errors, Code = "validation.failed" };
        }
        return await next(ctx);
    }
}
```

Validator được đăng ký một lần: `services.AddValidatorsFromAssembly(...)`.

### 4.3 Quy tắc

- Validate ở **boundary** (request), không validate lại trong domain một cách dư thừa.
- Domain vẫn bảo vệ invariant của chính nó (fail fast, ném lỗi domain).
- Thông điệp lỗi tiếng Việt, gắn `label`/tên field rõ ràng.

---

## V. Response & Result Pattern

- **Thành công**: trả DTO trực tiếp; bọc `Results.Ok(res)` / `TypedResults.Ok(res)`.
- **Tạo mới**: `Results.Created(uri, res)`.
- **Không có nội dung**: `Results.NoContent()`.
- **Lỗi**: ném `AppException` (mục III) — KHÔNG tự dựng ProblemDetails trong endpoint.
- Thời gian ISO 8601 UTC; JSON camelCase.

Không dùng một generic `ApiResponse<T>` bọc mọi thứ: HTTP status + Problem Details
đã mang ngữ nghĩa. Nếu cần metadata, gói trong DTO (`PagedResult<T>`).

---

## VI. Pagination Pattern

Dùng generic, tái sử dụng ở `Longevia.Shared`:

```csharp
public readonly record struct PaginationQuery(int Page, int PageSize)
{
    public const int MaxPageSize = 100;
    public PaginationQuery Normalized() =>
        new(Math.Max(Page, 1), Math.Clamp(PageSize, 1, MaxPageSize));
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
```

Repository trả `PagedResult<T>`; helper EF:

```csharp
public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
    this IQueryable<T> query, PaginationQuery p, CancellationToken ct)
{
    var n = p.Normalized();
    var total = await query.LongCountAsync(ct);
    var items = await query.Skip((n.Page - 1) * n.PageSize).Take(n.PageSize).ToListAsync(ct);
    return new PagedResult<T>(items, n.Page, n.PageSize, total);
}
```

Endpoint **luôn** Count trước Skip/Take; không trả `TotalItems = -1`.

---

## VII. Endpoint Pattern

Dùng **Minimal API endpoint group** theo feature. Endpoint mỏng:

```csharp
public static class UserEndpoints
{
    private const string GetUserSuccess = "Lấy thông tin người dùng thành công";

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, GetUserUseCase useCase, CancellationToken ct) =>
                TypedResults.Ok(await useCase.ExecuteAsync(id, ct)))
            .AddEndpointFilter<ValidationFilter<GetUserRequest>>()
            .WithName("GetUser");

        return app;
    }
}
```

Quy tắc:

- Endpoint chỉ: bind/validate → gọi use case → trả kết quả. Không nghiệp vụ.
- Message chuỗi cố định khai báo `const` ở đầu file, không hard-code trong lambda.
- Không `try/catch` trong endpoint — để global handler lo.
- Trả kiểu cụ thể (`TypedResults.Ok`) để Swagger suy ra schema.

> Controller (`[ApiController]`) chỉ được dùng nếu **cả service** thống nhất dùng
> controller; không trộn controller và minimal API trong cùng một service.

---

## VIII. Mapper Pattern

- Mapper là **hàm thuần**, đặt ở `Application/Common/Mappers/` (hoặc theo feature).
- Chuyển entity ⇄ DTO; không chứa truy cập DB.
- Tách file theo nhóm nghiệp vụ khi dài; tên `XxxMapper`.

```csharp
public static class UserMapper
{
    public static UserResponse ToResponse(this User user) =>
        new(user.Id, user.Email, user.DisplayName, user.CreatedAt);
}
```

- Mapster/AutoMapper cho phép nếu dùng nhất quán, nhưng map thủ công được ưu tiên
  để kiểm soát field nào lộ ra ngoài.
- **Không** map entity domain ra ngoài API/contract (mục XI).

---

## IX. Repository & Generic Repository

### 9.1 Interface (Domain hoặc Application/Abstractions)

```csharp
public interface IReadRepository<T, in TId> where T : class
{
    Task<T?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
    IQueryable<T> Query(); // cho phép compose truy vấn ở use case
}

public interface IRepository<T, in TId> : IReadRepository<T, TId> where T : class
{
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
```

### 9.2 Implementation (Infrastructure/Persistence/Repositories)

```csharp
public class Repository<T, TId> : IRepository<T, TId> where T : class
{
    protected readonly AppDbContext Db;
    protected readonly DbSet<T> Set;
    public Repository(AppDbContext db) { Db = db; Set = db.Set<T>(); }
    // ...
}
```

### 9.3 Repository của domain

```csharp
public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
}

public sealed class UserRepository : Repository<User, Guid>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db) { }
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(u => u.Email == email, ct);
}
```

Quy tắc:

- Interface ở `Domain` (hoặc `Application/Abstractions`), implementation ở
  `Infrastructure/Persistence/Repositories/`.
- Repository chỉ chạm **database của service mình**. `.Include`/`.Join` sang
  entity của service khác là **cấm**.
- Không trả entity ra khỏi use case; map sang DTO.

---

## X. Unit of Work Pattern

`DbContext` của EF Core đã là một unit of work; bọc nó sau một abstraction để use
case điều khiển transaction mà không biết EF.

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default);
}
```

```csharp
public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default) =>
        await db.Database.BeginTransactionAsync(ct);
}
```

Dùng trong use case cần nhiều thao tác ghi nguyên tử **+ outbox**:

```csharp
await using var tx = await uow.BeginTransactionAsync(ct);
await repoA.AddAsync(entityA, ct);
await publisher.Publish(new SomethingHappened(...), ct); // ghi vào outbox, cùng transaction
await uow.SaveChangesAsync(ct);
await tx.CommitAsync();
```

- Chỉ gọi `SaveChangesAsync` ở một nơi (use case hoặc UoW), không rải khắp repo.
- Event publish nằm **trong** transaction (outbox) — xem mục XV.

---

## XI. DTO Convention

Có **ba tầng shape** dữ liệu, không được trộn:

```
HTTP client  ↔  Request / Response     (Application/Common/Dtos hoặc Features/<Feature>/Dtos)
Service khác ↔  Contract DTO           (Application/Contracts, chỉ dữ liệu thuần)
Repository   ↔  Read model / Filter    (Domain/Repositories)
```

### Naming

| Vai trò | Pattern | Ví dụ |
| --- | --- | --- |
| Request body | `<Verb>XxxRequest` | `RegisterUserRequest` |
| Query param | `GetXxxQuery` | `GetUsersQuery` |
| Response chi tiết | `XxxResponse` | `UserResponse` |
| Response rút gọn | `XxxBriefResponse` | `UserBriefResponse` |
| Danh sách phân trang | `PagedResult<XxxResponse>` | `PagedResult<UserResponse>` |
| Contract liên service | `XxxContract` | `UserContract` |
| Read model | `XxxReadModel`, `XxxFilter` | `UserFilter` |
| Event payload | `XxxEvent` | `UserRegisteredEvent` |

### Quy tắc

- Một struct/record có **đúng một vai trò**. Không tái dùng response làm contract,
  không alias `XxxResponse = XxxContract`.
- Contract DTO chỉ chứa kiểu dữ liệu domain-native + khóa (Guid); MUST NOT trỏ tới
  entity/DTO nội bộ của service.
- Danh sách trả `[]` rỗng, không trả `null`.
- Enum/constant **không** nằm trong DTO — ở `Domain/Constants`.

---

## XII. Use Case & cấu trúc Application

Mỗi thao tác nghiệp vụ là một use case class, gom theo feature:

```
Application/Features/Users/
├── GetUserUseCase.cs
├── GetUserRequest.cs
├── GetUserRequestValidator.cs
└── UserMapper.cs        (nếu chỉ dùng cho feature)
```

```csharp
public sealed class GetUserUseCase(IUserRepository users)
{
    public async Task<UserResponse> ExecuteAsync(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw UserErrors.NotFound(id);
        return user.ToResponse();
    }
}
```

Quy tắc:

- Use case nhận **abstraction** (repository, publisher, provider) qua constructor.
- Use case ném lỗi domain; không `try/catch` chung chung.
- Helper chỉ phục vụ một use case → tách file cùng package, hậu tố `*Helper`.
  Không tạo helper cho biểu thức một dòng.
- Mapper/validator của feature đặt cùng feature nếu chỉ dùng cục bộ.

### Trách nhiệm từng layer

```
Api/Endpoints          → bind, validate, gọi use case, trả response
Application/Features   → nghiệp vụ, orchestration, transaction, phát lỗi domain
Application/Common     → DTO dùng chung, mapper, validator, error, abstraction
Domain                 → entity, invariant, constant, domain event, repo interface
Infrastructure         → EF Core, MassTransit, adapter ngoài (impl interface)
```

---

## XIII. Cross-Service Contract & gRPC

- **gRPC contract**: `.proto` ở `contracts/<owner>/v1/<owner>.proto`; là nguồn
  chân lý cho giao tiếp đồng bộ liên service.
- Codegen: `Grpc.Tools` sinh client/server từ `.proto` lúc build.
- **Transport mapping** (wire ↔ DTO) do **service sở hữu** (owner) định nghĩa,
  nằm trong `Infrastructure/Adapters/Grpc/` của owner; client gọi là adapter mỏng.
- Gọi gRPC MUST có **deadline/timeout**; xử lý lỗi transport tường minh.
- **Không** để lộ entity/domain type qua proto; proto chỉ mô tả dữ liệu + method.
- Xem [`contract-purity-pattern.md`](contract-purity-pattern.md) và
  [`microservices/02-grpc-and-events.md`](microservices/02-grpc-and-events.md).

---

## XIV. Worker Pattern

Worker nền là `BackgroundService`/`IHostedService`, đăng ký trong host, dùng để
consume event, tác vụ định kỳ, dọn dẹp. Worker **mỏng**, giao việc cho use case.

```csharp
public sealed class UserRegisteredConsumer(RegisterProfileUseCase useCase, ILogger<UserRegisteredConsumer> log)
    : IConsumer<UserRegisteredEvent>
{
    public async Task Consume(ConsumeContext<UserRegisteredEvent> ctx)
    {
        await useCase.ExecuteAsync(ctx.Message, ctx.CancellationToken);
    }
}
```

Quy tắc:

- Worker/consumer MUST NOT chứa nghiệp vụ; gọi use case qua abstraction.
- Consumer MUST idempotent (xử lý trùng không tạo hiệu ứng lần hai).
- Dùng `IHostedService` cho tác vụ định kỳ; tôn trọng `CancellationToken` để tắt êm.
- Worker không tự mở transaction rời rạc; đi qua use case/UoW.

---

## XV. Event & Outbox Pattern

Publish event bất đồng bộ qua **MassTransit + RabbitMQ**, dùng **transactional
outbox** để không mất event.

- Cấu hình outbox trên `DbContext` (MassTransit EF outbox): publish ghi vào bảng
  outbox **trong cùng transaction** với thay đổi dữ liệu.
- Publish bằng `IPublishEndpoint` **bên trong** transaction đã mở; không publish
  trực tiếp ra broker ở đường đi rời rạc.
- Tên event ở thì quá khứ (`UserRegisteredEvent`); payload đủ để consumer xử lý.
- Consumer idempotent; bỏ qua an toàn field lạ; timestamp thứ tự đóng ở thời điểm
  xử lý.
- Topology, DLQ, retry: xem [`event-driven-design.md`](event-driven-design.md).

---

## XVI. Auth & Authorization Pattern

- Authentication: JWT Bearer (`AddAuthentication().AddJwtBearer(...)`) với
  **ES256** — xem [ADR 001](../decisions/001-authn-authz-architecture.md):
    - Chỉ Identity giữ **private key**; Diet/Progress chỉ giữ **public key**.
    - Token có header **`kid`**; service chấp nhận **nhiều public key**, tra theo
      `kid` (để xoay vòng key không cần deploy đồng thời).
    - Public key lấy từ biến môi trường dạng **base64(PEM) một dòng**; **không**
      có JWKS endpoint, service **không** phụ thuộc runtime vào Identity.
- Đọc user hiện tại qua abstraction, không đọc `HttpContext` trong Application:

```csharp
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool InRole(string role);
}
```

- Authorization theo policy/role: `.RequireAuthorization("Admin")`.
- Endpoint công khai (đăng ký/đăng nhập) `.AllowAnonymous()`.
- Service **không** gọi Identity mỗi request để validate; tin JWT đã ký, vì public
  key đã có sẵn qua biến môi trường.
- **Chưa chốt** (xem [ADR 001](../decisions/001-authn-authz-architecture.md) §5):
  TTL / refresh / revoke / logout; mô hình authorization (RBAC vs permission,
  ownership); thuật toán băm mật khẩu.

---

## XVII. Configuration & Options Pattern

- Bind cấu hình vào **strongly-typed options**; validate lúc khởi động (fail fast).

```csharp
public sealed class RabbitMqOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 5672;
}

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMQ"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

- Secret, connection string, provider key lấy từ biến môi trường; không hardcode.
- Tên section theo nhà cung cấp (`ConnectionStrings`, `RabbitMQ`, `Jwt`, ...).

---

## XVIII. Rate Limiting Pattern

Dùng **ASP.NET Core Rate Limiter** (built-in), không tự viết.

- Chính sách theo nhu cầu: `fixed-window`/`sliding-window` cho business quota;
  `token-bucket` cho shield chống spam.
- Áp policy toàn cục + policy riêng cho endpoint nhạy cảm (login, gửi OTP):

```csharp
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
    });
});
```

- Trả `429` chuẩn; không nới limit để lách test.
- Nếu cần quota chính xác **xuyên nhiều instance**, key theo Redis (distributed),
  không dựa vào limiter in-memory.

---

## XIX. Logging & Observability

- **Structured logging** (`ILogger<T>` với message template), không nối chuỗi.
- Mọi request/consumer gắn **correlation id**; truyền xuyên service qua header/event.
- Trace/metric qua OpenTelemetry; health check `/health` + `/alive`.
- **Không log** secret, token, mật khẩu, dữ liệu cá nhân nhạy cảm.
- Log ở mức phù hợp; lỗi kèm stack nhưng không trả stack cho client.

---

## XX. Anti-patterns (CẤM)

- Domain phụ thuộc EF Core / ASP.NET Core.
- Application gọi thẳng `DbContext`, `HttpClient`, MassTransit bus.
- Service A `using` namespace nội bộ của service B.
- `.Include`/`.Join` sang entity của service khác (truy cập chéo DB).
- Ném/tự dựng ProblemDetails trong endpoint; `try/catch` rải rác.
- Trả entity domain ra API/contract; tái dùng một DTO cho nhiều vai trò.
- Nghiệp vụ trong controller/endpoint, middleware, hay worker/consumer.
- `async void`; `.Result`/`.Wait()`; quên `CancellationToken`.
- Hard-code connection string/secret; sửa DB bằng tay thay vì migration.
- Publish event ngoài transaction (không outbox).
- Hard-code giá trị enum; phải tham chiếu constant.

---

## XXI. Liên quan

- [`architecture.md`](architecture.md) — kiến trúc tổng thể, ranh giới service.
- [`contract-purity-pattern.md`](contract-purity-pattern.md) — hợp đồng liên service.
- [`event-driven-design.md`](event-driven-design.md) — event, outbox, topology.
- [`../development/coding-conventions.md`](../development/coding-conventions.md) — quy ước C#.
- [`../development/api-testing.md`](../development/api-testing.md) — test API bằng Swagger.
- [`../decisions/001-authn-authz-architecture.md`](../decisions/001-authn-authz-architecture.md) — quyết định AuthN/AuthZ.
