# Changelog

Theo dõi thay đổi đáng kể ở mức dự án. Chi tiết code xem git history.

## [Unreleased]

### Added
- Bộ khung repo: solution `.slnx`, Central Package Management, `Makefile` gate.
- Docker Compose: PostgreSQL (3 DB + role riêng), RabbitMQ, Nginx gateway.
- Hiến pháp dự án + bộ tài liệu system-design/development/api/decisions.
- ADR 002 — Knowledge Base & RAG cho Recommendation Service: `recommendation_db`
  (pgvector) làm knowledge store cho RAG trên tri thức sách *Chế độ ăn trường
  thọ*, kèm trích dẫn chương + trang bản in.

- Recommendation Service (RAG): knowledge store `recommendation_db` (pgvector,
  schema `kb`), pipeline nạp corpus offline `scripts/book-kb`, 5 RPC gRPC
  (gợi ý bữa ăn, thiếu hụt vi chất, điểm thói quen, hỏi sách, tra cứu đoạn) với
  trích dẫn chương + trang bản in + trang scan, và bộ eval 50 câu.
### Changed
- Database: SQL Server → PostgreSQL; chuyển sang EF Core Code First.
- Hiến pháp `1.0.0` → `2.0.0` (MAJOR): nguyên tắc I có **ngoại lệ duy nhất** —
  `Recommendation` sở hữu `recommendation_db` chỉ chứa corpus tri thức đã nhúng,
  **không** chứa dữ liệu người dùng.
- PRD §2.4 và `docs/api/api-spec.md` §5: Recommendation được đặc tả lại theo hướng
  RAG có căn cứ — thêm `citations` + `disclaimer` mọi response, 2 RPC mới
  (`AskLongevityQuestion`, `SearchLongevityKnowledge`), `candidate_foods` do Diet
  gửi kèm, rubric điểm Longevity và gap vi chất tính deterministic.

### Notes
- `docs/overview/*` đang là khung, sẽ điền khi chốt nghiệp vụ.
- Triển khai Recommendation (contract, service, pipeline ingest) chưa bắt đầu;
  sẽ đi qua Spec-kit theo `docs/development/agent-workflow.md`.
