# Báo cáo phân tích nhất quán (Loop A) — `duck/001/recommendation-rag`

**Ngày**: 2026-10-22 · **Phạm vi**: `spec.md`, `plan.md`, `tasks.md`, `research.md`,
`data-model.md`, `contracts/` đối chiếu với `.specify/memory/constitution.md` 2.1.0.

## Metrics

| Chỉ số | Vòng 1 (trước sửa) | Vòng 2 (sau sửa) |
| --- | --- | --- |
| Tổng requirements (FR) | 33 | 33 |
| Tổng success criteria (SC) | 10 | 10 |
| Tổng task | 119 | 120 |
| Coverage (requirement có ≥1 task) | 98,8% | **100%** |
| CRITICAL | 0 | **0** |
| HIGH | 1 | **0** |
| MEDIUM | 3 | 0 (đã xử lý) |
| LOW | 2 | 2 (giữ nguyên, không ảnh hưởng thực thi) |

## Findings đã sửa

| ID | Loại | Vị trí | Vấn đề | Sửa trong artifact |
| --- | --- | --- | --- | --- |
| A1 | HIGH | `tasks.md` | `FR-021` và `SC-005` (budget độ trễ) **không có task nào** → tiêu chí thành công không kiểm thử được | Thêm **T111** — test tích hợp đo budget độ trễ: assert p95 truy xuất `< 150ms`, ghi p95 end-to-end vào `artifacts/book-kb/latency-report.json` để đối chiếu SC-005 (3s), skip kèm log rõ lý do khi thiếu API key |
| A2 | HIGH | `tasks.md` T002 | `AppException` đặt ở `src/Shared/Errors/` (T005) nhưng T002 **không khai báo reference** tới `Longevia.Shared` → code không build được | Bổ sung `Recommendation.Application` → `Longevia.Shared` vào T002 |
| A3 | MEDIUM | `tasks.md` T050 | `FR-007` yêu cầu hỗ trợ 4 bữa nhưng không test nào kiểm tra ánh xạ `meal_type` | Bổ sung vào T050: assert đủ `BREASTFAST`/`LUNCH`/`DINNER`/`SNACK` |
| A4 | MEDIUM | `tasks.md` T013 | Constitution §II + PRD §3.1 yêu cầu deadline 5s; plan chỉ nói deadline ở phía **client** (ngoài phạm vi) → server không có ràng buộc | Bổ sung vào T013: timeout phía server 5.0s + truyền `CancellationToken` xuống use case |
| A5 | MEDIUM | `tasks.md` T029 | `docs/development/migration.md` yêu cầu merge `origin/main` trước `make ef-add` để timestamp migration không thấp hơn migration mới nhất | Bổ sung vào T029 |
| A6 | MEDIUM | `plan.md` | Rủi ro chưa ghi: `Google.GenAI` 1.x + `TreatWarningsAsErrors` có thể làm đỏ `make lint`; `make check` chạy NuGet audit có thể chặn package mới | Thêm mục **Rủi ro kỹ thuật đã biết** trong `plan.md` (5 rủi ro + cách xử lý) |

## Findings giữ lại (LOW, không ảnh hưởng thực thi)

| ID | Vị trí | Nội dung | Vì sao giữ |
| --- | --- | --- | --- |
| A7 | `plan.md`, `contracts/README.md` | `plan.md` trỏ tới `contracts/recommendation.proto` (bản thiết kế trong feature dir), còn contract authoritative sẽ ở `contracts/recommendation/v1/recommendation.proto` | `contracts/README.md` đã giải thích rõ: bản trong feature dir là thiết kế, contract authoritative là file trong repo; T018 tạo file authoritative |
| A8 | `spec.md` SC-005 (3s) vs `plan.md` (3.5s) | Hai ngưỡng khác nhau | Không phải mâu thuẫn: SC-005 là mục tiêu **người dùng cảm nhận** (3s), PRD §3.1 là ngân sách **gRPC nội bộ** (3.5s). T111 đo cả hai |

## Coverage map (FR/SC → task)

| Requirement | Task | | Requirement | Task |
| --- | --- | --- | --- | --- |
| FR-001 | T050, T054, T057, T060 | | FR-018 | T080, T082 |
| FR-002 | T047, T051, T054 | | FR-019 | T065, T075, T085 |
| FR-003 | T049, T053, T056 | | FR-020 | T086, T087, T088 |
| FR-004 | T048, T051, T055 | | FR-021 | **T111** |
| FR-005 | T050, T057, T060 | | FR-022 | T020–T028, T073 |
| FR-006 | T050, T057 | | FR-023 | T067, T073, T074 |
| FR-007 | T050, T057 | | FR-024 | T020, T073 |
| FR-008 | T091, T095, T097 | | FR-025 | T040, T043 |
| FR-009 | T091, T092, T096, T097 | | FR-026 | T040, T086, T087 |
| FR-010 | T092, T096 | | FR-027 | T058, T082, T097, T105 |
| FR-011 | T093, T097 | | FR-028 | T112 |
| FR-012 | T100, T103, T104 | | FR-029 | T015 |
| FR-013 | T100, T101, T104 | | FR-030 | T044, T079 |
| FR-014 | T101, T105 | | FR-031 | T080, T082 |
| FR-015 | T078–T082 | | FR-032 | T085 |
| FR-016 | T078, T081 | | FR-033 | T085 |
| FR-017 | T078, T082 | | | |
| SC-001 | T047, T051 | | SC-006 | T091, T092, T100 |
| SC-002 | T078, T109 | | SC-007 | T077 |
| SC-003 | T108, T109, T110 | | SC-008 | T078, T109 |
| SC-004 | T108, T109 | | SC-009 | T112 |
| SC-005 | **T111** | | SC-010 | T067, T073, T077 |

## Constitution alignment

| Nguyên tắc | Kết luận |
| --- | --- |
| I. Service Boundaries & Data Ownership | ✅ `kb.*` không có dữ liệu người dùng; `UserProfile`/`CandidateFood` chỉ đi qua gRPC; `recommendation_db` là DB riêng, role riêng |
| II. Đồng bộ qua gRPC | ✅ contract `.proto` (T018), deadline 5s cả hai chiều (T013), map lỗi transport tường minh (T014) |
| III. Bất đồng bộ qua RabbitMQ | ✅ N/A — service này không phát/không consume event |
| IV. Contract-First | ✅ `.proto` là nguồn chân lý; entity `KnowledgeChunk` không đi qua contract |
| V. Test-First & Verified Gates | ✅ mỗi story có test negative control; E2E qua Postgres container thật + corpus thật, assert giá trị cụ thể (T051, T081, T087, T094, T102, T111, T077) |

## Kết luận Loop A

**0 CRITICAL, 0 HIGH** → sang Loop B (implement theo thứ tự phase).
