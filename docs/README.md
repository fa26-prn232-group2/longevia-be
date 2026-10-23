# Tài liệu Longevia Backend

## Nguyên tắc tổ chức

- `docs/` chỉ chứa tài liệu viết tay. Sinh tự động (Swagger, report) để nơi khác.
- Ngoại lệ: `docs/reference/` chứa tài liệu tham chiếu được số hoá tự động từ sách
  (không viết tay). Mọi file trong đó mang `reviewed: false` cho tới khi được đối
  chiếu thủ công.
- Tài liệu viết bằng tiếng Việt; thuật ngữ kỹ thuật giữ nguyên tiếng Anh.
- Thứ tự thẩm quyền khi mâu thuẫn: **Constitution > system-design > development > khác**.
- Đường dẫn tham chiếu trong tài liệu dùng dạng tương đối.

## Cấu trúc

```
docs/
  overview/       # Tầm nhìn, roadmap, phạm vi MVP, changelog
  requirements/   # Yêu cầu nghiệp vụ (BRD) và yêu cầu sản phẩm chi tiết (PRD)
  system-design/  # Kiến trúc, design pattern, event-driven, ranh giới service
  development/    # Quy trình, convention, setup, test, migration
  api/            # Quy chuẩn thiết kế API và đặc tả API chi tiết
  decisions/      # Architecture Decision Records (ADR)
  reference/      # Tài liệu tham chiếu số hoá tự động (sách) — reviewed: false
```

## Mục lục

### Yêu cầu & Nghiệp vụ
- [requirements/brd.md](requirements/brd.md) — Business Requirement Document (BRD).
- [requirements/prd.md](requirements/prd.md) — Product Requirement Document (PRD) chi tiết từng service.

### Tổng quan
- [overview/vision.md](overview/vision.md) — tầm nhìn và mục tiêu.
- [overview/roadmap.md](overview/roadmap.md) — lộ trình.
- [overview/mvp-scope.md](overview/mvp-scope.md) — phạm vi MVP.
- [overview/changelog.md](overview/changelog.md) — lịch sử thay đổi.

### Kiến trúc
- [system-design/architecture.md](system-design/architecture.md) — kiến trúc tổng thể.
- [system-design/design-pattern.md](system-design/design-pattern.md) — layering & pattern.
- [system-design/event-driven-design.md](system-design/event-driven-design.md) — RabbitMQ, outbox.
- [system-design/contract-purity-pattern.md](system-design/contract-purity-pattern.md) — ranh giới contract.
- [system-design/microservices/](system-design/microservices/README.md) — chi tiết từng khía cạnh service.

### API & Contracts
- [api/api-spec.md](api/api-spec.md) — Đặc tả API chi tiết cho từng service (REST & gRPC).
- [api/README.md](api/README.md) — Quy chuẩn thiết kế API.

### Phát triển
- [development/agent-workflow.md](development/agent-workflow.md) — quy trình Spec-kit.
- [development/team-workflow.md](development/team-workflow.md) — làm việc nhóm, định danh feature, file dùng chung.
- [development/spec-kit-customizations.md](development/spec-kit-customizations.md) — các tuỳ biến Spec Kit có chủ đích.
- [development/coding-conventions.md](development/coding-conventions.md) — convention C#/.NET.
- [development/code-hygiene.md](development/code-hygiene.md) — ngưỡng cấu trúc code.
- [development/git-workflow.md](development/git-workflow.md) — git & commit.
- [development/local-setup.md](development/local-setup.md) — chạy local bằng Docker.
- [development/testing.md](development/testing.md) — chiến lược test & gate.
- [development/migration.md](development/migration.md) — EF Core Code First migrations.
- [development/api-testing.md](development/api-testing.md) — test API bằng Swagger.

### Khác
- [decisions/README.md](decisions/README.md) — danh mục ADR.
- [reference/che-do-an-truong-tho/00-index.md](reference/che-do-an-truong-tho/00-index.md) — số hoá tự động sách *Chế độ ăn trường thọ* (Markdown theo chương); **chỉ dùng cho mục đích học tập**.
- [../NOTICE.md](../NOTICE.md) — mục đích sử dụng, nội dung bên thứ ba có bản quyền và giấy phép các thành phần.
- [../LICENSE](../LICENSE) — giấy phép MIT cho mã nguồn (không áp dụng cho nội dung sách).
