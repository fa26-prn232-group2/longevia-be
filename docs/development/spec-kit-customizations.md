# Spec-kit Customizations

Repo dùng Spec Kit nhưng đã tuỳ biến có chủ đích cho quy trình nhóm và cho các
quy tắc trong `AGENTS.md`. Tài liệu này liệt kê mọi chỗ lệch so với bản phát
hành, để lần nâng cấp sau không vô tình xoá chúng.

## 1. Managed files đã sửa có chủ đích

`.specify/integrations/codex.manifest.json` quản lý hash của các file dưới đây.
Ba file đang ở trạng thái `MODIFIED` — **không phải lỗi**, không re-vendor:

| File | Sửa gì | Vì sao |
| --- | --- | --- |
| `.agents/skills/speckit-implement/SKILL.md` | `Worker Git guard`, trạng thái `frontend-guide.md`, quy tắc `deferred.md`, gate checklist | orchestrator/worker phân quyền git; frontend handoff có trạng thái trung thực |
| `.agents/skills/speckit-converge/SKILL.md` | `Worker Git guard`, quy tắc deferred, sync frontend guide | không cho worker commit; converge phải phân loại đúng phạm vi |
| `.agents/skills/speckit-tasks/SKILL.md` | `Worker Git guard`, task frontend handoff, deferred | task sinh ra phải khớp quy tắc bàn giao frontend của repo |

Các file managed còn lại giữ nguyên bản phát hành.

## 2. File không do Spec Kit quản lý (repo tự thêm)

| File | Vai trò |
| --- | --- |
| `.agents/skills/speckit-orchestrate/SKILL.md` | điều phối analyze → implement → converge, git/gate policy |
| `.agents/skills/speckit-worker/SKILL.md` | worker chạy đúng một skill/phase, không git |
| `.agents/skills/*/SKILL.md` (block "Feature Binding") | buộc mọi skill xác thực feature theo nhánh trước khi ghi |
| `scripts/speckit/team-feature.ps1` | định danh feature nhóm: `owner/resolve/new/bind/registry/check` |
| `.specify/templates/overrides/plan-template.md` | thêm mục "Shared Files & Conflict Hotspots" |
| `.specify/owner.json` | handle máy-local của dev (gitignored ở root `.gitignore`, không commit) |

Override template dùng đúng cơ chế có sẵn (`Resolve-TemplateContent` ưu tiên
`.specify/templates/overrides/`), nên không cần sửa template managed.

## 3. Đánh số feature

`.specify/init-options.json` vẫn ghi `"feature_numbering": "sequential"`, nhưng
skill `speckit-specify` **không** dùng nó: số được cấp bởi
`scripts/speckit/team-feature.ps1` theo từng owner. `create-new-feature.ps1`
(managed) vẫn đánh số toàn repository và không tương thích với tên thư mục mới,
nên không được gọi.

## 4. Quy trình nâng cấp

1. Đọc diff của bản mới cho các file ở mục 1 và 2.
2. Đối chiếu với bảng trên; chép lại phần tuỳ biến nếu bản mới ghi đè.
3. Chạy `specify integration upgrade` (lần đầu sẽ bị chặn vì modified managed
   files). Chỉ dùng `--force` sau khi đã đọc diff.
4. Chạy lại `pwsh scripts/speckit/team-feature.ps1 check` và `make check`.
5. Cập nhật lại bảng hash ở mục 1 (trạng thái mới sau nâng cấp).

Không chạy upgrade trên nhánh feature; dùng nhánh `chore/skills-*`.
