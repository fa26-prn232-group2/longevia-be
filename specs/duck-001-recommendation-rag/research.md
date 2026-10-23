# Nghiên cứu & Quyết định Kỹ thuật — Recommendation RAG

**Feature**: `duck/001/recommendation-rag` | **Date**: 2026-10-22 | **Plan**: [plan.md](./plan.md)

Mỗi quyết định ghi: **Decision** → **Rationale** → **Alternatives considered**. Không còn `NEEDS CLARIFICATION`.

---

## D-01 — Knowledge store: PostgreSQL + pgvector trong `recommendation_db`

**Decision**: `Recommendation` sở hữu `recommendation_db` (schema `kb`) chứa chunks + embeddings của corpus tri thức. Image `pgvector/pgvector:pg17`.

**Rationale**: RAG bắt buộc cần nơi lưu vector. Sơ đồ C4 level 2 đã vẽ `Recommendation Database` ngang hàng với các DB khác, nên đi ngược sơ đồ là tự tạo mâu thuẫn mới. Postgres đã có sẵn trong hạ tầng → thêm một DB không thêm công nghệ mới, chỉ thêm một schema. `kb` **không** chứa dữ liệu người dùng nên ranh giới dữ liệu vẫn nguyên vẹn.

**Alternatives considered**:
- *Index artifact nhúng trong image, search in-process* — loại: đã chọn DB để tái index không cần rebuild image; C4 không thể hiện.
- *Gemini File Search (managed RAG)* — loại: không kiểm soát được citation/prompt, khó debug, corpus nằm ngoài hệ thống.
- *Tách service `Knowledge` riêng* — loại: thêm 1 service + 1 contract + 1 deploy (~+2–3 ngày), C4 không thể hiện.

---

## D-02 — Corpus ingest là pipeline OFFLINE

**Decision**: `scripts/book-kb/` (Python, chạy trong Docker) đọc Markdown → clean → chunk → embed → nạp `kb.*`. Không có đường ghi vào `recommendation_db` từ runtime service.

**Rationale**: Runtime service phải stateless về phía dữ liệu người dùng và chỉ đọc tri thức. Tách ingest ra ngoài giúp: (1) không cần quyền ghi trong runtime; (2) nạp lại không cần deploy; (3) giống cách `scripts/book-ocr` đã tồn tại cho pipeline OCR; (4) chi phí embedding được gom một lần theo corpus thay vì rải rác.

**Alternatives considered**: *Nạp corpus qua HTTP endpoint nội bộ của service* — loại: thêm bề mặt ghi, vi phạm tinh thần "runtime chỉ đọc", khó kiểm soát quyền.

---

## D-03 — Chunking: 1000 token / overlap 150, cắt ở ranh giới đoạn, không cắt bảng

**Decision**: Chunk theo chương, target 1000 token, overlap 150 token, cắt tại ranh giới đoạn (`\n\n`), bảng Markdown là một khối nguyên vẹn. Metadata lấy từ `.manifest.json` + marker `<!-- sách N · PDF M -->`.

**Rationale**: 1000 token nằm gọn trong giới hạn 2048 token của `gemini-embedding-001`. Chunk theo chương + cắt ở ranh giới đoạn giữ nguyên mạch ý. Cắt bảng sẽ sinh trích dẫn không khớp nội dung (bảng sách là nơi chứa số liệu dinh dưỡng — đúng phần cần giữ nguyên vẹn). Overlap 150 giữ ngữ cảnh khi đoạn bị cắt giữa chừng. Marker trang là nguồn chuẩn (đã được kiểm chứng: `review/page-numbers.md` ghi 0 cụm sai lệch thật).

**Alternatives considered**: *Chunk theo trang PDF* — loại: đoạn trang đôi khi quá ngắn (< 100 token) gây vector nhiễu; *chunk 512 token* — loại: nhiều chunk hơn, trích dẫn bị cắt vụn.

---

## D-04 — Embedding: `gemini-embedding-001`, 768 chiều, L2-normalize thủ công

**Decision**: Model `gemini-embedding-001`, `OutputDimensionality = 768`, `TaskType = "RETRIEVAL_DOCUMENT"`. Vector **phải được L2-normalize thủ công** trước khi lưu, vì model này **không** tự chuẩn hoá lại khi cắt chiều.

**Rationale**: `gemini-embedding-001` là bản text-only ổn định, đủ cho corpus chữ. 768 chiều giảm 4× dung lượng so với 3072 và vẫn giữ chất lượng truy xuất tốt (768 là giá trị được khuyến nghị). Việc **phải tự normalize** là điểm then chốt: nếu bỏ qua, khoảng cách cosine trên vector chưa chuẩn hoá sẽ cho kết quả sai lệch. `gemini-embedding-2` có auto-normalize nhưng là đa phương thức (không cần cho corpus chữ).

**Alternatives considered**: *`gemini-embedding-2`* — loại: đa phương thức, không cần cho corpus text, mới hơn nên rủi ro hơn; *3072 chiều* — loại: 4× dung lượng, hiệu năng không khác đáng kể ở quy mô 1.300 chunk.

---

## D-05 — Retrieval hybrid: vector cosine (HNSW) + keyword tsvector → RRF → top-k 8

**Decision**: Ứng viên vector top-50 (cosine, index HNSW) ∪ ứng viên keyword top-50 (tsvector) → merge bằng **RRF** (`1/(60+rank)`) → top-k **8**.

**Rationale**: Tiếng Việt có nhiều từ đồng nghĩa ("nhịn ăn" / "nhịn ăn gián đoạn" / "fasting"), nên vector-only bỏ sót; keyword-only bỏ sót câu hỏi diễn đạt khác. RRF là cách merge không cần hiệu chỉnh trọng số giữa hai hệ điểm khác thang. Corpus chỉ ~1.300 chunk nên cả hai nhánh đều rẻ.

**Alternatives considered**: *Chỉ vector* — loại: bỏ sót truy vấn từ khoá tiếng Việt; *chỉ keyword* — loại: bỏ sót câu hỏi diễn đạt khác; *BM25 thuần trong C#* — loại: phải quét toàn bộ corpus mỗi lượt; *rerank bằng LLM* — defer: thêm 1 lời gọi provider, không cần ở quy mô này.

---

## D-06 — Keyword search tiếng Việt: cột `tsvector` duy trì bằng TRIGGER, truy vấn bằng `plainto_tsquery('simple', unaccent(@q))`

**Decision**: Cột `kb.chunk.content_tsv` kiểu `tsvector`, duy trì bằng trigger `BEFORE INSERT OR UPDATE` gọi `to_tsvector('simple', unaccent(NEW.content))`; index `GIN(content_tsv)`. Truy vấn: `content_tsv @@ plainto_tsquery('simple', unaccent(@q))`.

**Rationale** (đã kiểm chứng thực nghiệm trên PG 17.10 + pgvector):
- `unaccent` **có sẵn** trong `postgres:17` và `pgvector/pgvector:pg17` (version 1.1) và **bỏ đúng dấu tiếng Việt** kể cả `đ → d`, `ơ → o`, `ư → u`.
- `unaccent` là **STABLE**, không phải IMMUTABLE → **không** dùng được trong `GENERATED ALWAYS AS` column và **không** dùng được trong expression index. Trigger là cách đúng: không cần wrapper giả volatility, cột là cột thật nên EF map bình thường (`NpgsqlTsVector` là type built-in của Npgsql).
- Vế phải của phép `@@` là `tsquery` → bắt buộc dùng `plainto_tsquery` (`to_tsquery` bắt buộc toán tử và sẽ lỗi cú pháp với truy vấn tự do).
- `unaccent` áp vào **tham số** (vế phải) nên planner vẫn dùng được GIN index — đã verify bằng `EXPLAIN` trên 200.000 dòng: `Bitmap Index Scan`.
- Bẫy đã loại: dùng `unaccent()` ở vế query-side của một expression index đã dùng wrapper `IMMUTABLE` → **âm thầm mất index**, chạy seq scan toàn bảng.

**Alternatives considered**:
- *Tính `tsvector` ngay trong lúc insert từ app* — loại: dễ lệch giữa index và query, và phải viết hàm bỏ dấu tiếng Việt riêng trong .NET (`string.Normalize(FormD)` **không** đổi `đ` → `d` vì `đ` là ký tự precomposed, không phải combining mark).
- *Expression index với wrapper `IMMUTABLE` giả* — loại: "nói dối" volatility, dễ mất index như trên.
- *`pg_trgm` (gin_trgm_ops) cho fuzzy* — defer: trigram rất nhạy với dấu (`similarity('Tếng Việt','tieng') = 0.14` trước khi normalize) và index trên cột nội dung dài rất nặng; chỉ đáng dùng trên cột ngắn. Ở MVP không cần.

---

## D-07 — Chiến lược cờ chất lượng: giữ chunk, gắn cờ, cấm khẳng định số liệu

**Decision**: Chunk từ trang chưa đối chiếu **vẫn được truy xuất**. Cờ `needs_review = true` ⇒ prompt bắt buộc ghi "(chưa đối chiếu bản in)" khi dùng con số từ đoạn đó và không được khẳng định như sự thật y tế. Cờ bật/tắt qua `RAG__REQUIRE_VERIFIED` (mặc định `false`).

**Rationale** (đo trên corpus thật): 47/273 trang có cờ nghi vấn (41 `missing_numbers`, 4 `low_word_recall`, 2 `longer_than_ocr`, 1 `shorter_than_ocr`); sau khi trừ 2 trang đã được đối chiếu thủ công (266, 269) còn **45 trang ≈ 16,5%**. Nhưng cờ nghĩa là *"chưa đối chiếu"*, không phải *"chắc là sai"* — theo `review/qa.md`, phần lớn `missing_numbers` là Tesseract đọc sai, và 12 trang có bảng đã so từng ô số khớp 100%. Các trang bị cờ tập trung **đúng ở nơi có số liệu dinh dưỡng** (chương 4: 7/37, chương 11: 6/17, chương 12: 14/51) — loại chúng khỏi truy xuất là mất đúng phần người dùng thực sự hỏi. Ngược lại, nếu chỉ nhận `verified = true` thì corpus còn **~19 trang ≈ 7%** (4 trang chép tay + 3 trang vá + 12 trang bảng) → RAG gần như vô dụng.

**Alternatives considered**: *loại khỏi kết quả truy xuất* — loại (xem lý do trên); *chỉ loại số liệu, giữ văn bản* — loại: làm méo nội dung gốc, có thể sinh câu văn vô nghĩa.

---

## D-08 — Citation cho nguồn chưa kiểm chứng: vẫn trả, kèm cờ

**Decision**: Luôn trả citation khi có nguồn; kèm `verified` (bool) và `source_file`. Client hiển thị badge "chưa đối chiếu bản in". `abstained` chỉ dùng khi **không tìm được đoạn nào** khớp câu hỏi, hoặc khi `quote` không verify khớp chunk.

**Rationale**: BR-06 yêu cầu "phải truy ngược được về chương + trang" **và** "phải đánh dấu là chưa kiểm chứng" — tức BR-06 đã quy định hành vi này. Bỏ citation sẽ vi phạm chính BR-06 (83,5% corpus chưa kiểm chứng ⇒ phần lớn câu trả lời mất nguồn) và làm mất khả năng để người dùng **tự mở sách tra trang** — biện pháp an toàn mạnh nhất còn lại.

**Alternatives considered**: *không trả citation cho nguồn chưa kiểm chứng* — loại: vi phạm BR-06, mất khả năng tự kiểm chứng.

---

## D-09 — Mô hình cờ theo mức ưu tiên

**Decision**: `kb.page_flag` lưu cờ theo `kind`; `manual_verified` / `table_verified` **ghi đè** cờ nghi vấn của cùng trang. `chunk.verified = true` khi mọi trang của chunk đã đối chiếu **hoặc** không có cờ nghi vấn nào; `chunk.needs_review = true` khi có ít nhất một trang mang cờ nghi vấn chưa bị ghi đè.

**Rationale**: 6 trang nằm trong cả hai danh sách (đặc biệt 266 và 269 vừa có `missing_numbers` vừa nằm trong 12 trang bảng đã so từng ô số) → boolean đơn giản sẽ đánh dấu sai các trang đã kiểm chứng. Kiểu `kind` + quy tắc ưu tiên giữ được cả hai thông tin: trang nào đã kiểm chứng, trang nào còn nghi vấn, và vì sao.

**Alternatives considered**: *boolean `verified` trên `page_flag`* — loại: không biểu đạt được quan hệ "ghi đè".

---

## D-10 — Prompt 3 tầng: grounding → unverified guard → corpus-as-data

**Decision**: System prompt gồm 3 lớp ràng buộc: (1) chỉ dùng CONTEXT, không đủ thì abstain; (2) số liệu từ đoạn `verified = false` phải ghi chú "(chưa đối chiếu bản in)" và không được khẳng định y tế; (3) nội dung sách là **dữ liệu**, không phải chỉ dẫn.

**Rationale**: Lớp (1) là yêu cầu BR-06; lớp (2) là hiện thực hoá D-07; lớp (3) là biện pháp chống prompt injection — nội dung trong sách luôn được đưa vào prompt như dữ liệu để câu hỏi của người dùng không thể thay đổi hành vi hệ thống.

**Alternatives considered**: *chỉ 1 lớp* — loại: không đáp ứng được D-07 và FR-030.

---

## D-11 — Provider vector cho EF Core 10: `Pgvector.EntityFrameworkCore` 0.3.0

**Decision**: Thêm `Pgvector` 0.3.2 + `Pgvector.EntityFrameworkCore` 0.3.0 (ghim phiên bản). CLR type `Pgvector.Vector`, khai báo cột `[Column(TypeName = "vector(768)")]`. Bật mapping bằng `optionsBuilder.UseNpgsql(conn, o => o.UseVector())`; nếu tự dựng `NpgsqlDataSource` thì phải gọi `UseVector()` **cả** trên `NpgsqlDataSourceBuilder`. Extension tạo bằng `modelBuilder.HasPostgresExtension("vector")` (EF sinh `CREATE EXTENSION` trong migration — đúng yêu cầu Code First).

**Rationale** (đã kiểm chứng thực nghiệm trên đúng stack của dự án: .NET 10 + EF Core 10.0.12 + Npgsql.EFCore.PostgreSQL 10.0.3 + Npgsql 10.0.3 + `pgvector/pgvector:pg17`):
- **Npgsql 10 KHÔNG có sẵn kiểu `vector`** — reflection trên `Npgsql.dll` 10.0.3 không có type nào cho `vector`, `NpgsqlDbType` không có member `Vector`, release notes 10.0 chỉ thêm extension `cube`.
- `Pgvector.EntityFrameworkCore` 0.3.0 chạy được trên EF Core 10: restore sạch (lib `net8.0` → dùng được trên `net10.0`), `EnsureCreated` tạo đúng cột `vector(N)` + index HNSW, `SaveChanges` insert `Vector` không lỗi, `CosineDistance` dịch SQL đúng.
- Lỗi "Writing values of 'Pgvector.Vector' is not supported for parameters having no NpgsqlDbType" (issue #51) **đã được sửa** trong 0.3.0 bằng cơ chế plugin, yêu cầu EF Core ≥ 9.
- Nếu dùng property kiểu `float[]` thì **bắt buộc** có value converter, nếu không EF lỗi model validation.
- Index HNSW khai báo bằng annotation EF: `.HasMethod("hnsw").HasOperators("vector_cosine_ops").HasStorageParameter("m", 16).HasStorageParameter("ef_construction", 64)` — sinh đúng DDL mong muốn.

**Rủi ro ghi nhận**: 0.3.0 mới chỉ có asset `net8.0` (chạy được trên net10.0) và chưa có bản release chính thức khai báo EF Core 10 → **ghim phiên bản**, theo dõi upstream (PR #63 đang mở để bump lên `net10.0`). EF Core 10 tự nạp cột vector khi query entity → các truy vấn không cần embedding phải dùng projection.

**Alternatives considered**:
- *Raw SQL `$1::vector` + đọc `embedding::text`* — loại (giữ làm đường fallback): mất index HNSW khai báo bằng annotation, mất truy vấn khoảng cách có type-safety, phải tự quản lý text-format của vector. Đã kiểm chứng đường này **hoạt động** (`INSERT ... VALUES ($1::vector)` với tham số string; đọc `embedding::text` / `embedding::real[]`) nên vẫn dùng được nếu provider hỏng.
- *Tự viết type handler* — loại: tự bảo trì, không cần thiết khi package đã ổn định.

---

## D-12 — SDK Google AI cho .NET: `Google.GenAI` 1.26.1

**Decision**: Thêm `Google.GenAI` 1.26.1. Đăng ký `Client` singleton thủ công (`new Client(apiKey:)` — SDK **không** có DI extension), `IDisposable` → dispose khi shutdown. Embedding: `client.Models.EmbedContentAsync(model, List<Content>, config)` với `EmbedContentConfig { OutputDimensionality = 768, TaskType = "RETRIEVAL_DOCUMENT" }`; đọc `EmbedContentResponse.Embeddings[i].Values` (kiểu `List<double>?`). Sinh câu trả lời: `client.Models.GenerateContentAsync(model, contents, config)` với `GenerateContentConfig { ResponseMimeType = "application/json", ResponseJsonSchema = JsonNode.Parse(schema), SystemInstruction = new Content { Parts = [new Part { Text = ... }] } }`.

**Rationale** (đã đọc source + README upstream):
- `Google.Cloud.AIPlatform.V1` **deprecated** từ 05/2026; `Google.GenAI` là SDK .NET được khuyến nghị, phát hành gần như hằng tuần, TFM `net8.0`/`netstandard2.0` → chạy được trên `net10.0`.
- `ResponseJsonSchema` (JsonNode) được khuyến nghị thay cho `ResponseSchema` (`Dictionary<string, Schema>`) vì .NET **không** bảo đảm thứ tự Dictionary mà Gemini JSON output nhạy cảm với thứ tự property.
- `SystemInstruction` kiểu `Content?`, không phải string.
- Batch embedding job (`Batches.CreateEmbeddingsAsync`) chỉ có ở Gemini Developer API và **giảm 50% chi phí** → dùng cho pipeline ingest offline.
- Cần `TaskType` đúng: `RETRIEVAL_DOCUMENT` khi nhúng chunk, `RETRIEVAL_QUERY` khi nhúng câu hỏi — **bắt buộc** dùng cặp này để truy xuất đạt chất lượng tốt.

**Alternatives considered**:
- *Dùng HTTP thô tới `generativelanguage.googleapis.com`* — loại: tự viết lại mapping, mất cập nhật SDK.
- *Vertex AI* — loại: cần ADC + project + location, SDK .NET đã deprecated.

---

## D-13 — Chuẩn hoá vector L2 thủ công khi lưu

**Decision**: Sau khi nhúng, chuẩn hoá L2 vector (norm = 1) trước khi ghi vào `kb.chunk.embedding`, dùng khoảng cách **cosine**.

**Rationale**: `gemini-embedding-001` **không** tự chuẩn hoá lại khi cắt chiều (`OutputDimensionality = 768`); `gemini-embedding-2` thì có. Với vector chưa chuẩn hoá, khoảng cách cosine tính bằng `<=>` của pgvector vẫn chạy nhưng phản ánh độ dài vector lẫn hướng → xếp hạng sai. Chuẩn hoá tay một lần lúc ingest là rẻ và loại bỏ hẳn lớp lỗi.

**Alternatives considered**: *dùng `vector_l2_ops` + chuẩn hoá vector truy vấn* — loại: phải chuẩn hoá cả vector câu hỏi ở mọi lượt gọi, tăng bề mặt lỗi.

---

## D-14 — Đường truy vấn lưu trữ: raw SQL cho RRF, projection cho query chunk

**Decision**: Truy vấn RRF (union 2 nhánh + `ROW_NUMBER` + merge) chạy bằng `FromSqlRaw`/`FromSql` với keyless entity `ChunkSearchResult` → không cố biểu đạt bằng LINQ. Các truy vấn đọc chunk không cần embedding phải **projection** (không select cột `embedding`), vì EF Core 10 mặc định nạp toàn bộ cột vector khi materialize entity.

**Rationale**: RRF cần `ROW_NUMBER() OVER (ORDER BY ...)` và `UNION ALL` — LINQ không diễn đạt được. EF Core 10 sẽ tự nạp cột `vector` vào entity khi query (đã ghi nhận ở issue #59/PR #64 của pgvector-dotnet: EF Core 11 sẽ **ngừng auto-load** cột vector) → với 3 KB/chunk × 50 dòng là 150 KB mạng thừa mỗi lượt, và projection loại bỏ rủi ro hiệu năng khi package nâng cấp.

**Alternatives considered**: *truy vấn bằng LINQ thuần* — loại: không biểu đạt được RRF; *nạp entity đầy đủ* — loại: tải vector thừa.

---

## Tóm tắt câu hỏi đã giải quyết

| Câu hỏi từ Technical Context | Kết quả |
| --- | --- |
| Npgsql 10 có sẵn kiểu `vector`? | Không → dùng `Pgvector.EntityFrameworkCore` 0.3.0 (D-11) |
| SDK Google AI cho .NET? | `Google.GenAI` 1.26.1 (D-12) |
| Làm sao bỏ dấu tiếng Việt trong full-text? | `unaccent` có sẵn, bỏ đúng `đ/ơ/ư`; phải dùng **trigger** vì `unaccent` là STABLE (D-06) |
| Vector có cần chuẩn hoá? | Có — `gemini-embedding-001` không tự chuẩn hoá (D-13) |
| `pg_trgm` có cần thêm? | Không ở MVP; defer (D-06) |
| Batch embedding có dùng được? | Có, chỉ ở Gemini Developer API, giảm 50% chi phí → dùng cho ingest (D-12) |
| Có giới hạn quota nào cần biết? | Free tier `gemini-embedding-001`: RPM 100 / TPM 30k / RPD 1000 (nguồn forum, chưa verify trên docs chính thức) → ingest phải có retry + backoff và cache theo hash |
