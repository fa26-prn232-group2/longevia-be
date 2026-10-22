---

description: "Task list for feature duck/001/recommendation-rag"

---

# Tasks: Recommendation Service với RAG tri thức sách "Chế độ ăn trường thọ"

**Input**: Design documents from `specs/duck-001-recommendation-rag/`
**Feature ID**: `duck/001/recommendation-rag` · **Branch**: `feat/recommendation-rag`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/recommendation.proto](./contracts/recommendation.proto), [quickstart.md](./quickstart.md)

**Tests**: **BẮT BUỘC** — Constitution V (NON-NEGOTIABLE) yêu cầu test viết trước hoặc cùng code và phải **thấy đỏ trước khi thấy xanh**. Mỗi phase có test task riêng, đặt trước task implementation cùng story.

**Organization**: Task nhóm theo user story để mỗi story triển khai và kiểm thử **độc lập**.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: chạy song song được (file khác nhau, không phụ thuộc task chưa xong)
- **[Story]**: user story thuộc về (US1..US6 theo [spec.md](./spec.md))
- Mọi task có **đường dẫn file chính xác**

---

## Phase 1: Setup (Hạ tầng dùng chung)

**Purpose**: Khởi tạo service, hạ tầng chạy được, cấu hình môi trường.

- [x] T001 Tạo 4 project `Recommendation.Domain`, `Recommendation.Application`, `Recommendation.Infrastructure`, `Recommendation.Api` (classlib, `net10.0`) và 2 project test `Recommendation.UnitTests`, `Recommendation.IntegrationTests` (xunit) trong `src/Services/Recommendation/`
- [x] T002 [P] Thiết lập reference: `Recommendation.Api` → `Recommendation.Infrastructure` → `Recommendation.Application` → `Recommendation.Domain`; `Recommendation.Infrastructure` → `Recommendation.Domain`; `Recommendation.Application` → `Longevia.Shared` (cần cho `AppException` ở T005); test project → `Recommendation.Api`/`Recommendation.Application`
- [x] T003 [P] Thêm 6 project mới vào `longevia-be.slnx`
- [x] T004 [P] Thêm package version vào `Directory.Packages.props`: `Pgvector` 0.3.2, `Pgvector.EntityFrameworkCore` 0.3.0, `Google.GenAI` 1.26.1, `Polly` (bản 8.x), `Grpc.AspNetCore.HealthChecks` 2.84.0 (cần cho gRPC health service), `Testcontainers.PostgreSql` (đã có — chỉ dùng)
- [x] T005 [P] Tạo `src/Shared/Errors/AppException.cs` và `src/Shared/Errors/FieldError.cs` theo `docs/system-design/design-pattern.md` §III (mã lỗi ổn định dạng `recommendation.*`, `Status`, `Title`, `Code`, `Errors`) — **xung đột với feature Identity, merge sớm**
- [x] T006 [P] Tạo `src/Services/Recommendation/Recommendation.Api/Dockerfile` (multi-stage, `net10.0` runtime, chạy `Recommendation.Api`)
- [x] T007 [P] Đổi image `postgres:17` → `pgvector/pgvector:pg17` và thêm service `recommendation` (chỉ `internal_net` + `data_net`, **không** `edge`) trong `deployments/docker-compose.yml`
- [x] T008 [P] Tạo `recommendation_db` + role `recommendation_user` (cần quyền `CREATE EXTENSION`) trong `deployments/postgres/init/01-databases.sh`
- [x] T009 [P] Thêm biến môi trường vào `.env.example`: `KNOWLEDGE_DB`, `ConnectionStrings__Recommendation`, `RAG__TOP_K=8`, `RAG__MAX_CANDIDATES=50`, `RAG__EMBEDDING_MODEL=gemini-embedding-001`, `RAG__EMBEDDING_DIM=768`, `RAG__REQUIRE_VERIFIED=false`, `RAG__MIN_CITATION_SIMILARITY=0.75`, `RAG__CHUNK_TOKENS=1000`, `RAG__CHUNK_OVERLAP=150`, `RECOMMENDATION__GRPC_PORT=50051`, `RECOMMENDATION__HTTP_PORT=8080` (comment tiếng Anh)
- [x] T010 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Options/GoogleAiOptions.cs` (ApiKey, ChatModel, TimeoutMs; **không** có EmbeddingModel/OutputDimensionality — nguồn duy nhất là `RagOptions`) — validate khi khởi động
- [x] T011 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Options/RagOptions.cs` (TopK=8, MaxCandidates=50, RequireVerified=false, ChunkTokens=1000, ChunkOverlap=150, MinCitationSimilarity=0.75, **EmbeddingModel=gemini-embedding-001**, **EmbeddingDim=768**)
- [x] T012 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Options/KnowledgeDbOptions.cs` (ConnectionString, CorpusKey, Schema)
- [x] T013 Viết `src/Services/Recommendation/Recommendation.Api/Program.cs`: composition root duy nhất — `AddGrpc`, `AddNpgsql(..., o => o.UseVector())`, đăng ký `Client` Google AI singleton (`new Client(apiKey:)`), đăng ký use case + adapter theo interface, `MapGrpcService`, health check `/health` + gRPC health service, chạy `MigrateAsync()` **trước** khi phục vụ request, **timeout phía server 5.0s cho mỗi lời gọi** (`ServerCallContext` deadline + `CancellationToken` truyền xuống use case, theo PRD §3.1), **cổng lấy từ biến môi trường** `RECOMMENDATION__GRPC_PORT` (mặc định 50051, chỉ Http2/h2c) và `RECOMMENDATION__HTTP_PORT` (mặc định 8080, chỉ Http1) — không hard-code cổng, `app.Run()`
- [x] T014 Tạo `src/Services/Recommendation/Recommendation.Api/Middlewares/GrpcExceptionInterceptor.cs` — map `AppException` sang gRPC `Status` (`InvalidArgument`/`NotFound`/`FailedPrecondition`/`Unavailable`/`Internal`) + `ErrorDetails` chứa `Code` ổn định
- [x] T015 [P] Thiết lập structured logging + OpenTelemetry metrics cho Recommendation trong `src/Services/Recommendation/Recommendation.Api/Extensions/TelemetryExtensions.cs` (không log dữ liệu sức khỏe, token, API key)
- [x] T016 [P] Thêm target `kb-build`, `kb-load`, `kb-verify`, `kb-eval` vào `Makefile` (gọi `docker compose -f scripts/book-kb/compose.yml`)
- [x] T017 [P] Tạo khung `scripts/book-kb/` với `Dockerfile`, `compose.yml`, `requirements.txt` (`google-genai>=1,<2`, `psycopg[binary]`) và thêm `artifacts/book-kb/` vào `.gitignore`

**Checkpoint**: `make lint` xanh, `docker compose up -d postgres` chạy được với `pgvector/pgvector:pg17` và `recommendation_db` đã tạo.

---

## Phase 2: Foundational (Prerequisites chặn mọi user story)

**Purpose**: Contract, mô hình dữ liệu, migration, abstraction và hạ tầng truy xuất + sinh câu trả lời mà **mọi** story đều dùng.

**⚠️ CRITICAL**: Không story nào bắt đầu được cho tới khi phase này xong.

### Contract

- [x] T018 Tạo `contracts/recommendation/v1/recommendation.proto` theo bản thiết kế `specs/duck-001-recommendation-rag/contracts/recommendation.proto`: 5 RPC (`GetMealRecommendations`, `AnalyzeNutrientGaps`, `EvaluateLongevityHabits`, `AskLongevityQuestion`, `SearchLongevityKnowledge`), enum `MealType`/`BiologicalSex`/`UserGoal`, message `UserProfile`/`Citation`/`ScoreComponent`/`Disclaimer`/`CandidateFood`/`NutrientGap`/`KnowledgeSnippet` — **giữ nguyên số field và thứ tự** như bản thiết kế; include file này trong `Recommendation.Api.csproj` với `GrpcServices="Server"` và trong `Recommendation.IntegrationTests.csproj` với `GrpcServices="Client"` (đường dẫn tương đối từ project: `../../../contracts/recommendation/v1/recommendation.proto`)
- [x] T019 Tạo `contracts/recommendation/v1/README.md` mô tả quy ước đặt tên field, quy tắc `book_page` (bản in) vs `pdf_page` (scan) và nguồn chân lý

### Mô hình dữ liệu

- [x] T020 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Entities/Corpus.cs` — cột: `Id` uuid PK, `CorpusKey`, `Version`, `Title`, `SourcePath`, `ChapterCount`, `ChunkCount`, `EmbeddingModel`, `EmbeddingDim`, `ChunkerParams` jsonb, `IsActive`, `EmbeddedAt`, `CreatedAt`; unique `(CorpusKey, Version)`; unique index có điều kiện `(CorpusKey) WHERE IsActive`
- [x] T021 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Entities/Chapter.cs` — `Id` uuid PK, `CorpusId` FK cascade, `ChapterNumber`, `Title`, `SourceFile`, `BookPageFrom/To`, `PdfPageFrom/To`; unique `(CorpusId, ChapterNumber)`
- [x] T022 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Entities/Chunk.cs` — `Id` bigserial PK, `CorpusId`/`ChapterId` FK cascade, `ChapterNumber` (denormalized), `ChunkIndex`, `Content`, `ContentTsv` tsvector, `TokenCount`, `ContentHash`, `BookPageFrom/To`, `PdfPageFrom/To`, `Verified`, `NeedsReview`, `Embedding` vector(768), `CreatedAt`; unique `(CorpusId, ChapterId, ChunkIndex)`
- [x] T023 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Entities/PageFlag.cs` — `Id` bigserial PK, `CorpusId` FK cascade, `PdfPage`, `BookPage`, `Kind`, `SourceFile`, `Note`; unique `(CorpusId, PdfPage, Kind, SourceFile)`
- [x] T024 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/KnowledgeDbContext.cs` — chỉ trỏ `recommendation_db`, `HasDefaultSchema("kb")`, không tham chiếu DB khác
- [x] T025 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Configurations/CorpusConfiguration.cs`
- [x] T026 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Configurations/ChapterConfiguration.cs`
- [x] T027 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Configurations/ChunkConfiguration.cs` — cột `Embedding` kiểu `Pgvector.Vector` với `HasColumnType("vector(768)")`, `ContentTsv` kiểu `NpgsqlTsVector`, index `GIN(ContentTsv)`, index HNSW `.HasMethod("hnsw").HasOperators("vector_cosine_ops").HasStorageParameter("m", 16).HasStorageParameter("ef_construction", 64)`, index `(CorpusId, ChapterNumber)` và `(BookPageFrom, BookPageTo)`
- [x] T028 [P] Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Persistence/Configurations/PageFlagConfiguration.cs`
- [x] T029 Tạo migration `InitialKnowledgeStore` bằng `make ef-add NAME=InitialKnowledgeStore PROJECT=src/Services/Recommendation/Recommendation.Infrastructure`; **review file sinh ra** trước khi áp dụng; trước khi chạy lệnh phải `git fetch origin && git merge origin/main` để timestamp migration không thấp hơn migration mới nhất (theo `docs/development/migration.md`)
- [x] T030 Bổ sung vào migration `InitialKnowledgeStore`: `CREATE EXTENSION IF NOT EXISTS unaccent;` và trigger duy trì `content_tsv` (`BEFORE INSERT OR UPDATE OF content` → `NEW.content_tsv := to_tsvector('simple', unaccent(NEW.content))`) bằng `migrationBuilder.Sql(...)` — `vector` extension do `HasPostgresExtension("vector")` sinh ra
- [x] T031 Áp dụng migration trên container thật (`make ef-update PROJECT=src/Services/Recommendation/Recommendation.Infrastructure`) và xác nhận có 4 bảng trong schema `kb` + index HNSW + trigger `tsvector`

### Abstraction (Application)

- [x] T032 Tạo `src/Services/Recommendation/Recommendation.Application/Abstractions/IKnowledgeStore.cs` — `SearchAsync(KnowledgeQuery, CancellationToken)` trả `IReadOnlyList<RetrievedChunk>`, `UpsertAsync(...)`, `CountAsync(...)`
- [x] T033 [P] Tạo `src/Services/Recommendation/Recommendation.Application/Abstractions/IEmbeddingProvider.cs` — `EmbedAsync(string, CancellationToken)` và `EmbedBatchAsync(IReadOnlyList<string>, CancellationToken)` trả `IReadOnlyList<float[]>`
- [x] T034 [P] Tạo `src/Services/Recommendation/Recommendation.Application/Abstractions/IChatModel.cs` — `CompleteAsync(StructuredPrompt, CancellationToken)` trả JSON đã parse
- [x] T035 [P] Tạo `src/Services/Recommendation/Recommendation.Application/Abstractions/IClock.cs` — dùng để test thời gian
- [x] T036 [P] Tạo model nội bộ trong `src/Services/Recommendation/Recommendation.Domain/Models/`: `KnowledgeChunk.cs`, `RetrievedChunk.cs`, `Citation.cs`, `UserProfile.cs`, `CandidateFood.cs`, `NutrientGap.cs`, `ScoreComponent.cs`, `KnowledgeQuery.cs` (C# thuần, không EF/ASP.NET)

### Adapter Google AI

- [x] T037 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/GoogleAi/GoogleAiEmbeddingProvider.cs` — **model và số chiều đọc từ `RagOptions.EmbeddingModel` / `RagOptions.EmbeddingDim`** (nguồn duy nhất, phải khớp cột `vector(n)` của `kb.chunk`); `TaskType` tách đúng hai chiều theo research D-12 — `RETRIEVAL_QUERY` khi nhúng **câu hỏi lúc truy xuất**, `RETRIEVAL_DOCUMENT` khi nhúng **tài liệu/chunk**; đọc `EmbedContentResponse.Embeddings[i].Values`; **L2-normalize** trước khi trả về (D-13); retry + backoff khi lỗi quota
- [x] T038 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/GoogleAi/GoogleAiChatModel.cs` — `GenerateContentAsync` với `GenerateContentConfig { ResponseMimeType = "application/json", ResponseJsonSchema = JsonNode.Parse(schema), SystemInstruction = new Content { Parts = [new Part { Text = ... }] }, Temperature = 0.1 }`; parse JSON trả về; retry 2 lần exponential backoff; lỗi parse → trả `abstain`
- [x] T039 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/GoogleAi/SchemaFactory.cs` — sinh JSON schema cho từng loại response dưới dạng `JsonNode` (thứ tự property cố định)

### Truy xuất tri thức

- [x] T040 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/Knowledge/PgKnowledgeStore.cs` — hiện thực `IKnowledgeStore`: vector top-50 (`embedding <=> @qvec`) + keyword top-50 (`content_tsv @@ plainto_tsquery('simple', unaccent(@q))`) → merge RRF `SUM(1.0/(60+rank))` → top-k; **projection** không select cột `embedding`
- [x] T041 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/Knowledge/ChunkSearchResult.cs` — keyless entity cho `FromSqlRaw`, trả `Id, CorpusId, Content, ChapterNumber, ChapterTitle, SourceFile, BookPageFrom, BookPageTo, PdfPageFrom, PdfPageTo, Verified, NeedsReview, RrfScore` (`ChapterTitle`/`SourceFile` đến từ `JOIN kb.chapter`)
- [x] T042 Tạo `src/Services/Recommendation/Recommendation.Infrastructure/Adapters/Knowledge/VectorQueryBuilder.cs` — chuyển `float[]` → chuỗi `[0.1,0.2,...]` + ép kiểu `$1::vector`; query vector câu hỏi cũng phải L2-normalize
- [x] T043 Tạo `src/Services/Recommendation/Recommendation.Application/Features/Search/QueryNormalizer.cs` — bỏ dấu tiếng Việt ở phía **query** (dùng `unaccent` trong SQL, không tự viết hàm bỏ dấu trong C#)

### Sinh câu trả lời có căn cứ

- [x] T044 Tạo `src/Services/Recommendation/Recommendation.Application/Features/Ask/PromptBuilder.cs` — 3 tầng ràng buộc (research.md D-10): grounding + abstain; unverified guard ("chưa đối chiếu bản in"); corpus-as-data (chống prompt injection); nội dung sách luôn gắn nhãn là dữ liệu
- [x] T045 Tạo `src/Services/Recommendation/Recommendation.Application/Features/Ask/CitationResolver.cs` — verify `quote` khớp chuỗi với `content` của chunk (chuẩn hoá whitespace + bỏ dấu khi so khớp); citation không khớp → loại; mất hết citation → hạ response xuống `abstained = true`
- [x] T046 Tạo `src/Services/Recommendation/Recommendation.Application/Common/Errors/RecommendationErrors.cs` — factory `AppException` với mã ổn định `recommendation.*` (vd `recommendation.no_candidate_food`, `recommendation.knowledge_unavailable`, `recommendation.provider_unavailable`)

**Checkpoint**: migration đã áp dụng trên Postgres thật; `IKnowledgeStore` trả về chunk thật từ `kb.chunk`; `make lint` + `make test PROJECTS=src/Services/Recommendation` xanh.

---

## Phase 3: User Story 1 - Gợi ý bữa ăn chắc chắn an toàn (Priority: P1) 🎯 MVP

**Goal**: Gợi ý tối đa 5 món từ danh mục an toàn do bên gọi gửi, lọc dị ứng 100% bằng code, không vượt rule 105% calo, kèm căn cứ trong sách.

**Independent Test**: Gọi `GetMealRecommendations` với danh mục ứng viên + hồ sơ dị ứng → nhận ≤ 5 món, **không món nào chứa thành phần dị ứng**, mỗi món có `food_id` thuộc danh mục và `estimated_calories` bằng đúng giá trị món gốc, response có `disclaimer`.

### Tests cho User Story 1

- [x] T047 [P] [US1] Unit test `AllergenGuard` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/AllergenGuardTests.cs` — loại 100% món khớp dị ứng (khớp chính xác, khớp chứa, khớp không phân biệt hoa thường, khớp tiếng Việt có dấu/không dấu); **viết trước và phải thấy đỏ**
- [x] T048 [P] [US1] Unit test `CalorieCapRule` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/CalorieCapRuleTests.cs` — calo đã ăn ≥ 105% mục tiêu → chặn bữa ăn chính; < 105% → cho phép; biên đúng 105%
- [x] T049 [P] [US1] Unit test `DietaryPreferenceFilter` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/DietaryPreferenceFilterTests.cs` — Vegan loại thực phẩm thịt/trứng/sữa; Keto loại carbs vượt ngưỡng
- [x] T050 [P] [US1] Unit test `GetMealRecommendationsUseCase` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/GetMealRecommendationsUseCaseTests.cs` — không vượt 5 món; dinh dưỡng lấy từ `CandidateFood` chứ không tự tính; mọi món có `food_id` trong danh mục; response luôn có `disclaimer`; ánh xạ đủ 4 bữa `BREASTFAST`/`LUNCH`/`DINNER`/`SNACK` sang nhóm gợi ý tương ứng (FR-007)
- [x] T051 [US1] Integration test gRPC trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Meals/GetMealRecommendationsGrpcTests.cs` (dựng **Kestrel thật trong tiến trình** qua `RecommendationStartup` + Testcontainers `pgvector/pgvector:pg17`; không dùng `WebApplicationFactory` vì repo không có `Microsoft.AspNetCore.Mvc.Testing`) — gọi server thật qua `Grpc.Net.Client` với danh mục chứa món dị ứng, assert không món nào chứa thành phần dị ứng và `calorie_cap_reached = true` khi vượt trần

### Implementation cho User Story 1

- [x] T052 [P] [US1] Tạo `src/Services/Recommendation/Recommendation.Domain/Constants/AllergenAliases.cs` — ánh xạ thành phần dị ứng tiếng Việt ↔ tiếng Anh/sinh học (đậu phộng/peanut, sữa/milk/lactose, hải sản/shellfish…)
- [x] T053 [P] [US1] Tạo `src/Services/Recommendation/Recommendation.Domain/Constants/DietaryPreferenceRules.cs`
- [x] T054 [US1] Tạo `src/Services/Recommendation/Recommendation.Application/Features/Meals/AllergenGuard.cs` — lọc theo `profile.allergies` × `candidate_foods.allergens` + khớp tên món; **quy tắc bằng code, không giao cho AI**
- [x] T055 [US1] Tạo `src/Services/Recommendation/Recommendation.Application/Features/Meals/CalorieCapRule.cs` — `consumed_calories >= 1.05 * target_calories` → không gợi ý bữa ăn chính, chỉ đồ uống 0 calo + mẹo thư giãn/giấc ngủ
- [x] T056 [US1] Tạo `src/Services/Recommendation/Recommendation.Application/Features/Meals/DietaryPreferenceFilter.cs`
- [x] T057 [US1] Tạo `src/Services/Recommendation/Recommendation.Application/Features/Meals/GetMealRecommendationsUseCase.cs` — thứ tự: lọc dị ứng → lọc sở thích → rule 105% → chọn tối đa 5 món bằng AI trong tập đã lọc → gắn `rationale` + `citations`; `longevity_tip` lấy từ RAG, fallback sang tip tĩnh trong `Domain/Constants`
- [x] T058 [US1] Tạo `src/Services/Recommendation/Recommendation.Domain/Constants/DisclaimerText.cs` — nội dung khuyến cáo BR-04
- [x] T059 [US1] Tạo `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs` (partial) — hiện thực `GetMealRecommendations`, map DTO nội bộ ↔ message `contracts/recommendation/v1/recommendation.proto`, deadline 5s
- [x] T060 [US1] Tạo `src/Services/Recommendation/Recommendation.Api/Mapping/MealRecommendationMapper.cs` — map `CandidateFood` → `RecommendedMeal`, `Citation` → message `Citation`
- [x] T061 [US1] Viết test hồi quy prompt cho `GetMealRecommendations` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/MealPromptTests.cs` — assert prompt có lệnh "chỉ dùng nguyên liệu trong danh mục", có tầng unverified guard, không có tên món nào ngoài danh mục
- [x] T062 [US1] Cập nhật `docs/api/api-spec.md` §5 và `docs/requirements/prd.md` §2.4 nếu hợp đồng thực tế lệch với bản thiết kế `contracts/recommendation.proto`

**Checkpoint**: User Story 1 chạy độc lập — gọi được gRPC, lọc dị ứng đúng, không vượt 5 món.

---

## Phase 4: User Story 2 - Tri thức sách luôn được cập nhật (Priority: P2)

**Goal**: Nạp toàn bộ 273 trang sách vào kho tri thức bằng pipeline offline, idempotent, mỗi chunk truy được về chương và trang sách.

**Independent Test**: Chạy `make kb-build` + `make kb-load` hai lần → số chunk trong `kb.chunk` không nhân đôi; mọi chunk có `book_page_from/to` và `pdf_page_from/to` đúng theo marker.

### Tests cho User Story 2

- [x] T063 [P] [US2] Unit test chunker trong `scripts/book-kb/tests/test_chunk.py` — cắt đúng ở ranh giới đoạn; **không cắt giữa bảng**; overlap 150 token; chunk không vượt 1000 token (trừ bảng nguyên vẹn)
- [x] T064 [P] [US2] Unit test parser marker trong `scripts/book-kb/tests/test_corpus.py` — parse `<!-- sách N · PDF M -->` đúng; file thiếu marker → **fail fast**; đọc metadata chương từ `.manifest.json` khớp `book_pages`/`pdf_pages`
- [x] T065 [P] [US2] Unit test `review.py` trong `scripts/book-kb/tests/test_review.py` — parse `review/qa.md` và `review/numbers.md` theo **số trang PDF**; `table_verified`/`manual_verified` ghi đè cờ nghi vấn; tính `verified`/`needs_review` cho chunk theo [data-model.md](./data-model.md) §4
- [x] T066 [US2] Unit test `embed.py` trong `scripts/book-kb/tests/test_embed.py` — L2-normalize vector đúng (norm ≈ 1); cache theo hash nội dung (gọi lại không tốn tiền)
- [x] T067 [US2] Integration test idempotency trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Persistence/KnowledgeLoadTests.cs` — nạp cùng `version` 2 lần → số dòng `kb.chunk` không đổi; `UNIQUE (CorpusId, ChapterId, ChunkIndex)` được tôn trọng

### Implementation cho User Story 2

- [x] T068 [US2] Tạo `scripts/book-kb/src/corpus.py` — đọc `.manifest.json` + các file `NN-*.md`; trả về chương (number, title, source_file, book_pages, pdf_pages) + nội dung theo trang
- [x] T069 [US2] Tạo `scripts/book-kb/src/clean.py` — bỏ placeholder `[[HINH:n]]`, chuẩn hoá whitespace, giữ nguyên bảng Markdown, loại dòng chạy đầu trang
- [x] T070 [US2] Tạo `scripts/book-kb/src/chunk.py` — chunk 1000 token / overlap 150, cắt ở ranh giới đoạn, bảng là khối nguyên vẹn; gắn metadata `book_page_from/to`, `pdf_page_from/to`, `chapter_number`
- [x] T071 [US2] Tạo `scripts/book-kb/src/review.py` — parse `review/qa.md`, `review/numbers.md`, `README.md` → danh sách `page_flag` (kind, source_file, note) theo số trang PDF
- [x] T072 [US2] Tạo `scripts/book-kb/src/embed.py` — gọi Gemini `gemini-embedding-001` (768 chiều, `RETRIEVAL_DOCUMENT`), L2-normalize, cache theo SHA-256 nội dung, retry + backoff
- [x] T073 [US2] Tạo `scripts/book-kb/src/loader.py` — upsert idempotent vào `kb.corpus`/`kb.chapter`/`kb.chunk`/`kb.page_flag`; đặt `is_active = true` cho version mới, chuyển version cũ thành false; giữ tối đa 2 version
- [x] T074 [US2] Tạo `scripts/book-kb/src/cli.py` — lệnh `build` (chunk + embed → `artifacts/book-kb/chunks.jsonl`), `load`, `verify` (đối chiếu số chương/số chunk/page range), `eval`
- [x] T075 [US2] Tạo `scripts/book-kb/src/assemble.py` — sinh `kb.page_flag` và tính cờ `verified`/`needs_review` cho chunk theo quy tắc ưu tiên
- [x] T076 [US2] Viết `docs/development/rag-ingestion.md` — quy ước ingest: marker trang là nguồn chuẩn, các file `review/*.md` khoá theo **số trang PDF**, quy tắc ghi đè cờ, cách rebuild, cách bật `RAG__REQUIRE_VERIFIED=true`
- [x] T077 [US2] Chạy `make kb-build` + `make kb-load` + `make kb-verify` trên corpus thật; kết quả đo được: **13 chương, 188 chunk, 67 page_flag, 130 chunk `verified`, 58 chunk `needs_review` (30,9% theo chunk / 40 trang ≈ 14,7% theo trang)**, `ok = true`, nạp lại lần 2 vẫn 188 dòng — ghi vào `specs/duck-001-recommendation-rag/checklists/requirements.md` phần ghi chú

**Checkpoint**: Kho tri thức có đủ 13 chương; nạp lại idempotent; mọi chunk truy được về trang sách.

---

## Phase 5: User Story 3 - Hỏi tri thức trong sách (Priority: P3)

**Goal**: Trả lời câu hỏi chỉ dựa trên sách, kèm trích dẫn chương + trang + trích đoạn; abstain khi corpus không đủ căn cứ.

**Independent Test**: Hỏi một câu có lời giải đích trong sách → `abstained = false`, citation trỏ đúng chương/trang, `quote` xuất hiện nguyên văn trong chunk nguồn. Hỏi câu ngoài sách → `abstained = true`.

### Tests cho User Story 3

- [x] T078 [P] [US3] Unit test `CitationResolver` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Ask/CitationResolverTests.cs` — quote khớp chunk → giữ; quote không khớp → loại; mất hết citation → `abstained = true`; quote có/không dấu đều khớp
- [x] T079 [P] [US3] Unit test `PromptBuilder` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Ask/PromptBuilderTests.cs` — assert có 3 tầng ràng buộc (grounding + abstain, unverified guard, corpus-as-data)
- [x] T080 [US3] Unit test `AskLongevityQuestionUseCase` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Ask/AskLongevityQuestionUseCaseTests.cs` — khi không có chunk nào khớp → `abstained = true` + `abstain_reason`; khi provider lỗi → trả kết quả rỗng + cảnh báo, **không** trả nội dung bịa
- [x] T081 [US3] Integration test trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Ask/AskLongevityQuestionGrpcTests.cs` — nạp corpus thật (subset chương), hỏi câu có lời giải đích, assert `book_page` trong ±3 trang kỳ vọng và `quote` khớp nội dung chunk

### Implementation cho User Story 3

- [x] T082 [US3] Tạo `src/Services/Recommendation/Recommendation.Application/Features/Ask/AskLongevityQuestionUseCase.cs` — truy xuất → dựng prompt 3 tầng → gọi `IChatModel` → `CitationResolver` → `AskLongevityQuestionResponse`; luôn kèm `disclaimer`
- [x] T083 [US3] Tạo `src/Services/Recommendation/Recommendation.Api/Mapping/AskLongevityQuestionMapper.cs` — map nội bộ ↔ message `AskLongevityQuestionRequest/Response`
- [x] T084 [US3] Bổ sung `AskLongevityQuestion` vào `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs` — map lỗi sang `Status` đúng; deadline 5s
- [x] T085 [US3] Tạo `src/Services/Recommendation/Recommendation.Application/Features/Ask/SafetyGuard.cs` — câu hỏi về thuốc/liều/chẩn đoán/thay thế điều trị → từ chối và hướng về bác sĩ; nội dung bệnh nền vẫn trả ở mức lối sống kèm disclaimer + cờ chưa đối chiếu

**Checkpoint**: User Story 3 chạy độc lập — hỏi sách có trích dẫn, câu ngoài sách abstain.

---

## Phase 6: User Story 4 - Tra cứu các đoạn trong sách (Priority: P4)

**Goal**: Tìm các đoạn trong sách theo truy vấn tự do, có lọc theo chương, trả kèm điểm liên quan.

**Independent Test**: Tra cứu "nhịn ăn gián đoạn" → nhận danh sách đoạn có nội dung, chương, trang sách, điểm liên quan giảm dần; lọc theo chương hoạt động.

### Tests cho User Story 4

- [x] T086 [P] [US4] Unit test `SearchLongevityKnowledgeUseCase` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Search/SearchLongevityKnowledgeUseCaseTests.cs` — `top_k` mặc định 8, tối đa 20; lọc theo chương; kết quả sắp xếp giảm dần theo điểm
- [x] T087 [US4] Integration test trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/Search/SearchLongevityKnowledgeGrpcTests.cs` — truy vấn tiếng Việt có dấu ("nhịn ăn gián đoạn") trả về đoạn có `book_page` hợp lý; truy vấn lọc chương chỉ trả đoạn thuộc chương đó

### Implementation cho User Story 4

- [x] T088 [US4] Tạo `src/Services/Recommendation/Recommendation.Application/Features/Search/SearchLongevityKnowledgeUseCase.cs` — dùng `IKnowledgeStore.SearchAsync` với `chapter_filter` + `top_k`, trả `KnowledgeSnippet` kèm `score` và `verified`
- [x] T089 [US4] Bổ sung `SearchLongevityKnowledge` vào `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs`
- [x] T090 [US4] Tạo `src/Services/Recommendation/Recommendation.Api/Mapping/SearchKnowledgeMapper.cs`

**Checkpoint**: User Story 4 chạy độc lập.

---

## Phase 7: User Story 5 - Biết mình đang thiếu vi chất gì trong tuần (Priority: P5)

**Goal**: Tính lượng thiếu vi chất theo nhu cầu tham chiếu **theo nhóm tuổi và giới**, phân loại mức độ 25%/50%, gợi ý thực phẩm có căn cứ trong sách.

**Independent Test**: Gửi 7 ngày ăn uống thiếu chất xơ + hồ sơ → nhận `gaps[]` có chất xơ với `target` theo nhóm tuổi/giới và `severity` đúng ngưỡng; gọi 2 lần cùng đầu vào → kết quả giống hệt.

### Tests cho User Story 5

- [ ] T091 [P] [US5] Unit test `MicronutrientRda` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/NutrientGaps/MicronutrientRdaTests.cs` — nhu cầu tham chiếu theo nhóm tuổi và giới (người lớn nam ≠ người lớn nữ); giá trị thiếu tính đúng
- [ ] T092 [P] [US5] Unit test `NutrientGapCalculator` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/NutrientGaps/NutrientGapCalculatorTests.cs` — ngưỡng severity: <25% nhẹ, 25–50% trung bình, >50% nặng; biên đúng 25% và 50%
- [ ] T093 [P] [US5] Unit test `AnalyzeNutrientGapsUseCase` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/NutrientGaps/AnalyzeNutrientGapsUseCaseTests.cs` — cùng đầu vào cho cùng kết quả; thực phẩm gợi ý không chứa thành phần dị ứng; phần giải thích có citations
- [ ] T094 [US5] Integration test trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/NutrientGaps/AnalyzeNutrientGapsGrpcTests.cs` — gọi server thật, assert `gaps[]` chứa chất xơ với `severity` và `target` đúng

### Implementation cho User Story 5

- [ ] T095 [P] [US5] Tạo `src/Services/Recommendation/Recommendation.Domain/Constants/MicronutrientRda.cs` — bảng nhu cầu tham chiếu theo nhóm tuổi và giới cho ~18 vi chất (A, C, D, E, K, B1–B12, canxi, sắt, magiê, kẽm, iot, selen, omega-3, chất xơ)
- [ ] T096 [US5] Tạo `src/Services/Recommendation/Recommendation.Application/Features/NutrientGaps/NutrientGapCalculator.cs` — tính `average_intake` (7 ngày), `deficit`, `severity` theo ngưỡng 25%/50%
- [ ] T097 [US5] Tạo `src/Services/Recommendation/Recommendation.Application/Features/NutrientGaps/AnalyzeNutrientGapsUseCase.cs` — nhận hồ sơ + 7 ngày + mục tiêu → `gaps[]` (deterministic) + phần diễn giải/thực phẩm gợi ý có căn cứ (RAG) + `citations` + `disclaimer`
- [ ] T098 [US5] Tạo `src/Services/Recommendation/Recommendation.Api/Mapping/NutrientGapMapper.cs`
- [ ] T099 [US5] Bổ sung `AnalyzeNutrientGaps` vào `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs`

**Checkpoint**: User Story 5 chạy độc lập, kết quả deterministic.

---

## Phase 8: User Story 6 - Biết điểm thói quen trường thọ (Priority: P6)

**Goal**: Chấm điểm 0–100 theo rubric cố định 35/30/20/15, trả `score_breakdown` và gợi ý có căn cứ.

**Independent Test**: Gửi dữ liệu thói quen → nhận điểm + 4 nhánh có điểm/trọng số/lý do; gọi 2 lần cùng đầu vào → điểm giống hệt.

### Tests cho User Story 6

- [ ] T100 [P] [US6] Unit test `LongevityRubric` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/LongevityHabits/LongevityRubricTests.cs` — trọng số fasting 35 / sleep 30 / hydration 20 / nutrition 15; điểm tối đa khi nhịn ăn 16h, ngủ 7h, nước 7/7 ngày, dinh dưỡng 90%; cùng đầu vào → cùng điểm
- [ ] T101 [P] [US6] Unit test `EvaluateLongevityHabitsUseCase` trong `src/Services/Recommendation/tests/Recommendation.UnitTests/Features/LongevityHabits/EvaluateLongevityHabitsUseCaseTests.cs` — `score_breakdown` có đủ 4 nhánh; `actionable_insights` có citations khi nội dung có trong sách
- [ ] T102 [US6] Integration test trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Features/LongevityHabits/EvaluateLongevityHabitsGrpcTests.cs` — gọi server thật, assert điểm trong 0–100 và tổng trọng số = 100

### Implementation cho User Story 6

- [ ] T103 [P] [US6] Tạo `src/Services/Recommendation/Recommendation.Domain/Constants/LongevityRubric.cs` — trọng số + ngưỡng điểm tối đa từng nhánh (fasting 16h, sleep 7h, hydration 7/7 ngày, nutrition 90%)
- [ ] T104 [US6] Tạo `src/Services/Recommendation/Recommendation.Application/Features/LongevityHabits/LongevityRubricCalculator.cs` — chấm từng nhánh 0–100 theo ngưỡng, tính điểm tổng theo trọng số, trả `ScoreComponent[]`
- [ ] T105 [US6] Tạo `src/Services/Recommendation/Recommendation.Application/Features/LongevityHabits/EvaluateLongevityHabitsUseCase.cs` — `score_breakdown` + `actionable_insights` (có citations khi có trong sách) + `disclaimer`
- [ ] T106 [US6] Tạo `src/Services/Recommendation/Recommendation.Api/Mapping/LongevityHabitMapper.cs`
- [ ] T107 [US6] Bổ sung `EvaluateLongevityHabits` vào `src/Services/Recommendation/Recommendation.Api/Endpoints/RecommendationGrpcService.cs`

**Checkpoint**: User Story 6 chạy độc lập, điểm deterministic.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Việc cắt ngang nhiều story — eval, tài liệu, gate đóng.

- [ ] T108 [P] Tạo bộ câu hỏi vàng `scripts/book-kb/eval/golden-set.json` — ~50 câu hỏi theo 13 chương, mỗi câu có **đáp án kỳ vọng** + **trang sách kỳ vọng** + cờ "câu ngoài sách"
- [ ] T109 Tạo `scripts/book-kb/src/eval.py` — gọi service thật qua gRPC, kiểm tra: có citation, `book_page` trong ±3 trang kỳ vọng, `quote` khớp chunk nguồn, câu ngoài sách → `abstained = true`; xuất báo cáo groundedness ra `artifacts/book-kb/eval-report.json`
- [ ] T110 Chạy `make kb-eval` và lưu kết quả (tỉ lệ groundedness, số câu abstain đúng) vào `specs/duck-001-recommendation-rag/checklists/requirements.md`; **CI phải skip với log rõ lý do khi thiếu `GOOGLE_AI__API_KEY`** (không được báo pass giả)
- [ ] T111 Tạo test tích hợp đo budget độ trễ trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Performance/LatencyBudgetTests.cs` — với corpus thật đã nạp: **assert** p95 của bước truy xuất (vector + keyword + RRF) `< 150ms` (độc lập provider nên ổn định); đo p95 end-to-end của lời gọi gRPC và ghi vào `artifacts/book-kb/latency-report.json` để đối chiếu SC-005 (3s) — **không** assert cứng end-to-end vì phụ thuộc provider; skip với log rõ lý do khi thiếu `GOOGLE_AI__API_KEY` (FR-021, SC-005)
- [ ] T112 Tạo test tích hợp khẳng định kho tri thức không chứa dữ liệu người dùng trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Persistence/NoUserDataInKnowledgeStoreTests.cs` — kiểm tra mọi cột của mọi bảng `kb.*` không mang dữ liệu hồ sơ/nhật ký (SC-009)
- [ ] T113 Tạo test tích hợp xác nhận `content_tsv` luôn khớp `content` sau khi update trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Persistence/TsVectorTriggerTests.cs` — trigger hoạt động, index GIN được dùng (`EXPLAIN` → `Bitmap Index Scan`)
- [ ] T114 Tạo test tích hợp xác nhận truy xuất lai **không** trả cột `embedding` trong `src/Services/Recommendation/tests/Recommendation.IntegrationTests/Persistence/ProjectionExcludesEmbeddingTests.cs` (D-14)
- [ ] T115 [P] Cập nhật `docs/api/api-spec.md` §5 và `docs/requirements/prd.md` §2.4/§4 cho khớp hợp đồng và hành vi thực tế đã triển khai
- [ ] T116 [P] Cập nhật `docs/overview/changelog.md` (mục Added/Changed cho Recommendation RAG) và `docs/system-design/architecture.md` nếu phát sinh khác biệt so với Phase 0
- [ ] T117 [P] Cập nhật `meta.json` của feature (`services: ["recommendation"]`, `status`) và chạy `pwsh scripts/speckit/team-feature.ps1 check`
- [ ] T118 Chạy toàn bộ kịch bản trong `specs/duck-001-recommendation-rag/quickstart.md` và đánh dấu kết quả từng mục

### Frontend handoff decision *(bắt buộc)*

- [ ] T119 **Không đổi client surface**: xác nhận feature này **không** thêm/sửa/xoá HTTP endpoint, event payload hay DTO client-facing — Recommendation là service nội bộ gRPC-only, mọi thay đổi nằm trong `contracts/recommendation/v1/recommendation.proto` và `kb.*`; **không** tạo `frontend-guide.md`. Ghi kết luận này vào `tasks.md` (mục này) và vào `specs/duck-001-recommendation-rag/deferred.md` (phần tích hợp Diet → feature `duck/002`, nơi DTO client-facing của `GET /api/diet/meals/ai-recommendations` mới thực sự đổi)

### Phát hiện ngoài phạm vi *(bắt buộc khi có)*

- [ ] T120 Chuyển các finding thật sự ngoài phạm vi vào `specs/duck-001-recommendation-rag/deferred.md` theo `.specify/templates/deferred-template.md` — tối thiểu: (a) tích hợp phía service dinh dưỡng → feature `duck/002/diet-recommendation-integration`; (b) fuzzy search bằng `pg_trgm`; (c) rerank bằng LLM; (d) hỗ trợ nhiều corpus/multi-book; (e) `Pgvector.EntityFrameworkCore` chưa có bản release khai báo EF Core 10 — theo dõi upstream

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: không phụ thuộc — bắt đầu ngay
- **Foundational (Phase 2)**: phụ thuộc Phase 1 — **CHẶN mọi user story**
- **User Stories (Phase 3–8)**: phụ thuộc Phase 2; có thể làm tuần tự theo thứ tự ưu tiên
- **Polish (Phase 9)**: phụ thuộc các story còn lại đã xong

### User Story Dependencies

- **US1 (P1)**: bắt đầu sau Phase 2 — không phụ thuộc story khác; test được với kho tri thức rỗng (món + rationale, chưa có citation)
- **US2 (P2)**: bắt đầu sau Phase 2 — **điều kiện tiên quyết cho US3/US4** (cần chunk thật để assert citation)
- **US3 (P3)**: cần US2 đã nạp corpus để kiểm chứng citation; về mặt code chỉ cần Phase 2
- **US4 (P4)**: cần US2 để có kết quả truy xuất thật
- **US5 (P5)**: bắt đầu sau Phase 2 — dùng lại RAG của Phase 2, độc lập US1
- **US6 (P6)**: bắt đầu sau Phase 2 — dùng lại RAG của Phase 2, độc lập US1

### Trong mỗi User Story

- Test phải viết **trước** và **thấy đỏ** trước khi thấy xanh (Constitution V)
- Model/constant → abstraction → adapter → use case → gRPC endpoint
- Story xong mới chuyển sang ưu tiên kế tiếp

### Parallel Opportunities

- Toàn bộ task `[P]` trong Phase 1 (T002–T009, T010–T012, T014–T017) chạy song song
- Toàn bộ task `[P]` trong Phase 2 (T021–T023, T025–T026, T028, T033–T036) chạy song song
- Test task `[P]` của mỗi story chạy song song trước khi viết implementation
- Các story sau Phase 2 có thể làm song song nếu đủ người (mỗi story một người, khác file)

---

## Parallel Example — User Story 1

```bash
# Chạy song song các test task của US1 trước khi viết implementation:
Task: "T047 [P] [US1] Unit test AllergenGuard trong src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/AllergenGuardTests.cs"
Task: "T048 [P] [US1] Unit test CalorieCapRule trong src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/CalorieCapRuleTests.cs"
Task: "T049 [P] [US1] Unit test DietaryPreferenceFilter trong src/Services/Recommendation/tests/Recommendation.UnitTests/Features/Meals/DietaryPreferenceFilterTests.cs"

# Sau đó mới viết implementation (phụ thuộc test đã đỏ):
Task: "T054 [US1] Tạo src/Services/Recommendation/Recommendation.Application/Features/Meals/AllergenGuard.cs"
Task: "T055 [US1] Tạo src/Services/Recommendation/Recommendation.Application/Features/Meals/CalorieCapRule.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Xong Phase 1 (Setup)
2. Xong Phase 2 (Foundational — CRITICAL)
3. Xong Phase 3 (User Story 1)
4. **DỪNG và KIỂM CHỨNG**: gọi gRPC thật, assert lọc dị ứng + ≤5 món + disclaimer
5. Demo được

### Incremental Delivery

1. Setup + Foundational → nền móng sẵn sàng
2. US1 → kiểm chứng độc lập → demo (MVP)
3. US2 (nạp corpus) → kiểm chứng độc lập → demo
4. US3 + US4 → kiểm chứng → demo (RAG dùng được thật)
5. US5 + US6 → kiểm chứng → demo
6. Mỗi story thêm giá trị mà không phá story trước

---

## Notes

- `[P]` = khác file, không phụ thuộc task chưa xong
- Nhãn `[Story]` map task → user story trong `spec.md` (US1..US6)
- Mỗi user story phải hoàn thành và kiểm thử **độc lập**
- Kiểm tra test thấy đỏ trước khi triển khai (Constitution V)
- Khi chạy `$speckit-orchestrate`, commit **một lần mỗi phase** sau khi qua gate; worker chạy skill này **độc lập, không tự commit**
- Dừng ở mỗi checkpoint để kiểm chứng story độc lập
- Tránh: task mơ hồ, xung đột cùng file giữa các story, phụ thuộc chéo làm mất tính độc lập
