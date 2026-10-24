# Kế hoạch triển khai: Progress Service

**Nhánh**: `feat/progress-service` | **Feature**: `qbao-001-progress-service`
**Ngày**: 2026-10-24 | **Đầu vào**: [spec.md](spec.md)

## Tóm tắt

Phase 1 tạo đặc tả bốn story, nghiên cứu dependency, mô hình dữ liệu, proposal và
roadmap Phase 2–6. Không implement. Phase 2–6 sau này dùng Clean Architecture,
database riêng và các contract đã duyệt. Phần BMI/streak đầy đủ bị chặn bởi Identity/Diet;
không thay bằng HTTP, DB join hoặc giả dữ liệu. Các policy thiếu căn cứ giữ PENDING
theo lựa chọn developer, không tuyên bố toàn bộ kế hoạch đã sẵn sàng thực thi.

## Bối cảnh kỹ thuật

- C# / ASP.NET Core .NET 10; PostgreSQL 17, EF Core Code First/Npgsql theo repo.
- Tái sử dụng Longevia.Shared cho errors/validation/pagination/auth/observability
  khi đã kiểm chứng đáp ứng yêu cầu; không chỉnh Shared thuộc feature khác.
- Minimal API endpoint groups, use case, FluentValidation, mapping DTO riêng;
  Infrastructure triển khai repository/UoW/gRPC/messaging abstractions.
- xUnit, WebApplicationFactory, Testcontainers PostgreSQL/RabbitMQ; E2E qua Nginx.
- Deploy container Linux qua compose. Progress core cần edge + data_net;
  chỉ join internal_net nếu contract/ADR call đã approved, không bật AI mặc định.
- Mục tiêu PRD: read p95 <150ms, write p95 <250ms, ít nhất 100 RPS/container.
  Phase 6 benchmark fixture 10.000 metric/user và active days/sleep/water, 100 RPS
  trong 5 phút sau warmup 30 giây (kế hoạch đo, không phải kết quả hiện tại).
- Quy mô tối đa lịch sử/người dùng và retention chưa được product định nghĩa;
  không xóa dữ liệu lịch sử theo một TTL tự chọn. Retention retry/inbox chờ Q03/Q09.
- Nguồn authoritative: [architecture](../../docs/system-design/architecture.md),
  [design-pattern](../../docs/system-design/design-pattern.md),
  [coding conventions](../../docs/development/coding-conventions.md),
  [testing](../../docs/development/testing.md), [migrations](../../docs/development/migration.md).

## Constitution Check

Đánh giá trước nghiên cứu và đánh giá lại sau thiết kế: không có đề xuất ngoại lệ
Constitution. Bảng là kiểm tra thiết kế, không phải evidence runtime đã pass.

| Nguyên tắc | Cách đáp ứng thiết kế | Trạng thái |
| --- | --- | --- |
| I — ownership | Chỉ progress_db, không FK/import/DB access service khác. | Đạt ở tài liệu. |
| II — gRPC | Height/read phụ thuộc dùng contract gRPC nếu được duyệt, deadline/error explicit, direction cần ADR. | BMI tích hợp BLOCKED Q02; không lách HTTP. |
| III — RabbitMQ | Versioned contract trước consumer; outbox cùng transaction, inbox/projection atomic, idempotent. | Meal BLOCKED Q09. |
| IV — contract-first | Proposal ở feature/contracts, không đổi canonical; Nguyên/Huy/lead phải approve. | Chưa cho phép code dependency. |
| V — verified gates | Negative control, scoped tests, E2E thật và gate theo AGENTS §2; đỏ dừng commit/push. | Chưa có runtime Progress; Phase 1 gate ghi riêng. |
| EF/config/observability | Migration Code First khi sang phase sau; fail-start; public key env, health, metrics chuẩn. | Có task foundation, không migration Phase 1. |
| Team | Owner qbao từ user, helper cấp 001, services=[Progress], status=open. | Feature chưa hoàn tất triển khai nên không closed. |

E2E bắt buộc của toàn feature triển khai chưa thể có ở Phase 1 không code;
đã giữ task Phase 6 và không claim hoàn tất Progress core. Không miễn gate repository
chỉ vì đây là tài liệu. Nếu make không có, dùng đúng các lệnh dotnet trong Makefile.

## Cấu trúc dự kiến

Tài liệu thực nằm trong `specs/qbao-001-progress-service/`: meta, spec, plan, tasks,
research, data-model, contracts/README, quickstart, deferred, checklists và verification.
Không tạo application project ở Phase 1.

```text
src/Services/Progress/
  Progress.Domain/Entities/                    # invariants/constants/repository contracts
  Progress.Application/Abstractions/           # current user/clock/providers/UoW
  Progress.Application/Features/               # Fasting, Water, Sleep, Metrics, Dashboard
  Progress.Infrastructure/Persistence/         # DbContext, configurations, repositories, migrations
  Progress.Infrastructure/Adapters/Grpc/       # chỉ khi contract approved
  Progress.Infrastructure/Messaging/           # chỉ khi meal contract approved
  Progress.Api/Endpoints/                      # route không kèm /api/progress
  Progress.Api/Program.cs                      # composition root
  tests/Progress.UnitTests/
  tests/Progress.IntegrationTests/
```

Hướng phụ thuộc Api → Application → Domain; Infrastructure implement abstractions,
Domain không biết EF/ASP.NET. Không reference Identity/Diet/Worker source.
Path trong tasks là path dự kiến, không tuyên bố file đã tồn tại.

## Kiến trúc, dữ liệu và API

- [data-model.md](data-model.md) mô tả entity, constraints, indices và transition.
  Chỉ tạo schema từng phase sau khi policy liên quan được duyệt; không sinh toàn
  schema chưa chốt ở foundation. Auto MigrateAsync trước nhận request, fail-fast.
- Unique filtered index một active/user + atomic end chống race nhiều instance.
  Clock abstraction để current lấy một mốc; elapsed UTC duration không reset theo ngày.
- Water append/SUM tránh lost update; sleep xử lý instant đầy đủ. Ngày/zone Q01
  phải dùng thống nhất trong body activity, water, sleep và meal projection.
- BMI lưu height/BMI/category snapshot + provenance tại ghi (BR-05), không tính lại khi GET.
- Dashboard đọc snapshot và union active day; current/best và window Q06 phải duyệt.
  Không ship metrics-only streak như hoàn chỉnh hoặc đoán số 0 khi meal thiếu.
- [contracts/README.md](contracts/README.md) inventory 9 REST route, 6 có API detail,
  3 thiếu schema; không triển khai proposal trước contract review.
- History nội bộ PagedResult theo system-design, wire adapter cục bộ đề xuất Q07;
  không đổi Shared. Input validation 400 theo system-design, không lấy ví dụ 422
  thấp thẩm quyền hơn làm chuẩn cho mọi lỗi.
- Auth verify ES256/kid/issuer/audience/lifetime; current user Guid hợp lệ; filter
  UserId ở mọi query/mutation. Không đọc HttpContext trong Application. Ownership
  failure shape và Shared gaps Q08 phải được lead xử lý trước claim security hoàn chỉnh.

## Phụ thuộc và điều kiện mở chặn

| Owner | Cần xác nhận | Tác động |
| --- | --- | --- |
| Nguyên — Identity | Height int proposal vs API decimal; RPC/presence/effective-time/deadline/error hoặc event snapshot + backfill; profile missing; Shared auth strict kid/fail-start với lead. | BMI Phase 5/Q02; auth Q08. Không sửa feature Identity đang open. |
| Huy — Diet | Event lifecycle hoặc gRPC active days, date/zone, revision/tombstone, outbox, bootstrap/correction/completeness và canonical schema/topology. | Full streak Phase 6/Q09; không có Diet implementation ở base. |
| Duy — Gateway/Worker | Compose Progress, Prometheus scrape, route/Swagger reachability, shared solution/package coordination; digest proposal feature riêng. | Host foundation Phase 2; không triển khai Worker/Gateway Phase 1. |
| Đức — Recommendation | RPC workout/habits đã tồn tại; nếu mở tích hợp tương lai cần direction ADR, profile/nutrition, deadline/limits/citation/disclaimer. | Deferred; không chặn core độc lập. |
| Bảo/product/lead | Q01/Q03–Q08 và ADR direction mới. | Chặn task liên quan trước code; danh sách đầy đủ ở research. |

## Shared Files & Conflict Hotspots

Phase 1 **không sửa** hàng nào dưới đây. Đây là điều kiện phối hợp cho Phase 2–6,
không phải quyền sửa file owner khác. Registry base: Identity feature còn open với
Identity/Shared/Worker; Progress không nhận ownership các file đó.

| Path | Nhu cầu dự kiến | Owner / phối hợp trước sửa | Điểm hợp nhất |
| --- | --- | --- | --- |
| deployments/docker-compose.yml | Thêm host Progress/env/networks/health, không expose private key. | Duy; chờ xác nhận slot thay đổi. | Phase 2 host. |
| deployments/nginx/nginx.conf | Route Progress đã có; chỉ sửa nếu kiểm tra xác nhận thiếu. | Duy. | Phase 2 routing test. |
| deployments/prometheus/prometheus.yml | Thêm static scrape Progress /metrics; thiếu job thì target không xuất hiện. | Duy. | Phase 2 observability. |
| deployments/postgres/init/01-databases.sh | Đã có progress DB/user; kiểm tra quyền, không sửa tay DB. | Duy. | Phase 2 integration. |
| longevia-be.slnx | Thêm project Progress/tests sau khi tạo. | Bảo phối hợp Duy/lead, tránh nhiều PR sửa đồng thời. | Phase 2. |
| Directory.Packages.props | Chỉ nếu thiếu package so với dependency hiện có; ưu tiên tái sử dụng version. | Lead + Duy; PR shared riêng nếu cần. | Trước package mới; không mặc định cần sửa. |
| src/Services/Progress/Progress.Api/Program.cs | DI, auth, migrations, health/Swagger/metrics. | Bảo; mọi phase chạm composition root phải tuần tự. | Phase 2 rồi từng story. |
| src/Services/Progress/Progress.Infrastructure/Persistence/Migrations | Migrations/snapshot chỉ Progress, một open feature sở hữu. | Bảo; resolve/registry trước tạo, đồng bộ main theo quyền. | Từng phase persistence sau approve policy. |
| contracts/identity/v1/identity.proto | Height RPC nếu được chọn. | Nguyên; không sửa từ Progress. | Producer PR được duyệt/merge trước consumer. |
| contracts/events/v1/README.md, src/Shared/Events | Meal/schema topology nếu được chọn. | Huy + lead + Duy; không sửa khi Identity đang sở hữu Shared. | Contract PR riêng trước Phase 6. |
| src/Shared/Auth | Missing kid/fail-start khác chuẩn đề xuất strict. | Nguyên/lead; không patch Shared trong Progress. | Q08 trước claim auth hoàn chỉnh. |

Không tạo `specs/registry.md`: team-workflow định nghĩa registry là view in từ helper,
cao hơn câu hướng dẫn cũ còn trong plan template. Không đổi .agents/.specify tracked.

## Roadmap và verification

1. Phase 2 — Foundation & Persistence: skeleton, auth, clock, errors, DB riêng, config,
   health/metrics/Swagger, hạ tầng phối hợp. Chưa chọn schema business còn PENDING.
2. Phase 3 — Fasting: Q03/Q08 approved rồi test trước/cùng code; concurrency, retries, current.
3. Phase 4 — Water & Sleep: Q01/Q05/Q08 approved rồi append nước, sleep qua đêm và validation.
4. Phase 5 — Body Metrics & BMI: chờ Q02/Q04/Q07 và Identity canonical contract + ADR.
5. Phase 6 — Dashboard, Streaks & Integration: chờ Q06/Q09 và Diet contract/bootstrap,
   tests real broker/DB, load test, source/contract sync và guides.

Chi tiết task và dependency ở [tasks.md](tasks.md); validation guide ở
[quickstart.md](quickstart.md). Gates theo [AGENTS §2](../../AGENTS.md), không
chép lại bảng gate. Progress chưa có test project ở Phase 1 nên scoped Progress
test là không áp dụng, full repository close-out vẫn bắt buộc trước push.

## Độ phức tạp và giới hạn

Không có vi phạm Constitution cần ngoại lệ. Projection/retry receipt chỉ thêm khi
contract được duyệt, tránh framework/dedup tầng riêng không có nhu cầu.
Kế hoạch không chứng minh runtime đã hoạt động; tất cả Q giữ PENDING và task code
chưa hoàn thành. Hai hướng dẫn handoff được tạo khi Phase 2–6 thực sự đổi surface;
Phase 1 chỉ proposal nên chưa đặt trạng thái sẵn sàng tích hợp.
