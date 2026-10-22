# Specification Quality Checklist: Recommendation Service với RAG tri thức sách "Chế độ ăn trường thọ"

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-22
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — spec chỉ nêu hành vi người dùng, quy tắc nghiệp vụ và thực thể nghiệp vụ; chi tiết kỹ thuật (gRPC, pgvector, pipeline) nằm ở ADR 002 + PRD §2.4, không lặp trong spec
- [x] Focused on user value and business needs — 6 user story đều bắt đầu từ "Là người dùng/người bảo trì ... tôi muốn ..."
- [x] Written for non-technical stakeholders — tránh từ khóa hạ tầng; chỉ dùng khái niệm nghiệp vụ (trích dẫn, kho tri thức, chương, trang sách)
- [x] All mandatory sections completed — User Scenarios & Testing (6 story + edge cases), Requirements (FR-001..FR-031, Key Entities), Success Criteria (SC-001..SC-010), Assumptions

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — không còn marker nào; các quyết định còn mở đã được chốt trước khi viết spec (rubric 35/30/20/15, top-k 8, chunk 1000/150, chiến lược cờ `needs_review`, giữ citation kèm cờ)
- [x] Requirements are testable and unambiguous — mỗi FR có động từ bắt buộc ("phải") + đối tượng kiểm chứng cụ thể; FR-002 "100%", FR-004 "105%", FR-017 quy tắc loại trích dẫn, FR-018 quy tắc abstain
- [x] Success criteria are measurable — SC-001 100%, SC-002 100%, SC-003 ≥90% ±3 trang, SC-004 0 câu bịa, SC-005 <3 giây p95, SC-006 xác định, SC-009/SC-010 đếm được
- [x] Success criteria are technology-agnostic (no implementation details) — không nhắc framework, DB, gRPC; SC-007/SC-009 mô tả bằng khái niệm "kho tri thức" và "bản ghi dữ liệu người dùng"
- [x] All acceptance scenarios are defined — mỗi user story có ≥3 kịch bản Given/When/Then với giá trị cụ thể (dị ứng đậu phộng/sữa, thiếu 30g protein, 16:8, 7 giờ, 5/7 ngày)
- [x] Edge cases are identified — 10 edge case: dị ứng lấn át, chạm trần calo, danh mục rỗng, kho tri thức rỗng, câu hỏi ngoài lĩnh vực, sai dấu/câu dài, trích dẫn không khớp, AI lỗi/hết hạn mức, trang chưa đối chiếu, truy vấn đồng thời
- [x] Scope is clearly bounded — mục "Assumptions" ghi rõ tích hợp phía service dinh dưỡng nằm ngoài phạm vi; spec chỉ phủ 1 service
- [x] Dependencies and assumptions identified — 6 giả định: corpus đã số hoá, OCR chưa đối chiếu, người dùng không gọi trực tiếp, hồ sơ/danh mục do service khác truyền vào, tích hợp Diet tách riêng, tính quyết định khi đồng thời

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria — FR nằm trong 6 nhóm, mỗi nhóm có ít nhất một acceptance scenario bao phủ (FR-001..007 → US1; FR-008..011 → US5; FR-012..014 → US6; FR-015..019 → US3; FR-020 → US4; FR-022..026 → US2; FR-027..031 → ràng buộc xuyên suốt)
- [x] User scenarios cover primary flows — 6 story: giá trị sản phẩm (US1), điều kiện tiên quyết (US2), lõi RAG (US3, US4), phân tích (US5, US6)
- [x] Feature meets measurable outcomes defined in Success Criteria — mỗi SC đều có story/FR tương ứng để đo
- [x] No implementation details leak into specification — kiểm tra lại toàn spec: không có tên lớp, thư viện, lệnh, endpoint cụ thể

## Ghi chú xác nhận

- Ưu tiên story: P1 = US1 (gợi ý bữa ăn an toàn, giá trị + rủi ro an toàn cao nhất), P2 = US2 (nạp tri thức — điều kiện tiên quyết cho US3/US4), P3 = US3 (hỏi sách), P4 = US4 (tra cứu đoạn), P5 = US5 (gap vi chất), P6 = US6 (điểm thói quen). Thứ tự này khớp với plan đã chốt.
- Điểm cần chú ý khi lập kế hoạch: FR-002 và FR-017 là hai quy tắc **bằng code**, không được giao cho AI — cần task và test riêng (negative control theo Constitution V).
- SC-009 (không dữ liệu người dùng trong kho tri thức) cần được kiểm chứng bằng test truy vấn trực tiếp kho tri thức trong tích hợp test.
- Items marked incomplete require spec updates before `$speckit-clarify` or `$speckit-plan` — không còn mục nào chưa hoàn thành.
