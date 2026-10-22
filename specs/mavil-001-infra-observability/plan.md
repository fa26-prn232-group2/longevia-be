# Kế hoạch Triển khai Kỹ thuật: Hạ tầng, Gateway, Worker & Giám sát Grafana

**Branch**: `feat/infra-observability` | **Feature**: `specs/mavil-001-infra-observability` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

**Input**: Đặc tả kỹ thuật từ `specs/mavil-001-infra-observability/spec.md`

---

## 1. Tóm tắt (Summary)

Thiết lập toàn bộ hạ tầng phát triển cục bộ và môi trường container hóa cho dự án Longevia Backend:
- Đóng gói microservices .NET 10 (`Identity.Api`, `Worker`) bằng multi-stage build trên nền runtime chiseled non-root (`aspnet:10.0-noble-chiseled`).
- Nâng cấp Nginx API Gateway với bảng mã tiếng Việt UTF-8, cấu trúc lỗi RFC 7807 Problem Details và hỗ trợ SSE/WebSockets.
- Xây dựng Background Worker với MassTransit RabbitMQ, adapter MailKit gửi email linh hoạt giữa Mailpit (Local) và Brevo SMTP Relay (Production).
- Triển khai hệ thống giám sát hiệu năng với Prometheus (cào số liệu mỗi 10s) và Grafana (cổng `3001` - nhường `3000` cho Frontend), tự động nạp Datasource và Dashboard ASP.NET Core qua cơ chế provisioning.
- Đóng gói thư viện OpenTelemetry dùng chung trong `Longevia.Shared` để mọi service kích hoạt đo lường với 2 dòng code.

---

## 2. Bối cảnh Kỹ thuật (Technical Context)

**Language/Version**: C# / .NET 10 (ASP.NET Core, Worker Service SDK)  
**Primary Dependencies**: 
- `MassTransit` & `MassTransit.RabbitMQ` (v9.2.3)
- `MailKit` (v4.11.0)
- `StackExchange.Redis` (v2.8.24)
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` & `OpenTelemetry.Instrumentation.*` (v1.20.x — nâng từ 1.11.x: core 1.11–1.12 dính advisory GHSA-g94r-2vxg-569j, restore sạch ở 1.20.1)
- `Npgsql.EntityFrameworkCore.PostgreSQL` (v10.0.3)  

**Storage**: PostgreSQL 17 tích hợp extension `pgvector` (`pgvector/pgvector:pg17`), Redis 7 (`redis:7-alpine`)  
**Message Broker**: RabbitMQ 4 (`rabbitmq:4-management`)  
**Mail Server**: Mailpit (`axllent/mailpit:latest` cho dev), Brevo SMTP Relay (`smtp-relay.brevo.com:587`, Production)  
**Testing**: `dotnet test`, `xunit`, `make lint`, `make check`  
**Target Platform**: Linux Containers (Docker Compose local & Cloud deploy)  
**Project Type**: Microservices Backend (.NET Web API, gRPC, Background Worker, Nginx Gateway)  
**Performance Goals**: Nginx p95 latency < 50ms, Prometheus scrape interval 10s, image size < 180MB  
**Constraints**: 
- Tuân thủ phân tách 3 mạng Docker theo ADR 001 (`edge`, `internal_net`, `data_net`).
- Grafana cố định cổng `3001` (bảo lưu cổng `3000` cho Frontend).
- Runtime container không shell, non-root.  

---

## 3. Constitution Check (Kiểm tra Hiến pháp Dự án)

*GATE: Đạt 100% trước khi triển khai chi tiết.*

| Nguyên tắc Hiến pháp | Đánh giá Tuân thủ | Kết luận |
| :--- | :--- | :---: |
| **I. Service Boundaries & Data Ownership** | `Worker` là consumer hạ tầng dùng chung, không sở hữu DB nghiệp vụ riêng; chỉ dùng Redis cho dedup idempotency (TTL 7 ngày, không phải dữ liệu nghiệp vụ). `Identity` sở hữu riêng `identity_db`. Không truy cập chéo DB. Worker CHỈ render email từ payload đủ, không tổng hợp số liệu nghiệp vụ. | **PASS** |
| **II. Synchronous Inter-Service Calls via gRPC** | Recommendation tiếp tục được cô lập trong `internal_net`, không mở route qua Nginx. Nginx chỉ expose Identity, Diet, Progress. | **PASS** |
| **III. Asynchronous via RabbitMQ — phía consumer (phạm vi tính năng này)** | Worker consume event qua RabbitMQ (`data_net`) với queue bền `worker.email`, retry 3 lần (2s, 5s, 10s), DLX/DLQ, idempotency qua Redis dedup, bỏ qua field lạ. Topology khai báo tường minh bằng MassTransit (`data-model.md` §1.3). | **PASS** |
| **III. Transactional outbox — phía producer (ngoài phạm vi, chuyển cho feature Identity)** | Producer outbox (ghi outbox cùng transaction tạo user + relay nền) thuộc về service sở hữu `identity_db` và migration — KHÔNG thể làm trong tính năng hạ tầng này (1 service = 1 feature mở tạo migration). Contract event tại `data-model.md` §1.1 đã ghi nghĩa vụ outbox cho producer; consumer đã phòng thủ (durable queue + idempotent). | **OPEN → feature Identity** |
| **IV. Contract-First Interfaces** | Event có `SchemaVersion = 1`, chỉ mục version tại `contracts/events/v1/README.md`, record C# trong `src/Shared/Events/` là nguồn chân lý khả biên dịch. Thêm field optional = tương thích ngược; phá vỡ = version mới. | **PASS** |
| **V. Test-First & Verified Gates** | Mỗi phase chạy `make test PROJECTS=<path>` scoped + `make lint`. Tính năng bổ sung test tự động: consumer idempotency/retry (T017), template render (T016), metrics endpoint (T024) — xem tasks Phase 5/7/8. | **PASS** |

---

## 4. Cấu trúc Dự án (Project Structure)

### Tài liệu Đặc tả (Feature Documentation)
```text
specs/mavil-001-infra-observability/
├── spec.md              # Bản đặc tả tính năng đã làm rõ
├── checklists/          # Danh sách kiểm tra chất lượng yêu cầu
│   └── requirements.md
├── plan.md              # Bản kế hoạch này
├── research.md          # Kết quả nghiên cứu kỹ thuật & quyết định kiến trúc
├── data-model.md        # Đặc tả mô hình dữ liệu, event, biến môi trường
├── quickstart.md        # Hướng dẫn kiểm thử và khởi chạy nhanh
└── tasks.md             # Phân rã task thực thi (Phase 2 - speckit-tasks)
```

### Mã nguồn Triển khai (Repository Layout)
```text
longevia-be/
├── deployments/
│   ├── docker-compose.yml                     # Cấu hình đa container (Postgres, Redis, RabbitMQ, Mailpit, Prometheus, Grafana, Nginx, App)
│   ├── postgres/init/01-databases.sh          # Init per-service DB + role + `CREATE EXTENSION vector`
│   ├── nginx/
│   │   └── nginx.conf                         # Gateway reverse proxy, UTF-8 charset, 404 ProblemDetails
│   ├── prometheus/
│   │   └── prometheus.yml                     # Scrape targets: identity, worker, rabbitmq
│   └── grafana/
│       └── provisioning/
│           ├── datasources/prometheus.yml     # Auto-provision Prometheus datasource
│           └── dashboards/
│               ├── dashboards.yml             # Auto-provision dashboard provider
│               └── aspnetcore-overview.json   # Dashboard mẫu giám sát ASP.NET Core
├── src/
│   ├── Services/
│   │   ├── Identity/
│   │   │   └── Dockerfile                     # Multi-stage Chiseled build cho Identity.Api
│   │   └── Worker/                            # Project Worker Service mới (.NET 10)
│   │       ├── Worker.csproj
│   │       ├── Program.cs
│   │       ├── Dockerfile                     # Multi-stage Chiseled build cho Worker
│   │       ├── Adapters/Email/
│   │       │   ├── IEmailSender.cs
│   │       │   └── SmtpEmailSender.cs         # Triển khai gửi mail bằng MailKit
│   │       ├── Templates/
│   │       │   └── EmailTemplates.cs          # 4 mẫu email HTML responsive
│   │       └── Consumers/
│   │           ├── UserRegisteredConsumer.cs  # Consumer sự kiện đăng ký (idempotent qua Redis dedup, DLQ)
│   │           └── PasswordResetRequestedConsumer.cs # Consumer sự kiện đặt lại mật khẩu
│   └── Shared/
│       ├── Events/                            # Event Contracts (record C#, mỗi event có SchemaVersion = 1)
│       │   ├── UserRegisteredEvent.cs
│       │   └── PasswordResetRequestedEvent.cs
│       └── Observability/
│           └── OpenTelemetryExtensions.cs     # Extension AddObservability() + MapPrometheusScrapingEndpoint()
├── contracts/
│   └── events/v1/README.md                  # Chỉ mục canonical event schema có version (trỏ tới record C# trong src/Shared/Events/)
├── Directory.Packages.props                   # CPM: Thêm MailKit, StackExchange.Redis, OpenTelemetry
├── longevia-be.slnx                           # Đăng ký Worker.csproj
├── Makefile                                   # Thêm targets docker-up, infra-up, docker-logs...
├── .env.example                               # Cập nhật biến môi trường chuẩn
└── AGENTS.md                                  # Thêm quy chuẩn tích hợp Observability cho toàn đội ngũ
```

---

## 5. Danh sách Tệp Dùng chung & Điểm Xung đột (Shared Files & Conflict Hotspots)

| Đường dẫn tệp | Lý do tính năng này cần thay đổi | Người sở hữu / Feature khác | Điểm Merge |
| :--- | :--- | :--- | :--- |
| `Directory.Packages.props` | Khai báo tập trung phiên bản `MailKit`, `StackExchange.Redis`, `OpenTelemetry.*` | Dùng chung toàn repo | Trước khi code |
| `longevia-be.slnx` | Đăng ký project `Worker.csproj` vào solution | Dùng chung | Cùng lúc tạo Worker |
| `deployments/docker-compose.yml` | Bổ sung Redis, Mailpit, Prometheus, Grafana và kích hoạt Identity, Worker | Dev 5 sở hữu hạ tầng | Độc lập |
| `deployments/nginx/nginx.conf` | Cấu hình UTF-8, 404 ProblemDetails, timeout và routes | Dev 5 sở hữu Gateway | Độc lập |
| `Makefile` | Bổ sung các lệnh quản trị Docker tiện ích | Dev 5 sở hữu | Độc lập |
| `contracts/events/v1/README.md` | Chỉ mục canonical event schema có version (mới, do feature này sở hữu) | Không feature nào khác | Cùng lúc tạo Events |
| `AGENTS.md` | Bổ sung quy chuẩn bắt buộc tích hợp Observability cho các dev khác | Lead / Quy chuẩn nhóm | Ngay khi lập plan (đã làm, T026 xác minh lại) |
| `.env.example` | Đồng bộ các biến môi trường cho Email, Redis, Prometheus, Grafana | Dev 5 | Cùng compose |

---

## 6. Theo dõi Độ phức tạp (Complexity Tracking)

*Một mục OPEN có chủ đích (không phải vi phạm che giấu): producer-side transactional outbox thuộc về feature Identity (sở hữu `identity_db`/migration); tính năng này làm phía consumer + contract và ghi nghĩa vụ producer tại `data-model.md` §1.1. Không có vi phạm nào khác.*

---

## 7. Phân mạng Docker (quyết định bổ sung)

`identity`, `worker`: `edge` + `data_net`. `prometheus`, `grafana`: `edge` (cào metrics qua tên service trên cùng mạng edge). `postgres`, `redis`, `rabbitmq`, `mailpit`: `data_net`. `internal_net` giữ dành cho Recommendation gRPC (cô lập khỏi Nginx), không container nào của tính năng này tham gia nhưng compose PHẢI khai báo đủ 3 mạng theo FR-001.
