# Tài liệu Longevia Backend

## Nguyên tắc tổ chức

- `docs/` chỉ chứa tài liệu viết tay. Sinh tự động (Swagger, report) để nơi khác.
- Tài liệu viết bằng tiếng Việt; thuật ngữ kỹ thuật giữ nguyên tiếng Anh.
- Thứ tự thẩm quyền khi mâu thuẫn: **Constitution > system-design > development > khác**.
- Đường dẫn tham chiếu trong tài liệu dùng dạng tương đối.

## Cấu trúc

```
docs/
  overview/       # Tầm nhìn, roadmap, phạm vi MVP, changelog
  system-design/  # Kiến trúc, design pattern, event-driven, ranh giới service
  development/    # Quy trình, convention, setup, test, migration
  api/            # Quy chuẩn thiết kế API
  decisions/      # Architecture Decision Records (ADR)
```

## Mục lục

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

### Phát triển
- [development/agent-workflow.md](development/agent-workflow.md) — quy trình Spec-kit.
- [development/coding-conventions.md](development/coding-conventions.md) — convention C#/.NET.
- [development/code-hygiene.md](development/code-hygiene.md) — ngưỡng cấu trúc code.
- [development/git-workflow.md](development/git-workflow.md) — git & commit.
- [development/local-setup.md](development/local-setup.md) — chạy local bằng Docker.
- [development/testing.md](development/testing.md) — chiến lược test & gate.
- [development/migration.md](development/migration.md) — EF Core Code First migrations.
- [development/api-testing.md](development/api-testing.md) — test API bằng Swagger.

### Khác
- [api/README.md](api/README.md) — quy chuẩn API.
- [decisions/README.md](decisions/README.md) — danh mục ADR.
