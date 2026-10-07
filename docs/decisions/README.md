# Architecture Decision Records (ADR)

Mỗi file ghi một quyết định kiến trúc/vận hành đã chốt, kèm bối cảnh và hệ quả.
Đọc [template.md](template.md) trước khi viết ADR mới.

## Index

| # | Ngày | Tiêu đề | Trạng thái |
| --- | --- | --- | --- |
| [001](001-authn-authz-architecture.md) | 2026-10-07 | AuthN/AuthZ Architecture | Accepted |

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
