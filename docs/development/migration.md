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

## Áp dụng tự động

- Dev/local: có thể `dotnet ef database update` thủ công hoặc chạy lúc khởi động
  service (`Database.Migrate()`), tùy service.
- Production: áp dụng có kiểm soát (bước deploy riêng), không auto-migrate ngầm.

## Seeding

- Dữ liệu seed dev đặt trong `HasData` hoặc seed runner riêng; MUST idempotent.
- Không seed dữ liệu nhạy cảm.

## Kiểm tra

- Sau khi đổi schema, chạy `make test PROJECTS=<service>` cho integration test DB.
