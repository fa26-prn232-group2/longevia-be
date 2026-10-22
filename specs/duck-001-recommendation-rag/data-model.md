# Mô hình dữ liệu — `recommendation_db` (schema `kb`)

**Feature**: `duck/001/recommendation-rag` | **Date**: 2026-10-22 | **Plan**: [plan.md](./plan.md)

> Ràng buộc Constitution 2.1.0 §I: `recommendation_db` **chỉ** chứa corpus tri thức đã nhúng. **Không bảng nào** trong đây chứa dữ liệu người dùng.

---

## 1. Sơ đồ quan hệ

```
kb.corpus (1) ──┬──< kb.chapter (13) ──< kb.chunk (~900–1.300)
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
| `chunk_count` | `int` | NOT NULL | ~900–1.300 |
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
| Trong đó đã bị ghi đè bởi `table_verified` | 2 (trang 266, 269) |
| **Còn lại cờ nghi vấn** | **45 (≈ 16,5%)** |
| Cờ nghi vấn đã ghi đè (`table_verified`) | 10 (trang 142, 259–261, 263–265, 267–268, 271) |
| Đã đối chiếu thủ công khác (`manual_verified`) | 7 (trang 27, 28, 34, 103, 104, 114, 135) |
| `verified = true` nghiêm ngặt (chỉ trang đã kiểm chứng) | **19 (≈ 7%)** → không dùng làm điều kiện lọc (D-07) |

---

## 5. Truy vấn truy xuất (hybrid + RRF)

```sql
WITH v AS (
    SELECT id,
           ROW_NUMBER() OVER (ORDER BY embedding <=> @qvec) AS r
    FROM kb.chunk
    WHERE corpus_id = @corpus
      AND (@chapter = 0 OR chapter_number = @chapter)
    ORDER BY embedding <=> @qvec
    LIMIT 50
), k AS (
    SELECT id,
           ROW_NUMBER() OVER (ORDER BY ts_rank(content_tsv, plainto_tsquery('simple', unaccent(@q))) DESC) AS r
    FROM kb.chunk
    WHERE corpus_id = @corpus
      AND (@chapter = 0 OR chapter_number = @chapter)
      AND content_tsv @@ plainto_tsquery('simple', unaccent(@q))
    LIMIT 50
)
SELECT c.id, c.content, c.chapter_number, c.chapter_title,
       c.book_page_from, c.book_page_to, c.pdf_page_from, c.pdf_page_to,
       c.verified, c.needs_review,
       SUM(1.0 / (60 + s.r)) AS rrf_score
FROM (SELECT * FROM v UNION ALL SELECT * FROM k) s
JOIN kb.chunk c ON c.id = s.id
GROUP BY c.id
ORDER BY rrf_score DESC
LIMIT 8;
```

Ghi chú triển khai:
- `@qvec` = chuỗi dạng `[0.1,0.2,...]` của vector **câu hỏi đã L2-normalize**, truyền kiểu text + cast `$1::vector`.
- `unaccent(@q)` áp vào **tham số** → planner vẫn dùng GIN index (đã verify bằng `EXPLAIN`).
- `LIMIT 8` = `RAG__TOP_K`; `50` = `RAG__MAX_CANDIDATES`.
- Trả về **projection** (không select `embedding`) — xem D-14.

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
| Số chunk | ~900–1.300 |
| `embedding` | 768 × 4B + 8B ≈ 3,1 KB/chunk → ~4 MB |
| `content` + `content_tsv` | ~5 KB/chunk → ~6 MB |
| **Tổng schema `kb`** | **< 20 MB** |

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
