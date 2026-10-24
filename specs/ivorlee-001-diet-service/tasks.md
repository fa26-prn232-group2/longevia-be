# Công việc Diet độc lập

- [x] Đọc quy định, xác thực owner/nhánh và chốt phạm vi với developer.
- [x] Plan, Constitution Check và Shared Files & Conflict Hotspots.
- [x] Sáu project; test đỏ tính khẩu phần/validation.
- [x] Domain, repository, EF migration và seed idempotent.
- [x] Catalog, target nhập tay, meal log và daily summary.
- [x] Auth, Swagger, validation/errors và observability.
- [x] Integration PostgreSQL: isolation, atomicity, summary, auth.
- [x] Docker/Nginx smoke; Mailpit/Grafana/Prometheus local.
- [x] Gate và hướng dẫn bàn giao với evidence/sync counts.

Có đổi HTTP surface: cập nhật frontend-guide.md. Không đổi gRPC/event surface
trong giai đoạn này; backend-integration-guide.md ghi phần phụ thuộc còn thiếu.

Bằng chứng: [evidence.md](evidence.md). Checklist này chỉ đóng phạm vi độc lập
đã được developer chọn. Feature vẫn open vì [phần hoãn](deferred.md) còn thiếu.
