# ADR 002 - Knowledge Base & RAG cho Recommendation Service

- **Status**: Accepted
- **Decision Date**: 2026-10-22
- **Decision Maker**: Duck

## 1. Context

### 1.1 Yêu cầu gốc

Bài toán Longevia không chỉ là "sinh gợi ý bằng AI". Nguồn tri thức chuẩn của sản
phẩm là cuốn sách **"Chế độ ăn trường thọ"**, đã được số hoá thành Markdown
(273 trang bản in, sách 9–281, 13 chương) tại
[`docs/reference/che-do-an-truong-tho/`](../reference/che-do-an-truong-tho/README.md).
Yêu cầu là **RAG (Retrieval-Augmented Generation)**: mọi khuyến nghị phải bám
và có thể truy ngược về đoạn cụ thể trong sách.

### 1.2 Đặc tả hiện tại chưa phản ánh điều đó

- `docs/requirements/prd.md` §2.4 mô tả Recommendation chỉ là *"gọi Google AI
  Gemini với structured prompting ép JSON"* — không ingestion, không embedding,
  không retrieval, không grounding.
- `MealRecommendationResponse.longevity_tip` là chuỗi do LLM sinh tự do, **không
  có nguồn trích dẫn**. `docs/requirements/brd.md` §7 xếp "phản hồi sai lệch từ
  AI (hallucination)" là rủi ro mức **Cao**, và biện pháp giảm thiểu nêu rõ:
  ràng buộc AI, ép JSON schema, chỉ chọn nguyên liệu trong danh mục an toàn.
- `RecommendedMeal` để LLM **tự sinh cả tên món lẫn calo/macro** → không khớp
  `FoodItems` của Diet, người dùng không log được món đó, và **không thể bảo đảm
  100% loại trừ dị ứng** như BRD BR-03 (`brd.md:83`) yêu cầu.
- `AnalyzeNutrientGaps` không có hồ sơ người dùng (tuổi, giới, dị ứng, sở thích)
  → "phân tích thiếu hụt vi chất" không thể cá nhân hoá.
- `EvaluateLongevityHabits` trả `longevity_score` 1–100 **không rubric** → không
  deterministic, không kiểm thử được.
- Không có RPC nào phục vụ tra cứu tri thức; ma trận truy xuất (PRD §4) không có
  dòng nào cho tri thức sách.
- BRD BR-04 bắt buộc mọi đề xuất kèm khuyến cáo tham vấo bác sĩ, nhưng **không
  field nào trong contract mang disclaimer**.

### 1.3 Xung đột với hiến pháp và sơ đồ

- `constitution.md` nguyên tắc I: *"Service stateless (Recommendation) không sở hữu
  database nào"*, và `prd.md:281` ghi *"Không truy cập DB"*.
- Nhưng sơ đồ C4 level 2 (`docs/system-design/diagrams/png/c4-model-lv2.png`,
  commit `f6cb40b`) **đã vẽ `Recommendation Database`** ngang hàng với Diet /
  Identity / Progress Database.

Tức là **sơ đồ đã đi trước văn bản**. RAG bắt buộc phải có nơi lưu vector, nên
không thể giữ nguyên cả hai. Cần một quyết định tường minh thay vì diễn giải.

### 1.4 Ràng buộc của corpus

- Bản Markdown là **OCR của sách scan**, chưa đối chiếu tay toàn bộ
  (`reviewed: false`).
- Đánh dấu trang trong file là `<!-- sách N · PDF M -->`: **số trang bản in là
  nguồn chuẩn**, kèm số trang PDF scan để đối chiếu file gốc. Quy tắc ánh xạ
  nằm ở `scripts/book-ocr/src/pagemap.py`; `review/page-numbers.md` ghi nhận
  **0 cụm sai lệch thật** trên toàn sách.
- `review/numbers.md` và `review/qa.md` liệt kê các trang có nghi vấn số liệu.
  Các file review này khoá theo **số trang PDF**.
- Trang trùng (PDF 91 = ảnh chụp lần hai của sách 98) **đã bị loại** khỏi bản
  Markdown, nên corpus là 273 trang, không cần dedupe khi ingest.

## 2. Decision

### 2.1 Nơi lưu tri thức — PostgreSQL + pgvector trong `recommendation_db`

- Recommendation **sở hữu** database riêng tên `recommendation_db`, schema `kb`,
  lưu corpus tri thức đã nhúng:

  ```sql
  CREATE SCHEMA kb;
  CREATE TABLE kb.chunks (
      id             bigserial PRIMARY KEY,
      corpus_version text        NOT NULL,
      source_file    text        NOT NULL,
      chapter_number int         NOT NULL,
      chapter_title  text        NOT NULL,
      chunk_index    int         NOT NULL,
      book_page_from int         NOT NULL,
      book_page_to   int         NOT NULL,
      pdf_page_from  int         NOT NULL,
      pdf_page_to    int         NOT NULL,
      content        text        NOT NULL,
      token_count    int         NOT NULL,
      verified       boolean     NOT NULL DEFAULT false,
      needs_review   boolean     NOT NULL DEFAULT false,
      embedding      vector(768) NOT NULL,
      created_at     timestamptz NOT NULL DEFAULT now(),
      UNIQUE (corpus_version, source_file, chunk_index)
  );
  ```

- `recommendation_db` là DB thứ 5, **tách biệt** khỏi `identity_db` / `diet_db` /
  `progress_db`; role Postgres riêng; image Postgres đổi sang
  `pgvector/pgvector:pg17`.
- `corpus_version` = hash nội dung corpus → nạp lại idempotent, không nhân bản
  chunk.
- `verified` / `needs_review` là cờ chất lượng dữ liệu, **không** phải cờ tin cậy
  của AI: chunk `needs_review = true` (trang có nghi vấn số liệu theo
  `review/numbers.md` / `review/qa.md`) **không được dùng để khẳng định** một
  con số hay khuyến nghị y tế.

### 2.2 Ranh giới dữ liệu — DB knowledge không chứa dữ liệu người dùng

- `recommendation_db` **MUST NOT** chứa dữ liệu người dùng: không hồ sơ, không
  nhật ký, không token, không kết quả cá nhân hoá.
- Mọi dữ liệu người dùng đi vào Recommendation **qua gRPC request** và trả về
  **qua gRPC response**; không lưu lại.
- Recommendation vẫn là **service nội bộ, gRPC-only**, **MUST NOT** được expose
  qua Nginx; container `recommendation` chỉ join `internal_net` + `data_net`,
  **không** join `edge`.
- Quy tắc này được nâng thành nguyên tắc hiến pháp (amend `constitution.md` lên
  `2.0.0`) — xem §4.

### 2.3 Pipeline tri thức — offline, không nằm trong runtime service

- Tooling mới `scripts/book-kb/` (Python, dùng `google-genai` sẵn có của
  `scripts/book-ocr`): `corpus` → `clean` → `chunk` → `embed` → `load` → `verify`.
- Chunking theo chương, cắt tại ranh giới đoạn, **không cắt giữa bảng**; target
  **1000 token**, overlap **150 token**.
- Metadata lấy từ `.manifest.json` (`file`, `title`, `number`, `book_pages`,
  `pdf_pages`) và từ marker `<!-- sách N · PDF M -->` trong từng file.
- Embedding: `gemini-embedding-001`, `output_dimensionality = 768` (số chiều cố
  định theo schema `vector(768)`).
- **Fail fast**: file nào thiếu marker trang thì pipeline dừng, không ingest sai
  số trang.
- Corpus là **dữ liệu, không phải chỉ dẫn**: nội dung sách luôn được đưa vào
  prompt như dữ liệu để chặn prompt injection.

### 2.4 Truy xuất và sinh câu trả lời

- Retrieval **hybrid**: vector cosine (pgvector) + BM25 (tiếng Việt, bỏ dấu) →
  merge bằng RRF → **top-k = 8**; candidate vector **50**.
- Sinh câu trả lời: Gemini với `response_mime_type = application/json` +
  `response_schema`; system prompt ràng buộc *"chỉ dùng thông tin trong CONTEXT,
  không đủ thì abstain"*.
- **Citation bắt buộc**: mỗi khẳng định gắn `{chapter, chapter_title, book_page,
  pdf_page, quote, verified}`. `book_page` = trang **bản in** (nguồn chuẩn từ
  marker), `pdf_page` = trang file scan (để đối chiếu). `quote` phải được **verify
  khớp chuỗi** với chunk; không khớp thì bỏ citation và hạ response xuống
  `abstained`.
- **Phần deterministic không giao cho LLM**: lọc dị ứng 100% (BR-03), lọc sở
  thích ăn uống (BR-02), rule chạm trần **105%** calo (BR-03), tính gap vi chất
  theo bảng RDA, tính `longevity_score` theo rubric cố định
  (fasting 35% / sleep 30% / hydration 20% / nutrition 15%).
- **Mọi response mang `disclaimer`** (BR-04).
- Provider lỗi / timeout → trả kết quả rỗng kèm cảnh báo, **không** bịa nội
  dung.

### 2.5 Contract — sửa và bổ sung

- `GetMealRecommendations`: Diet gửi kèm `candidate_foods` (món trong danh mục
  an toàn) → Recommendation **lọc dị ứng bằng code rồi** mới để LLM chọn/pha;
  LLM **không** tự sinh món và **không** tự bịa calo. Response trả `food_id` map
  về `FoodItems` của Diet.
- Thêm 2 RPC: `AskLongevityQuestion` (hỏi sách, có citation, có cờ `abstained`)
  và `SearchLongevityKnowledge` (tra cứu đoạn theo truy vấn).
- Thêm `citations`, `disclaimer`, `score_breakdown`, `NutrientGap` chi tiết và hồ
  sơ người dùng vào request.

## 3. Consequences

### 3.1 Tích cực

- Khuyến nghị **có căn cứ truy ngược được** (chương + trang bản in + trích
  đoạn) → giảm rủi ro hallucination ở mức "Cao" mà BRD đã cảnh báo.
- LLM **không** bịa món ăn → món gợi ý luôn log được vào nhật ký, và dị ứng được
  lọc bằng code (kiểm thử được) thay vì bằng lời nhắc trong prompt.
- Điểm Longevity và gap vi chất trở nên **deterministic** → test được, đo được
  theo KPI.
- Corpus số hoá sẵn được tái sử dụng; tái index **không cần** rebuild image nhờ
  DB.

### 3.2 Tiêu cực

- **Mất tính "stateless" thuần** của Recommendation → phải amend Constitution
  (`2.0.0`) và cập nhật văn bản cho khớp sơ đồ C4.
- Thêm 1 database, thêm 1 image Postgres (`pgvector/pgvector:pg17`), thêm bước
  build corpus ngoài runtime.
- Chi phí embedding một lần theo corpus (~700–1200 chunk) + chi phí Gemini mỗi
  request; cần cache.
- RAG làm latency dài hơn một lệnh gọi LLM thuần → phải đặt budget riêng
  (retrieval < 150ms, tổng gRPC < 3500ms).
- Chất lượng corpus chịu ảnh hưởng OCR: `reviewed: false`, còn trang nghi vấn
  số liệu → phải có cờ chất lượng và disclaimer; **không** được dùng số liệu
  chưa đối chiếu như sự thật y tế.

### 3.3 Rủi ro kỹ thuật cần xử lý khi triển khai

- **Tương thích provider vector với EF Core 10**: nếu package `EFCore.PGVector`
  chưa tương thích, dùng cột `vector(768)` + raw SQL (`$1::vector`) và đọc lại
  bằng `embedding::text` → **không** phụ thuộc package bên thứ ba. Kết quả kiểm
  tra thực tế phải được ghi lại ở mục "Bổ sung".
- **Chi phí & độ trễ**: cache theo hash (context + prompt + profile chuẩn hoá),
  top-k nhỏ, timeout rõ ràng.

## 4. Bổ sung (2026-10-22)

- Sửa đổi hiến pháp sinh ra từ ADR này: `constitution.md` `1.0.0` → `2.0.0`
  (nguyên tắc I, MAJOR vì đổi nghĩa một nguyên tắc), ghi log trong mục
  Governance.
- Thứ tự ưu tiên tài liệu sau khi sửa:
  `Constitution > system-design > development > khác` (không đổi).

## 5. Điều kiện đánh giá lại quyết định này

- (a) Tái index không còn cần deploy lại service → cân nhắc bỏ `corpus_version`
  và dùng cơ chế version duy nhất.
- (b) Corpus vượt quá ~50k chunk → cân nhắc tách bảng vector ra store riêng hoặc
  đổi sang dịch vụ RAG quản lý (Gemini File Search / Vertex AI Search).
- (c) Cần cho phép nhiều corpus (nhiều cuốn sách) → `kb.chunks` hiện thiết kế
  theo `source_file` + `corpus_version` nhưng chưa có bảng `corpora`; phải bổ
  sung trước khi dùng.
- (d) Yêu cầu tra cứu sách từ client trực tiếp (ngoài gRPC nội bộ) → phải có
  ADR riêng vì vi phạm quy tắc "Recommendation không expose REST qua Nginx".

## Liên quan

- [../system-design/architecture.md](../system-design/architecture.md)
- [../system-design/microservices/01-service-boundaries.md](../system-design/microservices/01-service-boundaries.md)
- [../requirements/prd.md](../requirements/prd.md) — §2.4, §3.1, §4
- [../requirements/brd.md](../requirements/brd.md) — BR-02, BR-03, BR-04, §7
- [../api/api-spec.md](../api/api-spec.md) — §5
- [../reference/che-do-an-truong-tho/README.md](../reference/che-do-an-truong-tho/README.md)
- [001-authn-authz-architecture.md](001-authn-authz-architecture.md)
- [../../.specify/memory/constitution.md](../../.specify/memory/constitution.md)

### Bổ sung (2026-10-23) — Seed và bước deploy `kb-init`

Quyết định D-02 (ingest là pipeline offline) vẫn giữ nguyên, nhưng **load** được
tách khỏi **re-index** để môi trường mới không phải gọi provider:

- Artifact đã embed được đóng gói thành **seed** tại `resources/knowledge/<corpus_key>/`
  (`build.json`, `chunks.jsonl.gz`, `page_flags.jsonl.gz`) và commit vào repo.
- Khi deploy, compose chạy hai bước một lần: `kb-schema` (chạy chính image service với
  `--migrate-only`, tạo schema `kb` bằng migration EF Core) rồi `kb-init`
  (`load --work /seed --skip-if-loaded --verify`). Chỉ sau đó `recommendation` mới khởi động.
- Nạp seed **không cần** `GOOGLE_AI__API_KEY`; re-index vẫn cần và vẫn thủ công.
- Runtime service vẫn **read-only**; `kb-init` là bước deploy, không phải runtime.