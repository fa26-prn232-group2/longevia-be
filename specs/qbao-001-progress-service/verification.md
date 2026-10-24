# Báo cáo kiểm chứng Phase 1 và corrective PR #9

## Corrective — đối chiếu 2026-10-24/25 (Asia/Saigon)

PR #9 vẫn OPEN/non-draft, head trước corrective
`70d4faa2cf9d527333f0f9431ff0eec0de99dfb3`; local và remote head trùng, không có
commit mới hoặc review/comment trên GitHub tại lần kiểm tra. origin/main và PR base
`418d41b82c4eca846995ed3d505736f0ba7b0c5e`; PR #8 MERGED lúc
2026-10-24 16:35:19 UTC. Không merge/rebase base vào feature; đọc Diet bằng
git show origin/main:<path>, nên không nhận source Diet là chưa tồn tại trên remote.

Đã đối chiếu MealLog/MealUseCases/LogMealRequest/MealEndpoints/DailySummaryUseCase,
TargetResponse, Diet deferred/backend-integration-guide và system-design diet-service
ở SHA base trên. Diet đã có catalog, manual targets, create meal, daily summary,
EF diet_db, auth/tests/Docker; còn thiếu meal event/outbox/projection/bootstrap/E2E.
Q01/Q09 phân biệt DateOnly client business date, CreatedAt UTC và processed timestamp;
update/delete/change-date là future lifecycle. Q10 ghi dependency Diet cần cân nặng,
T038 chỉ theo dõi approval, BMR/TDEE vẫn thuộc Diet. T002/T003 không chờ Q01/Q08;
security acceptance cần Q08, date logic cần Q01. Không approve Q01–Q10.

### Evidence theo nguồn — không trộn số đếm

| Nguồn / SHA thực chạy | Thời điểm và URL | Kết quả / giới hạn |
| --- | --- | --- |
| Local Phase 1 trước PR #8; source base 6d3f7550a531b6a3a02cbd327361648d34a61968, sau đó commit thành 70d4faa2 | 2026-10-24 Asia/Saigon; log local, chi tiết phần lịch sử dưới | 510 pass/0 fail/0 skip, 15 project không vulnerable. Không bao gồm Diet. Giữ nguyên lịch sử. |
| CI push head 70d4faa2cf9d527333f0f9431ff0eec0de99dfb3 | 2026-10-24 16:36:36–16:38:54 UTC; [run 40112349086](https://github.com/fa26-prn232-group2/longevia-be/actions/runs/40112349086) | SUCCESS; log thực tế 510 pass/0 fail/0 skip, 15 project không vulnerable. Không phải 537/21. |
| CI PR gắn head 70d4faa2, checkout merge ref 58a4f8010914d942384ffc3cf718790733074ce0 vào base 418d41b8 | 2026-10-24 16:36:44–16:39:29 UTC; [run 40112359186](https://github.com/fa26-prn232-group2/longevia-be/actions/runs/40112359186) | SUCCESS; 537 pass/0 fail/0 skip; Diet Unit=11, Integration=16 thêm vào 510. Release 0 warning/error, 21 project không vulnerable. Chỉ chứng minh merge ref cũ, không corrective head tương lai. |
| Local corrective; source HEAD 70d4faa2, docs corrective chưa commit | 2026-10-24/25 Asia/Saigon; log máy-local progress-corrective-gates/tests/vuln | Đã chạy lại recipe Makefile: format exit 0; Debug/Release warnings-as-errors 0 warning/error; full test -c Release -m:1 510 pass/0 fail/0 skip (5 assemblies), 15 project không vulnerable, exit 0. Chỉ branch chưa merge Diet; không claim local 537. |

Các log CI trên đã tải và đối chiếu trực tiếp, không chỉ dựa trạng thái check.
Make chưa có nên dùng đúng dotnet recipes (thêm -m:1 để giới hạn MSBuild, không lọc test).
Local gates chạy source không thay đổi; chỉ docs feature trong corrective. New-head
CI phải kiểm tra sau push và báo riêng trong bàn giao/PR, không dùng CI cũ thay thế.

Kiểm tra corrective trước commit: resolve đúng qbao/001/progress-service; team check
5 feature consistent trên checkout; 12 artifact UTF-8/LF/JSON/whitespace hợp lệ,
56 relative Markdown link không hỏng; 38 task unique, 0 tick, 0 unmapped,
FR 19/19 và SC 7/7 có task. Chỉ 11 Markdown file trong feature thay đổi;
meta.json giữ nguyên; không source/canonical contracts/migration hoặc feature khác.

## Lịch sử Phase 1 trước corrective (2026-10-24)

Ngày: 2026-10-24, múi giờ Asia/Saigon. Báo cáo kiểm chứng trước commit/push/PR;
trạng thái bàn giao cuối cùng chỉ xác nhận sau khi xuất bản PR thành công.

## Trạng thái repository và phạm vi

- Repository: [fa26-prn232-group2/longevia-be](https://github.com/fa26-prn232-group2/longevia-be).
- Remote HEAD, origin/main sau fetch và base local HEAD:
  `6d3f7550a531b6a3a02cbd327361648d34a61968`; ls-remote cuối lượt vẫn cùng SHA.
- Branch có sẵn: `feat/progress-service`; chưa có remote branch cùng tên tại lúc kiểm tra.
- Feature helper cấp `qbao-001-progress-service`, owner user chọn `qbao`, services `["Progress"]`,
  status `open` vì feature triển khai chưa đóng. Không đoán số/handle, không tạo branch/spec trùng.
- Working tree trước làm việc sạch. Sau kiểm chứng chỉ còn feature directory mới chưa track;
  tracked source/contracts/deployments không có diff. Owner/binding local được gitignore.
- Tại thời điểm ghi báo cáo trước commit, chưa có SHA/PR number/URL mới.
  GitHub CLI đã xác nhận đăng nhập tài khoản qbao0111 có quyền repo.
  SHA, push, URL và CI/review status được báo trong kết quả bàn giao sau xuất bản.

## Artifacts đã tạo

1. [meta.json](meta.json) — metadata helper, services và mô tả Phase 1.
2. [spec.md](spec.md) — đủ 4 story, 19 FR, 7 SC, AC/edge/ownership/validation/test/dependencies.
3. [plan.md](plan.md) — kiến trúc, Constitution Check, roadmap và shared hotspots.
4. [research.md](research.md) — hiện 10 câu hỏi PENDING; ban đầu 9, corrective bổ sung Q10.
5. [data-model.md](data-model.md) — snapshot, invariants, indices/state/projection có điều kiện.
6. [contracts/README.md](contracts/README.md) — 9 REST routes và proposal height/meal/digest, không canonicalization.
7. [tasks.md](tasks.md) — hiện 38 task chưa tick; ban đầu 37, corrective thêm T038 tracking Q10.
8. [quickstart.md](quickstart.md) — cách verify Phase 1 và kịch bản Phase 2–6 chưa chạy.
9. [deferred.md](deferred.md) — hiện 6 nhóm ngoài scope, gồm implementation provider Q10 sau duyệt.
10. [checklists/requirements.md](checklists/requirements.md) — 13/16; 3 mục readiness vẫn chưa đạt.
11. [pr-description.md](pr-description.md) — nội dung đồng bộ với PR #9 khi corrective xuất bản.
12. Báo cáo verification này.

Spec-kit thực hiện bằng đọc và làm theo canonical SKILL.md, không giả gọi một CLI
`speckit-*` không tồn tại. Specify dùng helper new/resolve; clarify hỏi developer,
ghi hai câu trả lời; plan dùng setup-plan và research agent chỉ đọc; tasks dùng
setup-tasks; analyze dùng prerequisites RequireSpec/RequireTasks/IncludeTasks.
Không có `.specify/extensions.yml`, không có extension hook phải dispatch.
User trực tiếp cho phép giữ policy/contract PENDING để review nên plan là có điều kiện;
không claim đã thỏa quy tắc skill plan “không unresolved clarification” để implement.

## Kết quả lệnh thực tế

Các lệnh shell được chạy qua `rtk proxy`; bảng ghi lệnh gốc để dễ tái hiện.
Môi trường .NET SDK `10.0.203`; `make` không có nên dùng recipe tương đương.
Ban đầu không có Docker. Sau khi developer yêu cầu cài và chọn ổ E, đã cài
Docker Desktop `4.96.3`, CLI/Engine `29.9.2`, Compose `v5.6.1` ở ổ E.
Installer tải từ Docker chính thức, SHA-256 khớp metadata winget và chữ ký Docker Inc hợp lệ.
Image/container data, distro disk và swap WSL đặt ở ổ E; giới hạn WSL 4 GB RAM,
2 GB swap để giảm áp lực tài nguyên. Đây là cấu hình máy-local, không commit vào feature.
`hello-world` đã chạy thật thành công. Kho image containerd gặp image có file rỗng/
exec format error sau các lần WSL ngắt; đã chuyển cấu hình máy-local sang `overlay2`,
tải lại image và kiểm chứng `postgres` user trong cả PostgreSQL/pgvector image.

| Kiểm tra | Kết quả thực tế |
| --- | --- |
| git fetch origin; git ls-remote origin HEAD refs/heads/main refs/heads/feat/progress-service | Thành công; base SHA ở trên, remote progress chưa tồn tại. |
| team-feature owner -Handle qbao; new; resolve -Json | Thành công, đúng qbao/001/progress-service/feat branch. |
| pwsh scripts/speckit/team-feature.ps1 check | Exit 0: Features: 5, Owner: qbao, layout consistent. |
| check-prerequisites -Json -RequireSpec -RequireTasks -IncludeTasks | Exit 0; đủ spec/plan/tasks và supporting artifacts. |
| git diff --check | Exit 0; vì feature mới còn untracked, đã kiểm tra whitespace của toàn bộ file mới riêng, không chỉ dựa lệnh này. |
| Kiểm tra toàn bộ file mới | 12 file UTF-8/LF, newline cuối file, JSON hợp lệ, không trailing whitespace. 57 relative Markdown links kiểm tra, 0 link hỏng; 37 task ID unique, 0 task triển khai đã tick. |
| make lint; make check | Không chạy được: make không được nhận diện. Dùng recipe đúng Makefile dưới đây. |
| dotnet format longevia-be.slnx --verify-no-changes | Ban đầu Exit 1 vì checkout CRLF; sau chuẩn hóa máy-local về LF đã chạy lại Exit 0, không đổi logic/source blob. |
| dotnet build longevia-be.slnx --no-restore -warnaserror -m:1 | Lượt cuối Exit 0; Debug build, 0 warning, 0 error; 12.99 giây. |
| dotnet build longevia-be.slnx -c Release --no-restore -warnaserror -m:1 | Lượt cuối Exit 0; Release build, 0 warning, 0 error; 2.85 giây. |
| dotnet test longevia-be.slnx -c Release -m:1 | Lượt cuối Exit 0, đầy đủ 5 assemblies: 510 pass, 0 fail, 0 skip, 70 integration test chạy trên Docker thật. -m:1 chỉ giới hạn tiến trình MSBuild, không lọc/bỏ test. |
| dotnet list longevia-be.slnx package --vulnerable --include-transitive | Exit 0; 15 project báo không có package vulnerable theo nguồn NuGet hiện tại. |
| Scoped Progress test | Không áp dụng Phase 1: chưa có source/test project Progress, không giả chạy project chưa tồn tại. |

### Số test theo assembly

| Assembly | Pass | Fail | Skip |
| --- | --- | --- | --- |
| Worker.UnitTests | 16 | 0 | 0 |
| Recommendation.UnitTests | 372 | 0 | 0 |
| Identity.UnitTests | 52 | 0 | 0 |
| Recommendation.IntegrationTests | 64 | 0 | 0 |
| Identity.IntegrationTests | 6 | 0 | 0 |
| Tổng | 510 | 0 | 0 |

Log lượt xanh lưu máy-local với tên `longevia-progress-phase1-tests-overlay2.log`,
không commit log/generated output. Identity integration 6/6 (1 phút 6 giây),
Recommendation integration 64/64 (35 giây). Lượt cũ trước cài Docker 440/510;
hai lượt chẩn đoán sau cài bị dừng do WSL ngắt/image lỗi, không tính là gate xanh.
`git ls-files --eol` xác nhận ban đầu source có index LF, working tree CRLF;
`core.autocrlf=true`, `.editorconfig` yêu cầu LF. Theo yêu cầu xử lý môi trường của
developer, đã chuẩn hóa checkout 343 file source/project về LF, không đổi nội dung
logic; `git diff --quiet` exit 0 và staged diff rỗng. Format recheck exit 0.
Đã đối chiếu hash raw của từng file với blob trong index rồi làm mới stat cache
bằng chính mode/blob cũ; staged diff vẫn rỗng, working tree chỉ còn feature mới.
Không stage nội dung source hoặc đổi file Git attributes của repository.
Ba launchSettings.json untracked được công cụ .NET sinh trong lúc gate đã được
kiểm tra rồi dọn đúng ba file mới và thư mục rỗng; không xóa file có trước phiên.

## Phân tích nhất quán Spec-kit sau corrective (chỉ đọc)

Phạm vi spec/plan/tasks + Constitution và tài liệu chuẩn. Báo cáo được ghi lại sau
khi kết thúc pass phân tích chỉ đọc; không sửa artifacts để che các câu hỏi PENDING.

| ID | Loại | Mức | Vị trí | Kết luận / khuyến nghị |
| --- | --- | --- | --- | --- |
| U01 | Thiếu contract, đã nhận diện | HIGH trước implement | spec US1/FR-003/FR-018; research Q02; tasks T022–T027 | Height RPC chưa tồn tại; source int/API decimal. Chờ Nguyên/lead approve canonical contract + ADR, không implement từ proposal. |
| U02 | Thiếu contract, đã nhận diện | HIGH trước implement | spec US4/FR-014/FR-016; research Q09; tasks T028–T032 | Diet create-only đã có; thiếu meal contract/outbox/bootstrap/completeness và Progress projection/E2E. Future lifecycle chưa triển khai. Giữ full streak blocked. |
| U03 | Policy chưa duyệt | HIGH trước implement | research Q01/Q03–Q08; spec các AC có điều kiện; tasks T001/T009/T015/T022/T028 | Day/timezone, Custom, BMI bounds/category, water/sleep, dashboard windows/null, pagination và auth/error shape chưa chốt. Developer yêu cầu review proposal trước, không claim implementation-ready. |
| U04 | Dependency ngược, đã ghi nhận | HIGH trước provider implementation riêng | research Q10; T038; deferred DFR-06 | Diet cần weight contract/provenance và ADR direction, không chuyển BMR/TDEE sang Progress. PENDING APPROVAL; không chặn core skeleton/BMI. |

Không có vi phạm Constitution do thiết kế đề xuất truy cập DB/service sai ranh giới;
không có requirement mất task, task mồ côi hoặc task code đã tick. HIGH trên là
blocker implementation được minh bạch theo scope planning, không phải đã được giải
quyết. Gates repository còn đỏ là blocker commit/push riêng, không gộp thành pass tài liệu.

### Ma trận coverage

| Requirement | Task IDs | Ghi chú |
| --- | --- | --- |
| FR-001 | T001,T004,T005,T013,T016,T019,T023,T026,T029,T032,T033 | Ownership xuyên các story. |
| FR-002 | T022,T023,T024,T026,T036 | Bounds một phần chờ Q04. |
| FR-003 | T022,T023,T024,T025,T026,T033,T037 | BLOCKED Q02. |
| FR-004 | T022,T023,T024,T026 | BLOCKED Q04. |
| FR-005 | T022,T023,T026,T036 | Filter/wire Q01/Q07. |
| FR-006 | T009,T010,T011,T013,T036 | Q03. |
| FR-007 | T009,T010,T012,T013,T033 | Concurrency/retry. |
| FR-008 | T009,T010,T011,T013,T036 | Duration/goal. |
| FR-009 | T009,T010,T013,T036 | Current DTO Q03. |
| FR-010 | T015,T016,T017,T018,T033,T036 | Q01/Q05. |
| FR-011 | T015,T016,T018,T036 | Target Q05. |
| FR-012 | T015,T019,T020,T033,T036 | Sleep Q05. |
| FR-013 | T028,T029,T032,T036 | Baseline/window Q06. |
| FR-014 | T028,T029,T030,T031,T032,T033 | Full streak Q09. |
| FR-015 | T028,T029,T032 | Week/mean Q06. |
| FR-016 | T028,T029,T030,T031,T033,T037 | Lifecycle/replay Q09. |
| FR-017 | T001,T005,T015,T016,T019,T021,T023,T026,T029 | Q01 source zone pending. |
| FR-018 | T022,T023,T025,T028,T029,T031,T032,T037 | Missing-data behavior. |
| FR-019 | T001,T002,T006,T008,T035,T038 | Scope/ownership, Q10 tracking. |
| SC-001 | T002,T008,T035,T038 | Artifact/task coverage. |
| SC-002 | T010,T012,T014 | 20 start, 1 active. |
| SC-003 | T003,T004,T010,T014,T016,T019,T021,T023,T027,T029,T035 | Ownership/invalid. |
| SC-004 | T010,T014,T016,T019,T021,T023,T027,T029,T033,T035 | Bộ dữ liệu cụ thể. |
| SC-005 | T023,T027,T029,T031,T035 | Snapshot/dedup. |
| SC-006 | T007,T034,T035 | Observability/load. |
| SC-007 | T003,T007,T033,T035 | E2E thật/negative control. |

Metrics: 19 FR và 7 SC; FR coverage 19/19 (100%), SC coverage 7/7 (100%);
38 task unique, 0 task hoàn thành, 0 task unmapped, 0 duplicate requirement phát hiện,
0 CRITICAL design violations; 3 nhóm HIGH trước core implement + Q10 trước provider
feature riêng; 10 quyết định PENDING. Dependency graph không có vòng; skeleton
T002→T003 độc lập T001; Q08 gate T004–T008, Q01 gate date logic, T038 độc lập core.
Task breakdown: foundation 8, US1 7, US2 6, US3 7, US4 10.
Coverage không đồng nghĩa correctness runtime hoặc đã được duyệt contract.

## Trạng thái nghiệm thu Phase 1 và bước tiếp theo

Đủ artifacts planning, owner/branch/layout và scope boundaries. Các policy/contract
unresolved được minh bạch theo lựa chọn developer. Không có business code/migration,
không sửa feature người khác. Các bước xuất bản sau khi gate cuối đã xanh:

1. Review corrective diff chỉ feature, stage exact paths, commit message:
   `docs(progress): reconcile Phase 1 plan with merged Diet service`.
2. Push `origin/feat/progress-service`, cập nhật mô tả PR #9 hiện có (không tạo PR mới);
   xác nhận head/base mới, CI phải gắn new HEAD; reviewer chỉ khi danh tính đã xác nhận.
3. Báo SHA/URL và trạng thái CI thực tế rồi dừng chờ human review;
   không merge, không bắt đầu Phase 2. CI cũ chỉ là historical evidence.

Không bỏ qua gate đỏ hoặc coi các lượt test bị dừng là thành công.
Rủi ro còn: Q01–Q10 chưa approved, chưa runtime Progress, integration
contracts còn thiếu, benchmark/E2E feature chưa chạy (có task cho phase sau).
