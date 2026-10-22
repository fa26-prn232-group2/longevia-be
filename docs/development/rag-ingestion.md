# Nạp kho tri thức sách (RAG ingestion)

**Feature**: `duck/001/recommendation-rag` — pipeline offline `scripts/book-kb`.
**Đích ghi**: `recommendation_db`, schema `kb` (chỉ chứa corpus, **không** chứa dữ liệu người dùng — Constitution §I).

---

## 1. Nguồn và quy ước trang

Corpus nằm ở `docs/reference/che-do-an-truong-tho/`:

- `.manifest.json` — 13 chương, mỗi mục có `file`, `title`, `number`, `book_pages`, `pdf_pages`.
- `NN-<slug>.md` — nội dung từng chương; `00-front-matter.md` là chương số `0`.
- Mỗi file có marker trang ở đầu mỗi trang:

  ```text
  <!-- sách 35 · PDF 27 -->
  ```

  **Số bản in đứng trước, số trang PDF đứng sau.** Marker là **nguồn chuẩn** cho
  `book_page` / `pdf_page`; mọi metadata của chunk lấy từ marker, không suy đoán.

- File nào thiếu marker, marker đầu không khớp `book_pages`/`pdf_pages`, hoặc
  trang bản in không liên tục → pipeline **dừng ngay** (`CorpusError`).
- PDF 91 là ảnh chụp trùng của một trang sách và **đã bị loại** khỏi bản
  Markdown. Vì vậy tổng cộng có **273 trang** (sách 9–281), không phải 274.
  Trang không có marker (chỉ có thể là PDF 91) được ánh xạ dự phòng: PDF < 91
  cộng 8, PDF ≥ 91 cộng 7; mọi trang khác phải lấy từ marker.

### Manifest là khoảng nửa mở

Với các chương ở giữa sách, `book_pages`/`pdf_pages` của manifest ghi **trang bắt
đầu của chương này đến trang bắt đầu của chương kế tiếp** (ví dụ chương 1 ghi
`22-36`, nhưng marker cuối của chương là 35; chương 2 bắt đầu ở 36). Pipeline
lưu nguyên khoảng trong manifest vào `kb.chapter` và kiểm tra marker đầu khớp
`from`, marker cuối không vượt quá `to`. Trang thực tế của chunk vẫn lấy từ
marker nên luôn chính xác.

---

## 2. Các file review khoá theo **số trang PDF**

| File | Nội dung | Khoá |
| --- | --- | --- |
| `review/qa.md` | bảng đối chiếu Gemini ↔ Tesseract, cột `Flags` | số trang **PDF** |
| `review/numbers.md` | các dòng `- Trang N: \`giá trị\`` | số trang **PDF** |
| `README.md` §"Đã kiểm chứng thủ công" | trang chép tay / vá / so bảng | số trang **PDF** |

`review/numbers.md` là **cùng tập trang** với cờ `missing_numbers` trong
`review/qa.md`; giá trị số được ghi vào `note` của cờ `missing_numbers` để còn
truy vết. Nếu hai tập lệch nhau, pipeline in cảnh báo và vẫn tạo cờ cho trang chỉ
có trong một nguồn.

### Các loại cờ

| `kind` | Nguồn | Nghĩa |
| --- | --- | --- |
| `manual_verified` | `README.md` | trang chép tay hoặc đã vá lỗi |
| `table_verified` | `README.md` | bảng đã so từng ô số với ảnh gốc |
| `missing_numbers` | `review/qa.md`, `review/numbers.md` | Tesseract thấy số mà Markdown không có (phần lớn là Tesseract đọc sai) |
| `low_word_recall` | `review/qa.md` | trang nhiều hình/chữ nghiêng, có thể thiếu chữ |
| `longer_than_ocr` | `review/qa.md` | Markdown nhiều hơn OCR (không phải lỗi) |
| `shorter_than_ocr` | `review/qa.md` | trang hình, ít chữ |

### Quy tắc ghi đè khi tính cờ chunk

```text
V(page) = có manual_verified hoặc table_verified
S(page) = có cờ nghi vấn VÀ không có V(page)

chunk.needs_review = tồn tại page ∈ S trong book_page_from..book_page_to
chunk.verified     = NOT chunk.needs_review
```

`manual_verified` / `table_verified` **ghi đè** cờ nghi vấn của cùng trang. Cờ
vẫn được lưu đầy đủ trong `kb.page_flag` (nguồn sự thật); việc ghi đè chỉ diễn
ra khi suy ra cờ của chunk.

### Số liệu thực tế của corpus (2026-10-22)

| Nhóm | Số lượng |
| --- | --- |
| Trang có cờ nghi vấn trong `review/qa.md` | 47 |
| Trang đã đối chiếu (`manual_verified` + `table_verified`) | 19 |
| Trang vừa có cờ vừa đã đối chiếu (bị ghi đè) | **7** (PDF 135, 263, 264, 266, 267, 268, 269 — xem ghi chú) |
| Trang **còn** cờ nghi vấn | **40 / 273 ≈ 14,7 %** |
| Chunk `needs_review` | **58 / 188 ≈ 30,9 %** |

> Ghi chú: `specs/duck-001-recommendation-rag/data-model.md` §4 ghi kỳ vọng
> "47 – 2 = 45 trang" và chỉ nêu 266, 269. Đối chiếu trực tiếp `review/qa.md` +
> `README.md` cho thấy giao của hai tập là 7 trang (gồm cả 135 do
> `manual_verified`, và 263/264/267/268 do `table_verified`), nên kết quả theo
> đúng công thức là 40 trang còn cờ. Pipeline báo cáo số liệu thật thay vì ép
> về con số kỳ vọng. Cần orchestrator quyết định nếu muốn đổi quy tắc ghi đè.

---

## 3. Chunk và embedding

- Target **1000 token**, overlap **150 token** (`RAG__CHUNK_TOKENS`,
  `RAG__CHUNK_OVERLAP`). Token ước lượng `len(text) / 2.6` cho tiếng Việt.
- Cắt ở **ranh giới đoạn** (`\n\n`); bảng Markdown là **một block nguyên vẹn**
  nên có thể vượt 1000 token nếu bản thân bảng dài hơn ngưỡng.
- Bỏ placeholder `[[HINH:n]]`, gộp khoảng trắng trong dòng, bỏ dòng kẻ ngang;
  giữ nguyên từng dòng bảng.
- Mỗi chunk ghi `book_page_from/to`, `pdf_page_from/to` (min/max của các block),
  `chapter_number`, `chapter_id`, `corpus_id`, `chunk_index`, `token_count`,
  `content_hash` (SHA-256).
- Embedding: `gemini-embedding-001`, số chiều `RAG__EMBEDDING_DIM` (768),
  `TaskType = RETRIEVAL_DOCUMENT`, **L2-normalize thủ công** trước khi lưu.
- Cache: `artifacts/book-kb/cache/<2 ký tự>/<sha256>.json`; khoá theo SHA-256
  nội dung và có metadata model/số chiều. Chạy lại chỉ nhúng phần còn thiếu.
- `content_tsv` **không** được ghi từ pipeline: trigger
  `trg_chunk_content_tsv` (`BEFORE INSERT OR UPDATE OF content`) tự tính
  `to_tsvector('simple', unaccent(NEW.content))`.

Với corpus hiện tại (273 trang, ~159.000 token) pipeline sinh **188 chunk**.
Con số "~900–1.300 chunk" trong `data-model.md` §7 là ước lượng cao hơn thực tế
với target 1000 token; không dùng nó làm điều kiện verify.

---

## 4. Nạp vào database (idempotent)

`kb.corpus`:

- khoá `(corpus_key, version)`; `version` = SHA-256 trên 273 trang (tên file +
  nội dung), xếp theo `(tên file, số trang PDF)`;
- version mới → `is_active = true`, các version cũ cùng `corpus_key` chuyển
  `false`;
- giữ tối đa **2 version**, version cũ nhất bị xoá (cascade chapter/chunk/flag).

`kb.chapter` theo `(corpus_id, chapter_number)`; `kb.chunk` theo
`(corpus_id, chapter_id, chunk_index)` với `ON CONFLICT ... DO UPDATE`;
`kb.page_flag` theo `(corpus_id, pdf_page, kind, source_file)`.

Ghi embedding bằng tham số chuỗi `'[..]'` + `CAST(%s AS vector)`. Chunk dư của
cùng version (nếu chunker đổi số đoạn) bị xoá theo `chunk_index >= số chunk mới`.

---

## 5. Rebuild / chạy lại

Chuẩn bị:

```powershell
# Key chỉ lấy từ biến môi trường; không commit, không in ra log.
$env:GOOGLE_AI__API_KEY = "<key>"
```

Chạy:

```powershell
make kb-build    # build image + chunk + embed -> artifacts/book-kb
make kb-load     # upsert vào recommendation_db (idempotent)
make kb-verify   # đối chiếu chương/chunk/page range/embedding/cờ
```

`make kb-build` mount `docs/reference/che-do-an-truong-tho` (read-only) và
`artifacts/book-kb` (read/write). Container join network `longevia_data_net` và
kết nối `postgres:5432` bằng `ConnectionStrings__Recommendation` trong
`scripts/book-kb/compose.yml`.

### Khi bị giới hạn quota

- Mỗi batch thành công được ghi cache ngay; lần chạy sau **tiếp tục từ chỗ
  dừng**, không nhúng lại phần đã có.
- Retry tự động (429/5xx/quota) với backoff 5s → 10s → 20s → 40s → 60s → 120s.
- Nếu vẫn chưa xong sau tối đa 3 lần chạy, **không** được coi là thành công:
  báo cáo tỉ lệ đã nhúng (ví dụ `x/188 chunk`) và để `make kb-load` chưa chạy.

### Kiểm tra nhanh

```powershell
make kb-verify
```

Kết quả `ok: true` yêu cầu: đúng 1 corpus active, số chương khớp build, số chunk
khớp `chunk_count` và `build.json`, embedding đúng số chiều, `content_hash` dài
64, mọi khoảng trang nằm trong chương, và có `kb.page_flag`.

---

## 6. Bật `RAG__REQUIRE_VERIFIED=true`

- Mặc định `false` (D-07): chunk chưa đối chiếu **vẫn được truy xuất**, nhưng
  response/citation mang `verified = false` để client hiển thị badge.
- Khi `RAG__REQUIRE_VERIFIED=true`, truy vấn chỉ dùng chunk có `verified = true`,
  tức **không còn trang nghi vấn** trong khoảng trang của chunk. Đây **không**
  phải "chỉ trang đã kiểm chứng thủ công": corpus chỉ có 19 trang như vậy, nếu
  lọc theo nghĩa hẹp đó thì gần như không còn dữ liệu để trả lời.
- Có thể bật theo từng request qua `require_verified` trong contract; giá trị
  cấu hình là mức sàn (`config OR request`).

Bật tạm trong compose/host:

```powershell
$env:RAG__REQUIRE_VERIFIED = "true"
make kb-verify   # verify không phụ thuộc cờ này, nhưng dùng để xem tỉ lệ verified
```

Sau khi bật, chạy lại integration test/eval để đo tác động lên chất lượng trả lời.

---

## 7. Lỗi thường gặp

| Triệu chứng | Nguyên nhân / cách xử lý |
| --- | --- |
| `GOOGLE_AI__API_KEY chưa được đặt` | Chưa export biến trong shell chạy `make`; key trong `.env` đang để trống |
| `could not translate host name "postgres"` | Container không join `longevia_data_net`, hoặc postgres chưa chạy |
| `password authentication failed` | `POSTGRES_APP_PASSWORD` trong `.env` khác mật khẩu role `recommendation_user` |
| `Thiếu marker trang` | File Markdown mới chưa có `<!-- sách N · PDF M -->`; sửa corpus, không sửa pipeline để bỏ qua |
| `qa.md trang ... cờ không nhận diện được` | Có loại cờ mới chưa khai báo trong `review.py`; cập nhật code + test trước |
| Verify báo `page_ranges_valid = false` | Chunk vượt khoảng trang của chương trong manifest; kiểm tra lại việc sinh chunk/manifest |
