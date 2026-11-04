# Architecture Decision Records (ADR)

Mỗi file ghi một quyết định kiến trúc/vận hành đã chốt, kèm bối cảnh và hệ quả.
Đọc [template.md](template.md) trước khi viết ADR mới.

## Index

| # | Ngày | Tiêu đề | Trạng thái |
| --- | --- | --- | --- |
| [001](001-authn-authz-architecture.md) | 2026-10-07 | AuthN/AuthZ Architecture | Accepted |
| [002](002-recommendation-knowledge-base.md) | 2026-10-22 | Knowledge Base & RAG cho Recommendation Service | Accepted |
| [003](003-diet-to-progress-weight-grpc.md) | 2026-10-30 | Giao tiếp gRPC đồng bộ Diet → Progress truy vấn cân nặng gần nhất | Accepted |
| [004](004-diet-to-identity-profile-grpc.md) | 2026-11-02 | Diet → Identity profile gRPC | Accepted |
| [005](005-mvp-utc-business-calendar.md) | 2026-11-04 | Lịch nghiệp vụ UTC cho MVP — Q01 | Accepted |

## Quy ước

- **Tên file**: `NNN-ten-kebab.md` — `NNN` là 3 chữ số, cấp tăng dần.
- **Tiêu đề**: dòng đầu là `# ADR NNN - Tiêu đề`.
- **Metadata bắt buộc**: `Status`, `Decision Date` (YYYY-MM-DD), `Decision Maker`.
  Tùy chọn: `Supersedes`, `Superseded by`.
- **Status**: `Proposed` / `Accepted` / `Superseded by ADR NNN` / `Deprecated` / `Rejected`.
- **Số ADR bất biến**: cấp tăng dần, không tái sử dụng/đánh số lại. Quyết định bị
  thay thế thì viết ADR mới và thêm `Superseded by`; không sửa nội dung quyết định cũ
  (đính chính thì ghi vào mục "Bổ sung").
- **Ngôn ngữ**: heading giữ tiếng Anh (`Context` / `Decision` / `Consequences`),
  thân viết tiếng Việt.
