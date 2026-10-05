# EF Core Migrations (Code First)

Schema do **EF Core Code First** quản lý. Database là hệ quả của code, không sửa
bằng tay.

## Nguyên tắc

- Mỗi service có `DbContext` riêng, chỉ trỏ tới database của service đó.
- Migration thuộc về service sở hữu database; service khác MUST NOT tạo migration
  cho DB đó.
- MUST NOT sửa database thủ công (SQL tay) trong môi trường đã có migration.
- Provider: **Npgsql** (PostgreSQL).

## Tạo & áp dụng migration

Qua Makefile (bọc `dotnet ef`):

```bash
# Tạo migration cho một service
make ef-add NAME=InitialCreate PROJECT=src/Services/Identity/Identity.Infrastructure

# Áp dụng
make ef-update PROJECT=src/Services/Identity/Identity.Infrastructure
```

Hoặc trực tiếp:

```bash
dotnet ef migrations add InitialCreate --project <Infrastructure project>
dotnet ef database update --project <Infrastructure project>
```

## Quy ước

- Tên migration mô tả thay đổi: `AddUserEmailIndex`, `CreateDietPlanTable`.
- **Serialize migration numbering**: không để nhiều phase/nhánh cùng tạo migration
  song song trên một service — timestamp sẽ đụng nhau. Xem cost discipline trong
  skill `speckit-orchestrate` §3.
- Migration MUST review được: xem file sinh ra trước khi áp dụng.
- Thay đổi phá vỡ dữ liệu (drop/đổi kiểu) cần kế hoạch backfill rõ ràng.
- Không sửa migration đã áp dụng ở môi trường chia sẻ; tạo migration mới.

## Tự chạy migration khi khởi động

Mỗi service MUST tự áp dụng migration **của chính nó** lúc khởi động, để local,
Docker và CI luôn khớp schema mà không cần bước thủ công.

- Chạy sau khi build `IServiceProvider` và **trước** khi phục vụ request.
- Chỉ áp migration của database service đó; không đụng DB service khác.
- Idempotent: `MigrateAsync()` tự bỏ qua migration đã áp dụng.
- Log kết quả; migration lỗi là lỗi khởi động — **fail fast**, không chạy tiếp.

```csharp
// Program.cs — chạy migration trước khi app phục vụ request
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

Lưu ý:

- Nhiều instance khởi động cùng lúc có thể tranh nhau chạy migration. Với > 1
  instance, dùng khóa (Postgres advisory lock / leader election) hoặc chạy
  migration ở bước deploy trước khi scale.
- Mặc định **bật** auto-migrate khi start cho dev/docker. Production có thể tắt
  qua cờ cấu hình (ví dụ `Database:AutoMigrate=false`) và chạy ở bước deploy
  riêng có kiểm soát.

## Seeding

- Dữ liệu seed dev đặt trong `HasData` hoặc seed runner riêng; MUST idempotent.
- Không seed dữ liệu nhạy cảm.

## Kiểm tra

- Sau khi đổi schema, chạy `make test PROJECTS=<service>` cho integration test DB.
