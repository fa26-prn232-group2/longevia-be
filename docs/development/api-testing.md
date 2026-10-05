# Test API bằng Swagger

Mỗi service client-facing expose OpenAPI + Swagger UI để dev thử API nhanh.
Dùng **Swashbuckle.AspNetCore**, và **chỉ bật ở Development**.

## 1. Bật Swagger trong service

`Program.cs` của mỗi service client-facing (Identity, Diet, Progress):

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Longevia Identity API", Version = "v1" });

    // Nút Authorize: dán Bearer JWT để gọi endpoint bảo vệ.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT (không cần tiền tố 'Bearer ')."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();                 // /swagger/v1/swagger.json
    app.UseSwaggerUI();               // /swagger
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
```

> Ghi chú: Swagger chỉ phục vụ ở Development. Production MUST NOT expose Swagger.

## 2. Điểm truy cập

### Per-service (chạy trực tiếp khi dev)

```
http://localhost:<port>/swagger            # Swagger UI
http://localhost:<port>/swagger/v1/swagger.json
```

### Qua gateway (Nginx) — UI gộp cả 3 service

```
http://localhost:8080/swagger              # Swagger UI gộp (Identity, Diet, Progress)
http://localhost:8080/api/identity/swagger # Swagger UI của riêng Identity
http://localhost:8080/api/diet/swagger
http://localhost:8080/api/progress/swagger
```

UI gộp chạy bằng container `swagger-ui` (xem `deployments/docker-compose.yml`), đọc
3 spec qua gateway tại `/api/<service>/swagger/v1/swagger.json`.

## 3. Dùng UI gộp

1. `docker compose -f deployments/docker-compose.yml up -d` (cần service chạy).
2. Mở `http://localhost:8080/swagger`.
3. Chọn service ở dropdown **Select a definition**.
4. Với endpoint bảo vệ: gọi `/auth/login` lấy JWT → bấm **Authorize** → dán JWT →
   gọi các endpoint còn lại.

## 4. Recommendation (gRPC-only)

Recommendation không có REST/Swagger. Test bằng gRPC:

```bash
# nếu bật gRPC reflection
grpcurl -plaintext localhost:5001 list
grpcurl -plaintext -d '{...}' localhost:5001 longevia.recommendation.v1.RecommendationService/Recommend
```

## 5. Sinh client từ OpenAPI (tùy chọn)

```powershell
# cần: dotnet tool install --global NSwag.ConsoleCore (hoặc Microsoft.dotnet-openapi)
dotnet openapi add file http://localhost:8080/api/identity/swagger/v1/swagger.json
```

## Liên quan

- [`../api/README.md`](../api/README.md) — quy chuẩn API.
- [`../system-design/design-pattern.md`](../system-design/design-pattern.md) — endpoint pattern.
