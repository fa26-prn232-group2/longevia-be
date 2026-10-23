# Hướng dẫn kiểm chứng — Recommendation RAG

**Feature**: `duck/001/recommendation-rag` | **Date**: 2026-10-22

Hướng dẫn **chạy và kiểm chứng** feature từ đầu đến cuối. Không chứa mã nguồn triển khai — phần đó thuộc `tasks.md` và giai đoạn implement.

Tham chiếu: [data-model.md](./data-model.md) (mô hình `kb.*`) · [contracts/recommendation.proto](./contracts/recommendation.proto) (hợp đồng gRPC) · [plan.md](./plan.md) (kế hoạch).

---

## 0. Điều kiện tiên quyết

| Thứ | Yêu cầu |
| --- | --- |
| Docker + Docker Compose | chạy Postgres `pgvector/pgvector:pg17` |
| .NET SDK 10 | `dotnet --version` → 10.x |
| `GOOGLE_AI__API_KEY` | bắt buộc cho nhúng & sinh câu trả lời; **thiếu key thì scenario 3–6 bị skip** (không được coi là pass) |
| Python 3.11+ | cho pipeline ingest offline (`scripts/book-kb`, chạy trong Docker) |

```powershell
Copy-Item .env.example .env          # điền GOOGLE_AI__API_KEY
make jwt-keys                        # nếu chưa có keypair
```

---

## 1. Hạ tầng

```powershell
docker compose -f deployments/docker-compose.yml up -d postgres
```

Kiểm chứng:
```powershell
# image phải là pgvector/pgvector:pg17 và có extension vector + unaccent
docker compose -f deployments/docker-compose.yml exec postgres `
  psql -U longevia -d recommendation_db `
  -c "SELECT extname, extversion FROM pg_extension WHERE extname IN ('vector','unaccent');"
```
**Kỳ vọng**: hai dòng — `vector` (≥ 0.8.6) và `unaccent` (1.1).

---

## 2. Nạp corpus tri thức (offline)

```powershell
make kb-build      # chunk + embed → artifacts/book-kb/chunks.jsonl
make kb-load       # nạp idempotent vào recommendation_db
make kb-verify     # đối chiếu số chunk, số chương, page range
```

**Kỳ vọng**:
- `kb.corpus`: 1 dòng active, `corpus_key = che-do-an-truong-tho`, `chunk_count` ≈ 900–1.300, `embedding_model = gemini-embedding-001`, `embedding_dim = 768`.
- `kb.chapter`: **13** dòng, `book_page_from/to` khớp `.manifest.json`.
- `kb.chunk`: mọi dòng có `book_page_from <= book_page_to`, `pdf_page_from <= pdf_page_to`, `embedding` không null, `content_tsv` khác rỗng.
- **Idempotency**: chạy `make kb-load` lần hai → `kb.chunk` **không** tăng số dòng.

Kiểm tra cờ chất lượng:
```sql
SELECT count(*) FILTER (WHERE needs_review) AS needs_review,
       count(*) FILTER (WHERE verified)     AS verified,
       count(*)                              AS total
FROM kb.chunk;
```
**Kỳ vọng**: `needs_review` > 0 và ≈ 16,5% corpus; `verified` ≈ 83,5% (xem [data-model.md](./data-model.md) §4).

---

## 3. Gợi ý bữa ăn — lọc dị ứng 100% (US-REC-01)

Gọi `GetMealRecommendations` với hồ sơ dị ứng `đậu phộng`, `sữa` và danh mục ứng viên có cả hai.

**Kỳ vọng**:
- Không món nào trả về có `đậu phộng` hoặc `sữa` trong thành phần.
- Mỗi món có `food_id` thuộc danh mục ứng viên gửi lên (không có món nào bị sinh mới).
- `estimated_calories` bằng đúng giá trị của món tương ứng trong danh mục.
- Số món ≤ **5**.
- Có `disclaimer`.

Kịch bản chạm trần: gửi `consumed_calories = 106` với `target_calories = 100`.
**Kỳ vọng**: `calorie_cap_reached = true`, không có món ăn chính trong `meals`.

---

## 4. Hỏi tri thức sách (US-REC-04)

Gọi `AskLongevityQuestion` với câu hỏi có lời giải đích trong sách.

**Kỳ vọng**:
- `abstained = false`.
- `citations` không rỗng; mỗi citation có `book_page` và `pdf_page`.
- **Mỗi `quote` phải xuất hiện nguyên văn trong `content` của chunk tương ứng** (verify tự động, không tin lời).

Câu hỏi ngoài sách (ví dụ "giá vàng hôm nay là bao nhiêu").
**Kỳ vọng**: `abstained = true`, có `abstain_reason`, `citations` rỗng.

Câu hỏi về thuốc/chẩn đoán.
**Kỳ vọng**: không có khuyến nghị về thuốc/liều/chẩn đoán; câu trả lời hướng về bác sĩ chuyên khoa.

---

## 5. Tra cứu đoạn sách (US-REC-05)

Gọi `SearchLongevityKnowledge` với truy vấn tiếng Việt có dấu, ví dụ "nhịn ăn gián đoạn".
**Kỳ vọng**: trả về các đoạn có `chapter_title`, `book_page`, `pdf_page`, `score` giảm dần; lọc theo chương hoạt động đúng.

---

## 6. Phân tích vi chất & chấm điểm (US-REC-02, US-REC-03)

Gọi `AnalyzeNutrientGaps` với 7 ngày ăn uống thiếu chất xơ.
**Kỳ vọng**: `gaps` có mục chất xơ với `severity` theo ngưỡng 25% / 50%; `target` theo nhóm tuổi và giới.

Gọi `EvaluateLongevityHabits` **hai lần** với cùng đầu vào.
**Kỳ vọng**: `longevity_score` và `score_breakdown` **giống hệt nhau** (tính deterministic).

---

## 7. Đánh giá groundedness (bộ câu hỏi vàng)

```powershell
make kb-eval        # chạy scripts/book-kb/eval.py qua gRPC thật
```
**Kỳ vọng** (theo SC-003, SC-004 trong [spec.md](./spec.md)):
- ≥ 90% câu hỏi có lời giải đích trong sách → trả lời kèm citation, `book_page` trong ±3 trang kỳ vọng.
- 100% câu hỏi ngoài sách → `abstained = true`, không có câu trả lời không citation.
- 100% `quote` khớp nội dung chunk nguồn.

CI **skip** khi thiếu `GOOGLE_AI__API_KEY` và phải log rõ lý do (không được báo pass giả).

---

## 8. Gate kỹ thuật

```powershell
make lint                                        # format-check + build (warnings as errors)
make test PROJECTS=src/Services/Recommendation    # unit + integration scoped
make check                                       # close-out gate, trước khi push
```

---

## 9. Ma trận truy xuất yêu cầu → kiểm chứng

| Yêu cầu (spec) | Kịch bản kiểm chứng |
| --- | --- |
| FR-002 lọc dị ứng 100% | §3 |
| FR-004 rule 105% | §3 (chạm trần) |
| FR-005 dinh dưỡng lấy từ danh mục | §3 |
| FR-007 ≤ 5 món mỗi bữa | §3 |
| FR-009 gap theo nhóm tuổi/giới | §6 |
| FR-010 ngưỡng mức độ 25%/50% | §6 |
| FR-012 ngưỡng điểm tối đa | §6 |
| FR-016 trích dẫn đủ trường | §4 |
| FR-017 verify quote khớp chunk | §4, §7 |
| FR-018 abstain khi thiếu căn cứ | §4 |
| FR-019 cờ chưa đối chiếu | §4 |
| FR-022 kho tri thức đầy đủ | §2 |
| FR-023 nạp lại idempotent | §2 (chạy `kb-load` 2 lần) |
| FR-025 truy xuất kết hợp nghĩa + từ khoá | §5 |
| FR-028 không lưu dữ liệu người dùng | integration test truy vấn thẳng `kb.*` |
| FR-032/FR-033 bệnh nền & thuốc | §4 |
| SC-001..SC-010 | §2–§7 |
