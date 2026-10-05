# Git Workflow

## 1. Chính sách dùng git cho agent

- **Không tự ý thao tác git**: agent MUST NOT chạy `commit`, `push`, `tag`,
  `branch`, `checkout`, `merge`, `rebase`, `reset`, `amend` khi chưa có yêu cầu/cho
  phép rõ ràng của dev. Chỉ được đọc (`git status/log/diff`).
- Ngoại lệ: gọi một skill tự khai báo git policy (ví dụ `@speckit-orchestrate`) là
  cho phép thường trực cho lần chạy đó.
- Commit message: **tiếng Anh**, theo Conventional Commits.

## 2. Branch

- Feature: `feat/<name>`, sửa lỗi: `fix/<name>`, việc lặt vặt: `chore/<name>`.
- Feature Spec-kit: dùng tên `###-feature-name` nếu workflow yêu cầu.

## 3. Commit

- Commit đủ lớn để có nghĩa: gộp code + test + doc liên quan thành một commit.
- Không commit rác (typo một dòng, tick checkbox lẻ).
- Gate trước commit chọn theo độ lớn thay đổi — xem bảng trong
  [`AGENTS.md`](../../AGENTS.md) §2. Không chép lại gate ở đây.

Ví dụ:
```
feat(identity): add email/password registration
fix(diet): reject empty meal list
docs(architecture): clarify recommendation is internal
```

## 4. Tag & push tag (CI/CD)

- Chỉ tạo tag trên `main` sau khi PR đã merge và CI xanh.
- Tag semver: `vMAJOR.MINOR.PATCH` (pre-release: `vX.Y.Z-rc.1`).
- Push tag trigger workflow deploy tương ứng; không tag từ nhánh feature.
