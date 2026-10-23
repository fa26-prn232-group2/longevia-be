# Longevia Backend

Microservices backend cho **Longevia** — ASP.NET Core (.NET 10), PostgreSQL, giao
tiếp đồng bộ giữa các service qua **gRPC**, sự kiện bất đồng bộ qua **RabbitMQ**.
Client vào qua **Nginx** (API Gateway, HTTP/HTTPS).

## Mục đích sử dụng & Miễn trừ trách nhiệm

> **Dự án chỉ nhằm mục đích học tập** (dự án môn học), không dùng cho mục đích
> thương mại và không phải sản phẩm đang vận hành.

- Nội dung sách trong [`docs/reference/che-do-an-truong-tho/`](docs/reference/che-do-an-truong-tho/README.md)
  có bản quyền thuộc tác giả, dịch giả và nhà xuất bản; chỉ dùng trong phạm vi học
  tập, **không phân phối lại**, sẽ gỡ bỏ khi có yêu cầu.
- Dữ liệu phái sinh (embeddings/seed) sinh từ nội dung đó cũng chỉ dùng cho học tập.
- Hệ thống **không phải tư vấn y tế**: mọi phân tích và gợi ý chỉ mang tính tham
  khảo, không thay thế bác sĩ hoặc chuyên gia dinh dưỡng. Văn bản sách được số hoá
  bằng OCR nên có thể sai sót; số liệu phải đối chiếu bản in.
- Mã nguồn theo giấy phép [MIT](LICENSE); giấy phép này **không** áp dụng cho nội
  dung sách.
- Chi tiết thành phần bên thứ ba và yêu cầu gỡ bỏ: [NOTICE.md](NOTICE.md).

## Kiến trúc

```
Client (Web / Mobile)
        │ HTTP/HTTPS
        ▼
   Nginx (API Gateway)
        │ HTTP
        ├──► Identity Service ──► identity_db
        ├──► Diet Service ──────► diet_db ──gRPC──► Recommendation Service ──► Google AI
        └──► Progress Service ──► progress_db        │
                    │                              ▼
                    │                    recommendation_db
                    │                    (tri thức đã nhúng - RAG)
                    │ publish (RabbitMQ)
                    ▼
              Background Worker ──► Brevo (email)

External: Cloudinary (media), Google AI (AI provider)
```

- **Client-facing (qua Nginx)**: Identity, Diet, Progress.
- **Nội bộ (gRPC-only)**: Recommendation — không expose REST; sở hữu
  `recommendation_db` chỉ chứa corpus tri thức đã nhúng (RAG), **không** chứa
  dữ liệu người dùng.
- Mỗi service sở hữu database riêng; không truy cập chéo DB.
- Sơ đồ đầy đủ: [`docs/system-design/diagrams/png/c4-model-lv2.png`](docs/system-design/diagrams/png/c4-model-lv2.png).

## Tech stack

| Thành phần | Lựa chọn |
| --- | --- |
| Runtime | .NET 10 / ASP.NET Core |
| Database | PostgreSQL (Npgsql), EF Core **Code First** |
| Gateway | Nginx (prefix theo service) |
| Đồng bộ liên service | gRPC |
| Bất đồng bộ | RabbitMQ |
| Orchestration | Docker Compose (local == production) |
| Test | xUnit |

## Cấu trúc thư mục

```
AGENTS.md                 # luật agent + gate
Makefile                  # gate .NET (lint, test, check, ef-*)
Directory.Build.props     # cấu hình build dùng chung
Directory.Packages.props  # Central Package Management
longevia-be.slnx          # solution
deployments/              # docker-compose, nginx, postgres init
docs/                     # tài liệu (xem docs/README.md)
.opencode/                # skills & commands
.specify/                 # Spec Kit: constitution, templates, scripts
```

## Cài đặt công cụ (Windows)

Mở PowerShell (có thể chạy `winget`/`choco` với quyền admin).

```powershell
winget install Microsoft.DotNet.SDK.10      # .NET SDK 10
winget install Docker.DockerDesktop         # Docker (PostgreSQL, RabbitMQ, Nginx, Swagger UI)
choco install make                          # GNU Make (hoặc: winget install GnuWin32.Make)
```

Sau khi clone, khôi phục công cụ cục bộ (EF Core CLI) và NuGet:

```powershell
dotnet tool restore     # cài dotnet-ef theo .config/dotnet-tools.json
dotnet restore          # khôi phục NuGet
```

Kiểm tra phiên bản:

```powershell
dotnet --version        # 10.x
docker --version
make --version          # GNU Make
dotnet ef --version     # sau khi tool restore
```

> Nếu `make` không nhận diện được trong PowerShell, thêm `C:\ProgramData\chocolatey\bin`
> vào `PATH`, hoặc dùng Git Bash.

## Thư viện chính

Phiên bản được quản lý tập trung ở [`Directory.Packages.props`](Directory.Packages.props)
(Central Package Management) — trong project chỉ ghi `<PackageReference Include="..." />`
không kèm version.

| Nhóm | Thư viện |
| --- | --- |
| Data access | Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL |
| Messaging | MassTransit, MassTransit.RabbitMQ |
| gRPC | Grpc.AspNetCore, Grpc.Net.Client, Grpc.Tools |
| API & validation | Swashbuckle.AspNetCore, FluentValidation, JwtBearer |
| Testing | xunit, Microsoft.NET.Test.Sdk, Testcontainers.PostgreSql |

## Bắt đầu

```powershell
# 1. Tạo file môi trường
Copy-Item .env.example .env

# 2. Bật hạ tầng (PostgreSQL, RabbitMQ, Nginx, Swagger UI)
# --env-file là bắt buộc: compose nằm ở deployments/ nên không tự đọc .env ở gốc
docker compose --env-file .env -f deployments/docker-compose.yml up -d

# 3. Build và test
make lint
make check
```

Test API nhanh bằng Swagger: xem [`docs/development/api-testing.md`](docs/development/api-testing.md).

## Gate

Nguồn chân lý duy nhất cho gate là [`AGENTS.md`](AGENTS.md) §2.

| Lệnh | Ý nghĩa |
| --- | --- |
| `make lint` | fast gate — format-check + build (warnings as errors) |
| `make test PROJECTS=<path>` | test scoped theo project |
| `make check` | close-out gate — format-check + build Release + full test + quét lỗ hổng |

## Tài liệu

- [`docs/README.md`](docs/README.md) — mục lục tài liệu.
- [`docs/system-design/`](docs/system-design/) — kiến trúc, design pattern, event.
- [`docs/development/`](docs/development/) — quy trình, convention, setup, test, migration.
- [`.specify/memory/constitution.md`](.specify/memory/constitution.md) — hiến pháp dự án (thẩm quyền cao nhất).

Khi tài liệu mâu thuẫn: **Constitution > system-design > development > khác**.
