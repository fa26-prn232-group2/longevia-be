# Feature Specification: Hoàn thiện MVP Longevia

**Feature Branch**: `feat/longevia-mvp-completion`

**Created**: 2026-11-09

**Status**: Draft

**Input**: Hoàn thiện toàn bộ phạm vi MVP còn thiếu của Longevia (trừ deploy): ổn định AI, nối end-to-end các RPC Recommendation còn thiếu, hoàn thiện email, CRUD và hardening; deploy thật là backlog.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - AI ổn định và quan sát được (Priority: P1)

Là người dùng, tôi muốn gợi ý bữa ăn bằng AI trả kết quả trong thời gian hợp lý và không bị lỗi timeout vô cớ; là người vận hành, tôi muốn thấy metric của Recommendation trên Prometheus/Grafana.

**Why this priority**: AI timeout đang chặn tính năng đã có; Recommendation thiếu metric làm mất khả năng vận hành.

**Independent Test**: Gọi 50 lần endpoint gợi ý bữa ăn với provider thật/double, đo p50/p95; kiểm tra Prometheus target `recommendation` = UP và có metric `recommendation_rpc_calls_total`.

**Acceptance Scenarios**:

1. **Given** cấu hình deadline mới, **When** gọi gợi ý bữa ăn, **Then** không còn giới hạn cứng 2 giây ở provider và endpoint trả kết quả hoặc lỗi timeout theo budget đã chốt.
2. **Given** stack Docker đang chạy, **When** mở Prometheus, **Then** target `recommendation` UP và metric RPC xuất hiện.
3. **Given** p95 đo được, **When** chốt budget, **Then** budget = `ceil(p95 × 1.25)`, cap 25 giây, được ghi vào tài liệu/NFR.

---

### User Story 2 - Kế hoạch ăn uống, vi chất và tri thức sách (Priority: P1)

Là người dùng, tôi muốn nhận kế hoạch ăn uống theo tuần, xem thiếu hụt vi chất 7 ngày, và hỏi/tra cứu tri thức sách với trích dẫn.

**Why this priority**: Đây là các tính năng MVP đã có RPC nhưng chưa có REST/UI.

**Independent Test**: Tạo kế hoạch tuần, mở lại kế hoạch đã lưu; gọi nutrient gaps với 7 ngày dữ liệu; hỏi sách và nhận citation hoặc abstain.

**Acceptance Scenarios**:

1. **Given** hồ sơ, mục tiêu và danh mục món an toàn, **When** tạo kế hoạch tuần, **Then** kế hoạch được lưu và mở lại đúng dữ liệu, có disclaimer/citation/warning.
2. **Given** 7 ngày meal log, **When** xem vi chất, **Then** trả gap/severity/suggested foods; dữ liệu thiếu được ghi unknown, không phải 0.
3. **Given** câu hỏi ngoài corpus, **When** hỏi sách, **Then** trả `abstained = true` kèm lý do, không bịa.
4. **Given** citation `verified = false`, **When** hiển thị, **Then** UI ghi rõ “chưa kiểm chứng”.

---

### User Story 3 - Kế hoạch tập luyện và điểm thói quen (Priority: P2)

Là người dùng, tôi muốn nhận kế hoạch tập luyện theo tuần và điểm thói quen trường thọ kèm phân rã điểm.

**Why this priority**: Bổ sung trục vận động và phản hồi thói quen sau khi hạ tầng AI ổn định.

**Independent Test**: Tạo workout plan 7 ngày với số buổi/phút/giới hạn; xem longevity score với dữ liệu 7 ngày.

**Acceptance Scenarios**:

1. **Given** goal, số buổi, phút, hoạt động/thiết bị, **When** tạo kế hoạch, **Then** đúng số ngày/buổi, ngày nghỉ rõ ràng, có citation/warning/disclaimer.
2. **Given** dữ liệu fasting/sleep/water/nutrition 7 ngày, **When** xem điểm, **Then** score deterministic, có breakdown và insight.

---

### User Story 4 - Email chào mừng và báo cáo tuần (Priority: P3)

Là người dùng, tôi muốn nhận email chào mừng sau khi xác minh và báo cáo tuần nếu tôi bật nhận.

**Why this priority**: Hoàn thiện Worker; báo cáo tuần phụ thuộc dữ liệu của các service khác.

**Independent Test**: Verify email → nhận welcome; bật preference → tuần kế tiếp nhận digest; tắt preference → không nhận.

**Acceptance Scenarios**:

1. **Given** tài khoản pending, **When** verify thành công, **Then** Worker gửi welcome đúng một lần.
2. **Given** user bật `receiveWeeklyDigest`, **When** scheduler chạy, **Then** publish event và Worker gửi digest.
3. **Given** user tắt preference, **When** scheduler chạy, **Then** không gửi.

---

### User Story 5 - CRUD và hardening (Priority: P3)

Là người dùng, tôi muốn sửa/xoá món cá nhân và xoá bản ghi sức khỏe; là hệ thống, tôi muốn chặn gọi gRPC trái phép và giới hạn tần suất gateway.

**Independent Test**: Sửa/xoá custom food; xoá metric/sleep; gọi gRPC không token bị `UNAUTHENTICATED`; vượt rate limit nhận 429.

**Acceptance Scenarios**:

1. **Given** custom food của mình, **When** sửa/xoá, **Then** thành công; người khác không sửa được.
2. **Given** gRPC không có service token, **When** gọi, **Then** bị từ chối.
3. **Given** vượt ngưỡng request, **When** gọi gateway, **Then** nhận 429 ProblemDetails.

---

### Edge Cases

- AI provider chậm hơn budget hoặc lỗi timeout.
- Corpus rỗng hoặc citation không khớp.
- Kế hoạch tuần đã tồn tại, generate lại cùng tuần.
- Người dùng chưa có hồ sơ/cân nặng/mục tiêu.
- Món không có dữ liệu vi chất.
- User tắt digest giữa tuần.
- Xoá món đã dùng trong meal log.
- Hai service gọi nhau khi thiếu/sai token.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Recommendation MUST expose `/metrics` và được Prometheus scrape.
- **FR-002**: Diet MUST dùng deadline cấu hình cho Recommendation, không hard-code 2.8s/2s.
- **FR-003**: SLO AI MUST được đo lại và ghi bằng budget thực tế (D9).
- **FR-004**: Diet MUST có REST + persistence cho meal plan tuần.
- **FR-005**: Diet MUST có REST cho nutrient gaps 7 ngày.
- **FR-006**: Diet MUST có REST facade cho ask/search tri thức sách.
- **FR-007**: Diet MUST cung cấp gRPC nutrition adherence/weekly summary cho Progress.
- **FR-008**: Food MUST có dữ liệu vi chất; thiếu dữ liệu MUST là unknown, không phải 0.
- **FR-009**: Progress MUST có REST + persistence cho workout plan tuần.
- **FR-010**: Progress MUST có REST cho longevity habits và gọi Diet/Recommendation qua gRPC.
- **FR-011**: Identity MUST publish `UserVerifiedEvent` trong transaction verify.
- **FR-012**: Identity MUST có preference `receiveWeeklyDigest` (mặc định false) + endpoint cập nhật.
- **FR-013**: Worker MUST gửi welcome một lần và digest một lần theo event.
- **FR-014**: Progress scheduler MUST chỉ gửi digest cho user opted-in.
- **FR-015**: Diet MUST cho phép update/delete custom food của owner (soft delete).
- **FR-016**: Progress MUST cho phép delete body metric/sleep của owner.
- **FR-017**: gRPC nội bộ MUST dùng service token qua metadata và từ chối request thiếu/sai token.
- **FR-018**: Gateway MUST rate limit và trả 429 ProblemDetails.
- **FR-019**: FE MUST có trang cho 5 tính năng AI mới, không trang mồ côi, tiếng Việt.
- **FR-020**: Navbar MUST chỉ highlight mục khớp sâu nhất.
- **FR-021**: Mọi response AI MUST có disclaimer; citation `verified=false` MUST được ghi rõ.
- **FR-022**: Deploy cloud/CD/TLS production là backlog, không thuộc feature này.

### Key Entities

- **MealPlan / MealPlanDay / MealPlanMeal / MealPlanItem / MealPlanSuggestion**: kế hoạch ăn tuần đã lưu.
- **FoodMicronutrient**: giá trị vi chất theo món; thiếu row = unknown.
- **WorkoutPlan / WorkoutPlanDay**: kế hoạch tập tuần.
- **UserPreference**: `receiveWeeklyDigest`.
- **UserVerifiedEvent / WeeklyDigestReadyEvent**: event bất đồng bộ.
- **NutritionAdherence / WeeklyNutritionSummary**: dữ liệu Diet cung cấp cho Progress.

## Success Criteria *(mandatory)*

- **SC-001**: 100% endpoint/RPC mới có test unit + integration; full gate xanh.
- **SC-002**: AI p95 nằm trong budget D9; không còn timeout do giới hạn 2 giây.
- **SC-003**: 100% kế hoạch tuần trả đủ ngày/bữa và mở lại đúng dữ liệu đã lưu.
- **SC-004**: 100% response AI có disclaimer; 0 citation không khớp còn sót.
- **SC-005**: 0 email digest gửi cho user chưa opt-in.
- **SC-006**: 100% gRPC không token bị chặn; rate limit trả 429.
- **SC-007**: FE `tsc`, test, lint, build pass; navbar chỉ highlight một mục.

## Assumptions

- Recommendation vẫn gRPC-only; client-facing đi qua Diet/Progress.
- Deploy thật, TLS, CD, Brevo production là backlog.
- Dữ liệu vi chất lấy từ nguồn có thẩm quyền (USDA), seed cho món hệ thống.
- Meal/workout plan lưu DB; Digest opt-in mặc định false.
- Service auth dùng service token cho MVP; mTLS để backlog.
