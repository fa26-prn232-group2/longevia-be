# Implementation Plan: Recommendation Service với RAG tri thức sách "Chế độ ăn trường thọ"

**Branch**: `feat/recommendation-rag` | **Date**: 2026-10-22 | **Spec**: [spec.md](./spec.md)
**Feature ID**: `duck/001/recommendation-rag` | **Owner**: `duck`

**Input**: Feature specification from `specs/duck-001-recommendation-rag/spec.md`

**Note**: Tài liệu trong `specs/` viết bằng tiếng Việt theo `docs/development/agent-workflow.md` §IV.7. Đường dẫn trong tài liệu dùng dạng tương đối.

---

## Summary

Recommendation Service là service nội bộ, gRPC-only, **có** sở hữu một knowledge store riêng (`recommendation_db`, PostgreSQL + pgvector) chứa corpus tri thức đã nhúng từ cuốn sách *Chế độ ăn trường thọ* (273 trang bản in, 13 chương, đã số hoá tại `docs/reference/che-do-an-truong-tho/`).

Service cung cấp 5 RPC: gợi ý bữa ăn (chỉ chọn trong danh mục an toàn do bên gọi gửi, lọc dị ứng 100% bằng code), phân tích thiếu hụt vi chất 7 ngày (tính deterministic theo bảng RDA cá nhân hoá theo nhóm tuổi/giới), chấm điểm thói quen trường thọ (rubric cố định 35/30/20/15), hỏi tri thức sách và tra cứu đoạn sách.

Mọi câu trả lời có căn cứ phải kèm **trích dẫn** (chương + trang bản in + trang scan + trích đoạn + trạng thái đối chiếu bản in). Khi corpus không đủ căn cứ thì **abstain**, không suy diễn. Corpus được nạp bằng pipeline **offline** (`scripts/book-kb`), không nằm trong runtime service.

Quyết định kiến trúc nền: [ADR 002](../../docs/decisions/002-recommendation-knowledge-base.md).

---

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) — `net10.0`; Python 3.11+ cho pipeline ingest offline (`scripts/book-kb`, chạy trong Docker)

**Primary Dependencies**:
- `Microsoft.EntityFrameworkCore` 10.0.12 · `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 · `Npgsql` 10.0.3
- `Pgvector` 0.3.2 · `Pgvector.EntityFrameworkCore` 0.3.0 (mapping `vector` cho EF Core 10 — Npgsql 10 **không** có sẵn kiểu `vector`)
- `Google.GenAI` 1.26.1 (SDK .NET chính thức cho Gemini Developer API)
- `Grpc.AspNetCore` / `Grpc.Net.Client` / `Grpc.Tools` 2.84.0 (đã có trong CPM)
- `Polly` (retry/backoff) — **cần thêm vào CPM**
- `Microsoft.Extensions.AI.Abstractions` (đi kèm `Google.GenAI`; dùng cho `IEmbeddingGenerator` nếu cần)

**Storage**: PostgreSQL 17 (image `pgvector/pgvector:pg17`, pgvector ≥ 0.8.6) — database `recommendation_db`, schema `kb`, 4 bảng: `kb.corpus`, `kb.chapter`, `kb.chunk`, `kb.page_flag`. Không sửa DB bằng tay (EF Core Code First, migration thuộc service này).

**Testing**: xUnit + `Microsoft.NET.Test.Sdk` (đã có); `Testcontainers.PostgreSql` (đã có) với image `pgvector/pgvector:pg17`; integration test gRPC server ↔ client; eval golden set 50 câu (script `scripts/book-kb/eval.py`, skip khi thiếu `GOOGLE_AI__API_KEY`)

**Target Platform**: Linux container (Docker Compose), local == production; ASP.NET Core gRPC server nội bộ `:50051` trong mạng `internal_net`

**Project Type**: service web (gRPC server) trong kiến trúc microservices

**Performance Goals**:
- Truy xuất tri thức (vector + keyword + RRF) p95 < 150ms
- Sinh câu trả lời p95 < 2.5s
- Tổng gRPC p95 < 3.5s (theo PRD §3.1)
- Ngập 100 RPS mỗi container (PRD §3.1)

**Constraints**:
- Constitution 2.1.0 §I: `recommendation_db` **MUST NOT** chứa dữ liệu người dùng; Recommendation vẫn gRPC-only, **MUST NOT** expose qua Nginx
- Không truy cập chéo DB; không import code nội bộ giữa các service
- Timeout gRPC 5.0s; retry 2 lần exponential backoff khi provider lỗi
- Mọi response có `disclaimer` (BR-04); citation bắt buộc có `verified` + `source_file` (BR-06)
- Lọc dị ứng 100% và rule 105% calo bằng **code**, không giao cho AI
- `sgrep`/secret: không log dữ liệu sức khỏe, token, API key

**Scale/Scope**:
- Corpus: 13 chương / 273 trang → ~900–1.300 chunk; ước lượng < 20 MB trong `kb`
- Embedding `gemini-embedding-001`, 768 chiều → 3.1 KB/chunk
- Phạm vi: **chỉ Recommendation**. Tích hợp phía service dinh dưỡng (gửi `candidate_foods`, hiển thị citation ra REST) → feature riêng `duck/002/diet-recommendation-integration`

**Rủi ro kỹ thuật đã biết** (Loop A của `speckit-analyze`, lần 1):

| Rủi ro | Ảnh hưởng | Xử lý |
| --- | --- | --- |
| `Google.GenAI` 1.26.1 là package 1.x, có thể phát sinh cảnh báo analyzer; repo bật `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` | `make lint` đỏ ở Phase 1/2 | Chỉ dùng API ổn định; nếu còn cảnh báo từ package ngoài, khai báo `<NoWarn>` **có chú thích lý do** trong csproj của Recommendation — không nới `TreatWarningsAsErrors` toàn cục |
| `make check` chạy `vuln` (NuGet audit) — package mới `Pgvector`, `Pgvector.EntityFrameworkCore`, `Google.GenAI`, `Polly` có thể bị audit chặn | close-out gate đỏ | Nâng phiên bản lên bản đã vá nếu có; không bỏ qua bước audit |
| `Pgvector.EntityFrameworkCore` 0.3.0 mới chỉ có asset `net8.0` và chưa có bản publish khai báo EF Core 10 | xem `research.md` D-11 | Ghim phiên bản; đường dự phòng raw SQL `$1::vector` đã được kiểm chứng |
| Lời gọi provider cần `GOOGLE_AI__API_KEY`; test/eval không có key thì **skip** | bằng chứng SC-003/SC-005 thiếu | Mọi task phụ thuộc provider phải skip **kèm log rõ lý do**, không được báo pass giả |
| `make check` chạy toàn bộ test suite của solution | có thể đỏ vì test của feature khác | Chạy và báo cáo trung thực; phần đỏ ngoài phạm vi feature phải ghi vào `deferred.md` kèm lý do |

---

## Constitution Check

*GATE: phải pass trước Phase 0. Re-check sau Phase 1.*

| Nguyên tắc | Yêu cầu | Trạng thái |
| --- | --- | --- |
| **I. Service Boundaries & Data Ownership** | Mỗi service một DB; ngoại lệ duy nhất `recommendation_db` chỉ chứa corpus tri thức, không PII | ✅ PASS — `kb.*` chỉ chứa corpus/chunk/cờ; dữ liệu người dùng đi qua gRPC request/response; **hiện** `recommendation_db` là DB thứ 5, tách khỏi `diet_db`/`identity_db`/`progress_db`, role Postgres riêng |
| **II. Đồng bộ qua gRPC** | Giao tiếp service↔service bằng gRPC, có deadline, xử lý lỗi transport tường minh | ✅ PASS — contract `contracts/recommendation/v1/recommendation.proto`; deadline 5s; Polly retry 2 + backoff; map lỗi sang gRPC `Status` + `ErrorDetails` |
| **III. Bất đồng bộ qua RabbitMQ** | Event có version, idempotent, outbox | ✅ N/A — service này **không** phát event, không consume event. Không có RabbitMQ trong `docker-compose` của service này |
| **IV. Contract-First** | Contract định nghĩa trước code; không lộ type nội bộ qua contract | ✅ PASS — `.proto` là nguồn chân lý; `Citation`/`CandidateFood` là DTO thuần; entity `KnowledgeChunk` **không** đi qua contract |
| **V. Test-First & Verified Gates** | Test viết trước, thấy đỏ trước khi thấy xanh; có ít nhất 1 đường E2E qua hạ tầng thật assert giá trị cụ thể | ✅ PASS — mỗi user story có test negative control; integration test dùng container Postgres thật + corpus thật; assert `book_page` trúng ±3 trang và `quote` khớp chunk |
| **Tech Constraints** | EF Core Code First; chỉ expose client-facing qua Nginx; dịch vụ ngoài qua adapter; secret qua env | ✅ PASS — migration do service này tạo; `recommendation` chỉ join `internal_net`+`data_net`; Google AI qua `IChatModel`/`IEmbeddingProvider` + adapter `Infrastructure/Adapters`; `GOOGLE_AI__API_KEY` từ env |
| **Git & Workflow** | Không tự ý thao tác git; docs trong `specs/` bằng tiếng Việt; đường dẫn tương đối | ✅ PASS — commit/push chỉ khi được phép; tài liệu `specs/` bằng tiếng Việt; mọi đường dẫn trong tài liệu là tương đối |

**Kết luận: PASS — không có vi phạm nào cần ghi vào Complexity Tracking.**

---

## Project Structure

### Documentation (this feature)

```text
specs/duck-001-recommendation-rag/
├── plan.md              # File này
├── spec.md              # Đặc tả (đã có, đã clarify)
├── research.md          # Phase 0 — 14 quyết định kỹ thuật
├── data-model.md        # Phase 1 — mô hình dữ liệu kb.* + DTO nội bộ
├── quickstart.md        # Phase 1 — hướng dẫn kiểm chứng end-to-end
├── contracts/
│   └── recommendation.proto   # Contract gRPC (bản authoritative)
├── checklists/
│   └── requirements.md  # Checklist chất lượng đặc tả (16/16)
└── meta.json            # Nguồn sự thật: owner/number/slug/branch/status/services
```

### Source Code (repository root)

```text
src/Services/Recommendation/
├── Recommendation.Domain/          # C# thuần: constant, model, VO — KHÔNG EF/ASP.NET
│   ├── Constants/                  # MicronutrientRda, LongevityRubric, AllergenAliases, DisclaimerText
│   └── Models/                     # KnowledgeChunk, RetrievedChunk, Citation, UserProfile, CandidateFood
├── Recommendation.Application/     # use case, DTO, abstraction, validator — chỉ biết Domain
│   ├── Abstractions/                # IKnowledgeStore, IEmbeddingProvider, IChatModel, IClock
│   ├── Features/
│   │   ├── Meals/                  # GetMealRecommendations + AllergenGuard + CalorieCapRule
│   │   ├── NutrientGaps/            # AnalyzeNutrientGaps + MicronutrientRdaCalculator
│   │   ├── LongevityHabits/         # EvaluateLongevityHabits + LongevityRubricCalculator
│   │   ├── Ask/                     # AskLongevityQuestion + PromptBuilder + CitationResolver
│   │   └── Search/                  # SearchLongevityKnowledge
│   └── Common/{Dtos,Mappers,Validators,Errors}/
├── Recommendation.Infrastructure/  # EF Core, adapter Google AI, gRPC server
│   ├── Persistence/
│   │   ├── KnowledgeDbContext.cs
│   │   ├── Entities/                # Corpus, Chapter, Chunk, PageFlag
│   │   ├── Configurations/          # IEntityTypeConfiguration cho từng bảng
│   │   └── Migrations/
│   ├── Adapters/
│   │   ├── GoogleAi/                # GoogleAiEmbeddingProvider, GoogleAiChatModel
│   │   ├── Knowledge/               # PgKnowledgeStore (vector + keyword + RRF)
│   │   └── Grpc/                    # RecommendationGrpcService (server)
│   └── Options/                    # GoogleAiOptions, RagOptions, KnowledgeDbOptions
├── Recommendation.Api/             # host gRPC :50051 (h2c, nội bộ) + /health :8080, composition root, middleware
├── Dockerfile                     # nằm trong Recommendation.Api/ (theo tasks.md T006)
└── tests/
    ├── Recommendation.UnitTests/
    └── Recommendation.IntegrationTests/   # Testcontainers pgvector + gRPC server

scripts/book-kb/                     # Pipeline ingest OFFLINE (Python, Docker)
├── Dockerfile · compose.yml · requirements.txt
└── src/
    ├── cli.py            # build | embed | load | verify | eval
    ├── corpus.py         # đọc .manifest.json + marker "sách N · PDF M"
    ├── clean.py          # làm sạch OCR, bỏ [[HINH:n]], chuẩn hoá bảng
    ├── chunk.py          # 1000 token / overlap 150, không cắt bảng
    ├── review.py         # parse review/numbers.md + review/qa.md → kb.page_flag
    ├── embed.py          # gemini-embedding-001, 768 chiều, L2-normalize, cache theo hash
    ├── loader.py         # upsert idempotent vào recommendation_db
    └── eval.py           # golden set 50 câu → groundedness report

contracts/recommendation/v1/recommendation.proto   # Contract gRPC (mới)
docs/development/rag-ingestion.md                 # Quy ước ingest (mới)
```

**Structure Decision**: giữ đúng 4 project + 2 test project như `src/Services/Identity/` (mẫu chuẩn của repo, theo `docs/system-design/design-pattern.md` §II). Không thêm project thứ 5. Pipeline ingest nằm ngoài `src/` vì nó **không** chạy trong runtime service và dùng Python — cùng cách `scripts/book-ocr` đã tồn tại cho pipeline OCR.

---

## Shared Files & Conflict Hotspots

Theo `docs/development/team-workflow.md` §V, các path dưới đây **không thuộc riêng feature này** và phải được khai báo. Feature này **được phép sửa** chúng vì `meta.json.services = ["recommendation"]` và chưa feature nào khác đang mở.

| Path | Mức độ | Thay đổi dự kiến | Cách xử lý |
| --- | --- | --- | --- |
| `Directory.Packages.props` | 🔴 cao | Thêm `Pgvector`, `Pgvector.EntityFrameworkCore`, `Google.GenAI`, `Polly` | Gom vào **PR nhỏ riêng** và merge sớm để không chặn feature khác |
| `longevia-be.slnx` | 🔴 cao | Thêm 6 project | Cùng PR nhỏ với `Directory.Packages.props` |
| `src/Shared/**` | 🔴 cao | Tạo `AppException` + `FieldError` tại `src/Shared/Errors/` (theo `docs/system-design/design-pattern.md` §III) | **Xung đột với feature Identity** (cũng cần loại lỗi dùng chung) → merge sớm hoặc thống nhất với owner Identity trước khi code |
| `deployments/docker-compose.yml` | 🟡 | `postgres` → `pgvector/pgvector:pg17`; thêm service `recommendation` (internal_net + data_net) | Khai báo; merge sớm vì đổi image Postgres ảnh hưởng mọi service |
| `deployments/postgres/init/01-databases.sh` | 🟡 | Tạo `recommendation_db` + role `recommendation_user` | Cùng PR với compose |
| `.env.example` | 🟡 | `KNOWLEDGE_DB`, `ConnectionStrings__Recommendation`, `RAG__*`, `GOOGLE_AI__EMBEDDING_*` | Khai báo |
| `Makefile` | 🟡 | `kb-build`, `kb-load`, `kb-verify`, `kb-eval` | Khai báo |
| `contracts/recommendation/v1/recommendation.proto` | 🟢 thấp | **File mới**, chưa feature nào tạo | Tạo mới; đây là contract authoritative của feature |
| `docs/api/api-spec.md`, `docs/requirements/prd.md` | 🟢 | Đã cập nhật ở Phase 0 (commit `475acf3`, `b8e5bc3`); chỉ chỉnh nếu contract đổi | Kiểm tra lại ở checkpoint |
| `docs/system-design/design-pattern.md` | 🔴 cao | §3.2 và §IV.2 dùng cú pháp `with { Code = ... }` trên `AppException` — **không compile** (C# cấm `record` kế thừa `Exception`) | Đã sửa sang `WithCode(...)` / `WithErrors(...)`; **phải** thống nhất với feature Identity trước khi merge `src/Shared` |
| `docs/development/local-setup.md`, `README.md` | 🟡 | Lệnh compose phải có `--env-file .env` (compose ở `deployments/` không tự đọc `.env` ở gốc) | Đã sửa; xem D-07 trong `deferred.md` |
| `src/Services/Diet/**` | 🔴 **không được sửa** | — | Tích hợp Diet tách sang feature `duck/002` |

**Không tạo migration cho service khác**: feature này chỉ tạo migration trong `Recommendation.Infrastructure` (database `recommendation_db`) — không xung đột với feature khác theo `team-workflow.md` §VI.

---

## Phase 0 — Nghiên cứu (đã hoàn tất)

Xem [research.md](./research.md): 14 quyết định kỹ thuật (D-01…D-14), mỗi quyết định kèm **lý do** và **phương án đã cân nhắc**. Không còn `NEEDS CLARIFICATION`.

## Phase 1 — Thiết kế (đã hoàn tất)

- [data-model.md](./data-model.md) — 4 bảng `kb.*`, quy tắc tính cờ `verified`/`needs_review`, cây RRF, entity ↔ bảng ↔ hợp đồng
- [contracts/recommendation.proto](./contracts/recommendation.proto) — 5 RPC, message `Citation`/`CandidateFood`/`ScoreComponent`/`NutrientGap`/`Disclaimer`
- [quickstart.md](./quickstart.md) — 6 kịch bản kiểm chứng end-to-end

**Constitution Check sau thiết kế**: PASS — các quyết định ở Phase 1 không làm phát sinh vi phạm mới. Điểm cần chú ý: `kb.chunk` **không** có cột nào chứa dữ liệu người dùng; `CandidateFood`/`UserProfile` chỉ tồn tại trong request/response gRPC, không đi qua `recommendation_db`.

---

## Điều kiện hoàn thành (Definition of Done)

1. `contracts/recommendation/v1/recommendation.proto` có 5 RPC, đồng bộ `docs/api/api-spec.md` §5 và PRD §2.4/§4.
2. `recommendation_db` chạy trong compose; `kb.chunks` có ~900–1.300 chunk từ 273 trang, mỗi chunk có `book_page`, `pdf_page`, `verified`, `needs_review`.
3. 3 RPC gốc trả lời có căn cứ (`citations[]`) + `disclaimer`; gap vi chất và điểm thói quen **deterministic**, có `gaps[]` / `score_breakdown[]`.
4. 2 RPC mới: hỏi sách (có citation, `abstained` khi thiếu căn cứ) và tra cứu đoạn.
5. Lọc dị ứng bằng code ở Recommendation; `calorie_cap_reached` đúng rule 105%; ≤ 5 món mỗi bữa.
6. Constitution 2.1.0 + ADR 002 + tài liệu đồng bộ (đã merge ở Phase 0).
7. Unit + Integration + E2E xanh; eval golden set chạy được và có báo cáo groundedness.
8. `make check` xanh trước khi push.

---

## Complexity Tracking

> Chỉ điền nếu Constitution Check có vi phạm cần biện minh.

| Vi phạm | Vì sao cần | Phương án đơn giản bị loại vì |
| --- | --- | --- |
| (không có) | — | — |

**Ngoài hiến pháp nhưng cần ghi nhận**: dự án thêm package `Pgvector.EntityFrameworkCore` (bên thứ ba) vì Npgsql 10 chưa hỗ trợ kiểu `vector` — rủi ro được giới hạn bằng cách ghim phiên bản `0.3.0` và cô lập toàn bộ truy cập vector sau interface `IKnowledgeStore` để đổi backend không phải sửa use case. Chi tiết ở `research.md` D-11.

---

## Quyết định trong quá trình triển khai

Ghi lại các quyết định orchestrator tự chốt khi triển khai (theo skill §9), kèm lý do — để người đọc không phải suy đoán.

| # | Quyết định | Lý do |
| --- | --- | --- |
| I-01 | Cổng Kestrel đọc từ `RECOMMENDATION__GRPC_PORT` / `RECOMMENDATION__HTTP_PORT` (mặc định 50051 / 8080) thay vì hard-code | Constitution yêu cầu cấu hình qua biến môi trường; hard-code là vi phạm |
| I-02 | `RAG__EMBEDDING_MODEL` + `RAG__EMBEDDING_DIM` là **nguồn duy nhất** của model/số chiều; bỏ `GOOGLE_AI__EMBEDDING_MODEL` | Hai nguồn cho cùng một giá trị sẽ lệch giữa corpus và câu hỏi → hỏng truy xuất im lặng |
| I-03 | Không dùng `env_file: ../.env` cho service; giữ `environment:` tường minh + tài liệu hoá `docker compose --env-file .env ...` | `env_file` nạp toàn bộ `.env`, kéo cả `JWT__SIGNING_PRIVATE_KEY` của Identity vào container → vi phạm ADR 001 |
| I-04 | `RAG__MIN_CITATION_SIMILARITY = 0.75` (không phải 0.6) | Trích dẫn là tính chất an toàn cốt lõi (BR-06); ngưỡng lỏng hơn dễ gắn trích dẫn không thuộc chunk |
| I-05 | `TaskType` tách hai chiều: `RETRIEVAL_QUERY` cho câu hỏi, `RETRIEVAL_DOCUMENT` cho chunk | Theo research D-12; dùng sai chiều làm giảm chất lượng truy xuất |
| I-06 | Giữ `IKnowledgeStore.UpsertAsync` | Cần để seed corpus nhỏ trong integration test; đường ghi chuẩn vẫn là pipeline offline (D-02) |
| I-07 | Tách composition root sang `Recommendation.Api/RecommendationStartup.cs`, `Program.cs` chỉ gọi | Integration test cần dựng host Kestrel thật trong tiến trình mà không thêm `Microsoft.AspNetCore.Mvc.Testing` vào CPM |
| I-08 | Model sinh câu trả lời: `gemini-3.5-flash-lite` (`GOOGLE_AI__CHAT_MODEL`) | Theo yêu cầu của dev: dùng bản lite thay cho `gemini-3.5-flash` |
| I-09 | Ngưỡng lọc sở thích ăn uống (spec không định nghĩa): Keto ≤ 10 g carbs/khẩu phần, LowCarb ≤ 30 g, Mediterranean không loại trừ, Vegan so khớp từ khoá có dấu | Cần một giá trị để kiểm thử; là hằng số trong `Domain/Constants/DietaryPreferenceRules.cs`, đổi được mà không ảnh hưởng contract. **Dev nên xác nhận** |
| I-10 | `RecommendedMeal.description` và `key_micronutrients` trả rỗng ở US1 | `CandidateFood` không có dữ liệu tương ứng; bịa là vi phạm FR-005. Enrich thuộc feature `duck/002` khi Diet gửi dữ liệu đầy đủ hơn |
| I-11 | So khớp dị ứng bỏ dấu → có thể dương tính giả (dị ứng "cá" loại cả món có "cà") | Chọn hướng **an toàn** (loại nhầm còn hơn để lọt); ghi nhận để cân nhắc khi có dữ liệu dị ứng chuẩn hoá từ Identity |
