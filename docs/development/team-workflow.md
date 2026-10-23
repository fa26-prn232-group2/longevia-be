# Quy Trình Làm Việc Nhóm với Spec-kit

Tài liệu này mô tả cách 5 người cùng làm trên một repository mà vẫn dùng
Spec-kit, không đụng số feature, không sửa nhầm feature của nhau và không xung
đột ở các file dùng chung.

Đọc kèm: [`agent-workflow.md`](agent-workflow.md) (chu trình Spec-kit) và
[`git-workflow.md`](git-workflow.md) (git & commit).

---

## I. Mô Hình Cốt Lõi

Ba quy tắc đủ để tránh phần lớn xung đột:

1. **1 feature = 1 owner = 1 nhánh.** Không hai người cùng sửa `tasks.md` của
   một feature. Nếu hai người phải làm chung, tách thành hai feature (mỗi người
   một owner) và ghi chú quan hệ đó trong `meta.json` (`description`) của cả hai.
2. **1 service = 1 feature đang mở tại một thời điểm**, nếu feature đó tạo
   migration hoặc đổi `contracts/`. Hai feature khác owner không được cùng lúc
   sửa `DbContext`, migration project, hay cùng một file contract.
3. **`.agents/skills/**` do lead sở hữu.** Nhánh feature MUST NOT sửa skill.
   Thay đổi skill đi qua nhánh `chore/skills-*` và một PR riêng.

## II. Định Danh Feature

```
specs/<owner>-<NNN>-<slug>/     ← thư mục feature
feat/<slug>                     ← nhánh duy nhất của feature
```

- `<owner>`: **handle máy-local**, không nằm trong git. Handle là tên ngắn của
  bạn dùng để đặt tên thư mục feature, ví dụ `duck` → `specs/duck-001-user-auth`;
  chỉ chữ thường, số và gạch ngang, 2–20 ký tự.
- `<NNN>`: số 3 chữ số, **đếm riêng theo owner**. Hai người khác nhau có thể
  cùng `001`; đó là bình thường, không phải xung đột.
- `<slug>`: duy nhất toàn repository, trùng với tên nhánh.

Danh tính lưu ở `.specify/owner.json` — file **gitignored**, mỗi máy tự điền,
không bao giờ commit:

```json
{"handle": "duck"}
```

Điền bằng helper (agent sẽ hỏi handle nếu file còn trống):

```bash
pwsh scripts/speckit/team-feature.ps1 owner -Handle duck
pwsh scripts/speckit/team-feature.ps1 owner            # xem handle hiện tại
```

Mỗi dev dùng một clone riêng: file là state của checkout, hai người dùng chung
một clone sẽ dùng chung danh tính.

Mỗi feature có `meta.json` — nguồn sự thật cho owner/number/slug/branch/status:

```json
{
  "owner": "duck",
  "number": 1,
  "slug": "user-auth",
  "branch": "feat/user-auth",
  "status": "open",
  "services": ["identity"],
  "description": "Email/password authentication",
  "opened_at": "2026-10-22"
}
```

Bảng tổng hợp chỉ là **view**, không phải file trong repo: chạy
`pwsh scripts/speckit/team-feature.ps1 registry` khi cần xem. Nguồn sự thật là
`meta.json` của từng feature — cập nhật `services` và `status` ở đó.

## III. Helper

Mọi thao tác định danh đi qua `scripts/speckit/team-feature.ps1` (không dùng
`create-new-feature.ps1` — script đó đánh số toàn repository):

```bash
# Khai báo / xem handle của máy này (state local, không commit)
pwsh scripts/speckit/team-feature.ps1 owner -Handle duck
pwsh scripts/speckit/team-feature.ps1 owner

# Kiểm tra checkout hiện tại đang bind đúng feature của nhánh
pwsh scripts/speckit/team-feature.ps1 resolve -Json

# Tạo feature mới (bắt buộc đang ở feat/<slug>)
pwsh scripts/speckit/team-feature.ps1 new -Slug user-auth -Description "Email/password auth" -Json

# Bind lại sau khi clone / chuyển nhánh
pwsh scripts/speckit/team-feature.ps1 bind

# Xem bảng feature đang mở (chỉ in ra, không ghi file)
pwsh scripts/speckit/team-feature.ps1 registry

# Kiểm tra toàn vẹn (số trùng, slug trùng, meta.json thiếu, bind lệch)
pwsh scripts/speckit/team-feature.ps1 check
```

Các skill `speckit-*` gọi `resolve` trước khi viết bất kỳ file nào và STOP nếu
nhánh hiện tại không khớp feature đã bind. Đây là lý do agent không bao giờ sửa
spec của người khác sau khi bạn `git checkout`.

## IV. Luồng Làm Việc

1. **Đồng bộ**: `git fetch origin && git switch main && git pull`.
2. **Tạo nhánh**: `git switch -c feat/<slug>` (slug tự đặt, chưa ai dùng).
3. **Tạo feature**: chạy `$speckit-specify`. Nếu chưa có `.specify/owner.json`,
   agent hỏi handle rồi ghi hộ; sau đó gọi helper `new`, sinh
   `specs/<owner>-<NNN>-<slug>/` và ghi `meta.json`.
4. **Khai báo phạm vi**: trong `meta.json`, khai báo `services` mà feature sẽ
   chạm. Nếu cần sửa file dùng chung, ghi rõ ở mục "Shared Files & Conflict
   Hotspots" trong `plan.md` (bước plan).
5. **Chạy các bước Spec-kit** theo [`agent-workflow.md`](agent-workflow.md).
6. **Trước khi mở PR**: `pwsh scripts/speckit/team-feature.ps1 check`, cập nhật
   `status` nếu feature đã đóng, chạy `make check`.
7. **PR**: 1 reviewer bắt buộc; người khác không push hộ. Sau khi merge, người
   tiếp theo merge `main` vào nhánh của mình trước khi tạo migration mới.

## V. File Dùng Chung và Điểm Xung Đột

Các path sau **không thuộc** riêng một feature. Chỉ được sửa khi `plan.md` khai
báo và không feature đang mở nào khác sở hữu:

| Path | Vì sao xung đột | Cách xử lý |
| --- | --- | --- |
| `contracts/**` | nhiều feature cùng đổi một schema | khai báo `services` trong `meta.json`; merge tuần tự; feature sau rebase rồi cập nhật |
| `Directory.Packages.props` | thêm/xoá package dùng chung | gộp vào PR nhỏ riêng nếu có thể |
| `*.slnx` | thêm project | chỉ người tạo project sửa, merge sớm |
| DI/registration (`Program.cs`, `ServiceCollectionExtensions`) | hai feature cùng thêm service | khai báo trong plan; feature sau rebase và thêm lại |
| `Migrations/**` | timestamp + model snapshot | một service một feature đang mở; regenerate sau merge `main` |
| `.specify/owner.json` | danh tính **máy-local** | gitignored, mỗi dev tự điền; không phải file chia sẻ |
| `.agents/skills/**`, `.specify/**` | hạ tầng dùng chung | lead sở hữu, PR `chore/skills-*` (trừ `.specify/owner.json`, state local) |

## VI. Migration

- Một service chỉ có **một** feature đang mở được tạo migration.
- Trước `make ef-add`, merge `origin/main` vào nhánh để chắc timestamp không
  thấp hơn migration mới nhất.
- Không sửa tay model snapshot hay file migration đã áp dụng; nếu lệch, xoá
  migration vừa tạo và sinh lại.
- Sau khi merge, nếu migration của feature khác đã vào `main`, chạy lại
  `make ef-add` trên nhánh của mình rồi kiểm tra diff.

## VII. Skills và Nâng Cấp Spec-kit

- Skill canonical ở `.agents/skills/<name>/SKILL.md`. Đây là **managed files**
  của Spec Kit (trừ `speckit-orchestrate`, `speckit-worker` do repo tự thêm).
- Repo đã có deviation có chủ đích; danh sách ở
  [`spec-kit-customizations.md`](spec-kit-customizations.md).
- `specify integration upgrade` sẽ báo `Modified managed files`. Quy trình:
  xem diff, đối chiếu danh sách deviation, rồi mới `--force`. Không ai tự chạy
  upgrade trên nhánh feature.

## VIII. Checklist Trước PR

- [ ] `pwsh scripts/speckit/team-feature.ps1 check` xanh (không warning mới).
- [ ] `meta.json`: `services` đúng, `status` đúng.
- [ ] `plan.md` liệt kê đủ file dùng chung; không sửa file của feature khác.
- [ ] Không có thay đổi ngoài phạm vi feature trong diff.
- [ ] `make check` xanh.
- [ ] PR có 1 reviewer; mô tả nêu rõ feature id (`owner/NNN/slug`).
