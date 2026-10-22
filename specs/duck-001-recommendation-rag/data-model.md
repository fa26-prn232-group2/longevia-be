# Mô hình dữ liệu — `recommendation_db` (schema `kb`)

**Feature**: `duck/001/recommendation-rag` | **Date**: 2026-10-22 | **Plan**: [plan.md](./plan.md)

> Ràng buộc Constitution 2.1.0 §I: `recommendation_db` **chỉ** chứa corpus tri thức đã nhúng. **Không bảng nào** trong đây chứa dữ liệu người dùng.

---

## 1. Sơ đồ quan hệ

```
kb.corpus (1) ──┬──< kb.chapter (13) ──< kb.chunk (188 đo, đo trên corpus thật)
                 │                              │
                 │                              ├─ embedding  vector(768)   (L2-normalized)
                 │                              ├─ content_tsv tsvector      (GIN, do trigger duy trì)
                 │                              └─ verified / needs_review   (denormalized từ page_flag)
                 │
                 └──< kb.page_flag (~50)      (nguồn sự thật cho cờ chất lượng, theo trang)
```

Quan hệ: `corpus` 1—N `chapter` 1—N `chunk`; `corpus` 1—N `page_flag`. `chunk` tham chiếu `chapter`; `page_flag` **không** tham chiếu `chunk` (cờ gắn theo **trang**, chunk suy ra cờ từ tập trang của nó).

---

## 2. Bảng

### 2.1 `kb.corpus` — phiên bản tri thức

Một bản ghi cho **mỗi lần nạp** bộ tài liệu tham chiếu. Cho phép quay lại phiên bản trước và làm nạp lại idempotent.

| Cột | Kiểu | Ràng buộc | Ý nghĩa |
| --- | --- | --- | --- |
| `id` | `uuid` | PK | |
| `corpus_key` | `text` | NOT NULL, `UNIQUE (corpus_key, version)` | định danh corpus, ví dụ `che-do-an-truong-tho` |
| `version` | `text` | NOT NULL | SHA-256 nội dung 273 file Markdown → đổi nội dung = version mới |
| `title` | `text` | NOT NULL | |
| `source_path` | `text` | NOT NULL | `docs/reference/che-do-an-truong-tho` |
| `chapter_count` | `int` | NOT NULL | 13 |
| `chunk_count` | `int` | NOT NULL | 188 (đo trên corpus thật, chunk 1000 token) |
| `embedding_model` | `text` | NOT NULL | `gemini-embedding-001` |
| `embedding_dim` | `int` | NOT NULL | 768 |
| `chunker_params` | `jsonb` | NOT NULL | `{ "tokens": 1000, "overlap": 150 }` |
| `is_active` | `boolean` | NOT NULL DEFAULT false | **chỉ một version active cho mỗi `corpus_key`** → unique index có điều kiện |
| `embedded_at` | `timestamptz` | NOT NULL | |
| `created_at` | `timestamptz` | NOT NULL DEFAULT now() | |

```sql
CREATE TABLE kb.corpus (
    id              uuid        PRIMARY KEY,
    corpus_key      text        NOT NULL,
    version         text        NOT NULL,
    title           text        NOT NULL,
    source_path     text        NOT NULL,
    chapter_count   int         NOT NULL,
    chunk_count     int         NOT NULL,
    embedding_model text        NOT NULL,
    embedding_dim   int         NOT NULL,
    chunker_params  jsonb       NOT NULL,
    is_active       boolean     NOT NULL DEFAULT false,
    embedded_at     timestamptz NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (corpus_key, version)
);
CREATE UNIQUE INDEX ux_corpus_active ON kb.corpus (corpus_key) WHERE is_active;
```

### 2.2 `kb.chapter` — chương sách

| Cột | Kiểu | Ràng buộc | Nguồn |
| --- | --- | --- | --- |
| `id` | `uuid` | PK | |
| `corpus_id` | `uuid` | NOT NULL, FK → `kb.corpus(id)` ON DELETE CASCADE | |
| `chapter_number` | `int` | NOT NULL, `UNIQUE (corpus_id, chapter_number)` | `.manifest.json` → `number` (0 = phần đầu sách) |
| `title` | `text` | NOT NULL | `.manifest.json` → `title` |
| `source_file` | `text` | NOT NULL | `.manifest.json` → `file` |
| `book_page_from` | `int` | NOT NULL | trang **bản in** đầu |
| `book_page_to` | `int` | NOT NULL | trang **bản in** cuối |
| `pdf_page_from` | `int` | NOT NULL | trang file scan đầu |
| `pdf_page_to` | `int` | NOT NULL | trang file scan cuối |

### 2.3 `kb.chunk` — đơn vị truy xuất

| Cột | Kiểu | Ràng buộc | Ý nghĩa |
| --- | --- | --- | --- |
| `id` | `bigserial` | PK | |
| `corpus_id` | `uuid` | NOT NULL, FK → `kb.corpus(id)` CASCADE | |
| `chapter_id` | `uuid` | NOT NULL, FK → `kb.chapter(id)` CASCADE | |
| `chapter_number` | `int` | NOT NULL | **denormalized** để lọc theo chương không cần join |
| `chunk_index` | `int` | NOT NULL, `UNIQUE (corpus_id, chapter_id, chunk_index)` | thứ tự trong chương |
| `content` | `text` | NOT NULL | nội dung đã làm sạch OCR |
| `content_tsv` | `tsvector` | NOT NULL | **duy trì bằng trigger**, index GIN |
| `token_count` | `int` | NOT NULL | ước lượng (~2,6 ký tự/token tiếng Việt) |
| `content_hash` | `text` | NOT NULL | SHA-256 nội dung → idempotency/dedupe |
| `book_page_from` | `int` | NOT NULL | trang **bản in** đầu của chunk |
| `book_page_to` | `int` | NOT NULL | trang **bản in** cuối của chunk |
| `pdf_page_from` | `int` | NOT NULL | trang scan đầu |
| `pdf_page_to` | `int` | NOT NULL | trang scan cuối |
| `verified` | `boolean` | NOT NULL DEFAULT false | xem §4 |
| `needs_review` | `boolean` | NOT NULL DEFAULT false | xem §4 |
| `embedding` | `vector(768)` | NOT NULL | L2-normalized |
| `created_at` | `timestamptz` | NOT NULL DEFAULT now() | |

```sql
CREATE TABLE kb.chunk (
    id              bigserial PRIMARY KEY,
    corpus_id       uuid        NOT NULL REFERENCES kb.corpus(id)  ON DELETE CASCADE,
    chapter_id      uuid        NOT NULL REFERENCES kb.chapter(id) ON DELETE CASCADE,
    chapter_number  int         NOT NULL,
    chunk_index     int         NOT NULL,
    content         text        NOT NULL,
    content_tsv     tsvector    NOT NULL,
    token_count     int         NOT NULL,
    content_hash    text        NOT NULL,
    book_page_from  int         NOT NULL,
    book_page_to    int         NOT NULL,
    pdf_page_from   int         NOT NULL,
    pdf_page_to     int         NOT NULL,
    verified        boolean     NOT NULL DEFAULT false,
    needs_review    boolean     NOT NULL DEFAULT false,
    embedding       vector(768) NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (corpus_id, chapter_id, chunk_index)
);
CREATE INDEX ix_chunk_corpus  ON kb.chunk (corpus_id);
CREATE INDEX ix_chunk_chapter ON kb.chunk (corpus_id, chapter_number);
CREATE INDEX ix_chunk_pages   ON kb.chunk (book_page_from, book_page_to);
CREATE INDEX ix_chunk_tsv     ON kb.chunk USING gin (content_tsv);
CREATE INDEX ix_chunk_hnsw    ON kb.chunk USING hnsw (embedding vector_cosine_ops)
                              WITH (m = 16, ef_construction = 64);
```

### 2.4 `kb.page_flag` — cờ chất lượng theo trang (nguồn sự thật)

| Cột | Kiểu | Ràng buộc | Ý nghĩa |
| --- | --- | --- | --- |
| `id` | `bigserial` | PK | |
| `corpus_id` | `uuid` | NOT NULL, FK → `kb.corpus(id)` CASCADE | |
| `pdf_page` | `int` | NOT NULL | trang trong file scan (khoá của các file `review/*.md`) |
| `book_page` | `int` | NOT NULL | trang bản in tương ứng |
| `kind` | `text` | NOT NULL | xem bảng bên dưới |
| `source_file` | `text` | NOT NULL | `review/qa.md` \| `review/numbers.md` \| `README.md` |
| `note` | `text` | nullable | ghi chú thêm |
| | | `UNIQUE (corpus_id, pdf_page, kind, source_file)` | |

| `kind` | Nguồn | Nghĩa |
| --- | --- | --- |
| `manual_verified` | `README.md` | trang được chép tay từ ảnh gốc (27, 28, 103, 104) hoặc vá lỗi (34, 114, 135) |
| `table_verified` | `README.md` | trang có bảng đã so **từng ô số** với ảnh gốc (142, 259–261, 263–269, 271) |
| `missing_numbers` | `review/qa.md` | Tesseract đọc thấy số mà bản Markdown không có → **nghi vấn số liệu** (đa số là Tesseract sai) |
| `low_word_recall` | `review/qa.md` | trang nhiều hình/chữ nghiêng, có thể thiếu chữ |
| `longer_than_ocr` | `review/qa.md` | Markdown nhiều hơn OCR → không phải lỗi |
| `shorter_than_ocr` | `review/qa.md` | trang hình, ít chữ |

> **Quy ước khoá trang**: các file trong `review/` khoá theo **số trang PDF**, khớp trực tiếp `pdf_page` của chunk. Ghi lại trong `docs/development/rag-ingestion.md`.

---

## 3. Quy tắc nghiệp vụ trên dữ liệu

| # | Quy tắc | Nơi thực thi |
| --- | --- | --- |
| R1 | Nạp lại cùng `version` không tạo chunk trùng | `UNIQUE (corpus_id, chapter_id, chunk_index)` + upsert idempotent |
| R2 | Chỉ một `kb.corpus` active cho mỗi `corpus_key` | `ux_corpus_active` (unique index có điều kiện) |
| R3 | `content_tsv` luôn khớp `content` | trigger `BEFORE INSERT OR UPDATE OF content` |
| R4 | `verified` / `needs_review` của chunk = hợp của cờ của các trang trong `book_page_from..book_page_to` | tính lúc ingest (§4) |
| R5 | `chapter_number` phải khớp `kb.chapter.chapter_number` | chỉ ghi qua pipeline ingest |
| R6 | `embedding` luôn L2-normalized | chuẩn hoá ngay sau khi nhúng (D-13) |
| R7 | Không có cột nào chứa dữ liệu người dùng | Constitution 2.1.0 §I — kiểm tra bằng integration test (SC-009) |

---

## 4. Quy tắc tính cờ `verified` / `needs_review`

Nguồn: `kb.page_flag` (nguồn sự thật) → suy ra cờ trên `kb.chunk` (denormalized để lọc nhanh).

```
V(page)  = tồn tại kind ∈ {manual_verified, table_verified} cho page
S(page)  = tồn tại kind ∈ {missing_numbers, low_word_recall, longer_than_ocr, shorter_than_ocr}
           VÀ không có kind ∈ {manual_verified, table_verified} cho page

chunk.verified     = NOT S(page)  cho MỌI page trong chunk
chunk.needs_review = S(page)      cho ÍT NHẤT một page trong chunk
```

**Kết quả trên corpus hiện tại** (273 trang):

| Nhóm | Số trang |
| --- | --- |
| Cờ nghi vấn (nguồn `review/qa.md`) | 47 |
| Trong đó đã bị ghi đè bởi `manual_verified`/`table_verified` | 7 (trang 135, 263, 264, 266, 267, 268, 269) |
| **Còn lại cờ nghi vấn** | **40 (≈ 14,7% số trang)** |
| Cờ nghi vấn đã ghi đè (`table_verified`) | 10 (trang 142, 259–261, 263–265, 267–268, 271) |
| Đã đối chiếu thủ công khác (`manual_verified`) | 7 (trang 27, 28, 34, 103, 104, 114, 135) |
| `verified = true` nghiêm ngặt (chỉ trang đã kiểm chứng) | **19 (≈ 7%)** → không dùng làm điều kiện lọc (D-07) |
| Chunk `needs_review` (đo trên DB sau khi nạp) | **58/188 ≈ 30,9%** — chunk phủ nhiều trang nên tỉ lệ theo chunk cao hơn theo trang |
| Chunk `verified` (đo trên DB) | **130/188 ≈ 69,1%** |

---

## 5. Truy vấn truy xuất (hybrid + RRF)

```sql
WITH vector_candidates AS (
    SELECT c.id,
           ROW_NUMBER() OVER (ORDER BY c.embedding <=> CAST(@qvec AS vector)) AS rank
    FROM kb.chunk AS c
    WHERE c.corpus_id = @corpus
      AND (@chapter = 0 OR c.chapter_number = @chapter)
      AND (NOT @requireVerified OR c.verified)
    ORDER BY c.embedding <=> CAST(@qvec AS vector)
    LIMIT @maxCandidates
), keyword_candidates AS (
    SELECT c.id,
           ROW_NUMBER() OVER (
               ORDER BY ts_rank(c.content_tsv, plainto_tsquery('simple', unaccent(@q))) DESC) AS rank
    FROM kb.chunk AS c
    WHERE c.corpus_id = @corpus
      AND (@chapter = 0 OR c.chapter_number = @chapter)
      AND (NOT @requireVerified OR c.verified)
      AND c.content_tsv @@ plainto_tsquery('simple', unaccent(@q))
    LIMIT @maxCandidates
)
SELECT c.id, c.corpus_id, c.content, c.chapter_number,
       ch.title AS chapter_title, ch.source_file,
       c.book_page_from, c.book_page_to, c.pdf_page_from, c.pdf_page_to,
       c.verified, c.needs_review,
       SUM(1.0 / (60 + merged.rank)) AS rrf_score
FROM (SELECT * FROM vector_candidates UNION ALL SELECT * FROM keyword_candidates) AS merged
JOIN kb.chunk AS c   ON c.id = merged.id
JOIN kb.chapter AS ch ON ch.id = c.chapter_id
GROUP BY c.id, ch.id
ORDER BY rrf_score DESC
LIMIT @topK;
```

Ghi chú triển khai:
- `@qvec` = chuỗi dạng `[0.1,0.2,...]` của vector **câu hỏi đã L2-normalize**, truyền kiểu text và cast bằng `CAST(@qvec AS vector)`. `$1::vector` **không** dùng được trong `FromSqlRaw` của EF (tham số hoá tạo `$1`), nên dùng `CAST(...)` — tương đương về ngữ nghĩa và đã kiểm chứng trên `recommendation_db`.
- **`kb.chunk` không có cột `chapter_title`/`source_file`** — hai cột này nằm ở `kb.chapter`, nên truy vấn phải `JOIN kb.chapter` để lấy tiêu đề chương và tệp nguồn cho `Citation`.
- `unaccent(@q)` áp vào **tham số** → planner vẫn dùng GIN index (đã verify bằng `EXPLAIN`).
- `LIMIT @topK` mặc định 8 (tối đa 20); `@maxCandidates` mặc định 50 (`RAG__TOP_K`, `RAG__MAX_CANDIDATES`).
- Trả về **projection** (không select `embedding`) — xem D-14.
- Cờ `@requireVerified` = `RAG__REQUIRE_VERIFIED` **hoặc** yêu cầu từ request (mặc định `false`, theo D-07).

---

## 6. Entity nội bộ ↔ bảng ↔ hợp đồng

| Entity (Domain/Application) | Bảng | Trường hợp đồng (`contracts/recommendation/v1/recommendation.proto`) |
| --- | --- | --- |
| `KnowledgeChunk` | `kb.chunk` | — (nội bộ, **không** đi qua contract) |
| `Corpus` | `kb.corpus` | — |
| `Chapter` | `kb.chapter` | `Citation.chapter_number`, `Citation.chapter_title` |
| `PageFlag` | `kb.page_flag` | — (chỉ dùng lúc ingest) |
| `RetrievedChunk` | kết quả truy xuất | đầu vào của `Citation` |
| `Citation` | dựng từ `RetrievedChunk` | `Citation` |
| `UserProfile` | **không lưu** | `UserProfile` (request) |
| `CandidateFood` | **không lưu** | `CandidateFood` (request) |
| `NutrientGap` | **không lưu** | `NutrientGap` (response) |
| `ScoreComponent` | **không lưu** | `ScoreComponent` (response) |
| `Disclaimer` | hằng `Domain/Constants` | `Disclaimer` (response) |

Bảng `kb.*` chỉ chứa dữ liệu corpus. Mọi thực thể suy ra từ hồ sơ người dùng **không** được ghi vào đây.

---

## 7. Dung lượng (ước tính)

| Thành phần | Ước lượng |
| --- | --- |
| Số chunk | **188** (đo trên corpus thật; ~159.000 token ÷ ~850 token/chunk có overlap) |
| `embedding` | 768 × 4B + 8B ≈ 3,1 KB/chunk → ~0,6 MB |
| `content` + `content_tsv` | ~5 KB/chunk → ~0,9 MB |
| **Tổng schema `kb`** | **< 5 MB** |

HNSW hỗ trợ tối đa 2.000 chiều → 768 nằm thoải mái. Với ~1.300 chunk, kể cả exact search cũng dưới 50ms; index HNSW vẫn giữ để mở đường khi corpus lớn dần.

---

## 8. Migration

| Bước | Lệnh / nội dung |
| --- | --- |
| Tạo | `make ef-add NAME=InitialKnowledgeStore PROJECT=src/Services/Recommendation/Recommendation.Infrastructure` |
| Extension | `modelBuilder.HasPostgresExtension("vector")` → EF sinh `CREATE EXTENSION IF NOT EXISTS vector;` **trong migration** (đúng Code First) |
| `unaccent` | `migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;")` — cần cho trigger `tsvector` |
| Trigger `tsvector` | `migrationBuilder.Sql(...)` tạo hàm + trigger `BEFORE INSERT OR UPDATE OF content` |
| Index HNSW | khai báo bằng annotation EF → sinh đúng DDL `USING hnsw (embedding vector_cosine_ops) WITH (m=16, ef_construction=64)` |
| Role DB | `recommendation_user` phải có quyền `CREATE EXTENSION` — cấp trong `deployments/postgres/init/01-databases.sh` |
| Áp dụng | auto-migrate lúc khởi động (theo `docs/development/migration.md`); chỉ migration của `recommendation_db` |
| Quy tắc | không sửa migration đã áp dụng; serialize số thứ tự migration (chỉ feature này mở service Recommendation) |
