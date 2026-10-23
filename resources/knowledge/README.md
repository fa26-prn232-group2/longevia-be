# Seed tri thức đã nhúng

Thư mục này chứa **seed** của kho tri thức: đầu ra của pipeline
[`scripts/book-kb`](../../../scripts/book-kb) đã được nén lại và commit vào repo,
để môi trường mới nạp được tri thức **mà không cần gọi Google AI và không cần API key**.

- Nguồn: [`docs/reference/che-do-an-truong-tho/`](../../../docs/reference/che-do-an-truong-tho/README.md)
- Chunk + embedding do `make kb-build` sinh ra trong `artifacts/book-kb/`
- Seed do `make kb-seed` xuất từ artifact đó

## Nội dung

| File | Ý nghĩa |
| --- | --- |
| `che-do-an-truong-tho/build.json` | metadata: `corpus_key`, `version` (hash corpus), `embedding_model`, `embedding_dim`, `chunk_count`, tham số chunking |
| `che-do-an-truong-tho/chunks.jsonl.gz` | 188 chunk kèm embedding 768 chiều (đã L2-normalize), trang bản in và trang scan |
| `che-do-an-truong-tho/page_flags.jsonl.gz` | 67 cờ chất lượng theo trang (`manual_verified`, `table_verified`, `missing_numbers`…) |

Hiện tại: 13 chương, 188 chunk, model `gemini-embedding-001`, 768 chiều.

## Cách dùng

```powershell
# Nạp seed vào recommendation_db (idempotent, không cần key)
docker compose --env-file .env -f deployments/docker-compose.yml up -d   # service kb-init tự nạp
make kb-ensure                                                          # hoặc nạp tay
```

## Khi nào phải xuất lại seed

| Thay đổi | Việc cần làm |
| --- | --- |
| Sửa nội dung Markdown của sách | `make kb-build` → `make kb-seed` → commit seed mới |
| Đổi `RAG__EMBEDDING_MODEL` hoặc `RAG__EMBEDDING_DIM` | `make kb-build` → `make kb-seed`; **đồng thời** cập nhật cột `vector(n)` bằng migration |
| Đổi `RAG__CHUNK_TOKENS` / `RAG__CHUNK_OVERLAP` | `make kb-build` → `make kb-seed` |

`make kb-build` cần `GOOGLE_AI__API_KEY` (cache embedding nằm ở `artifacts/book-kb/cache/`
nên chạy lại không tốn thêm chi phí cho phần đã nhúng).

## Cảnh báo

- **Không sửa tay** các file trong thư mục này — chúng là dữ liệu sinh tự động.
- Seed phải khớp `embedding_dim` với cột `vector(n)` trong `kb.chunk`; lệch số chiều
  sẽ làm `kb-init` dừng với lỗi rõ ràng (không ghi dữ liệu sai).
- Nội dung là **dữ liệu phái sinh từ sách có bản quyền**, chỉ dùng cho mục đích học tập;
  xem [`NOTICE.md`](../../../NOTICE.md) mục 2 và mục 3.
