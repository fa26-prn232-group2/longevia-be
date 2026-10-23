# Feature Specification: Sinh kế hoạch ăn uống và tập luyện theo tuần bằng hai RPC mới

**Feature Branch**: `feat/recommendation-plan-rpcs`

**Created**: 2026-10-23

**Status**: Draft

**Input**: User description: "Mở rộng Recommendation Service bằng 2 RPC gRPC mới để sinh kế hoạch theo TUẦN: `GenerateMealPlan` (Diet gọi) và `GenerateWorkoutPlan` (Progress gọi). Service chỉ TRẢ kế hoạch, KHÔNG lưu dữ liệu người dùng. Kế hoạch tháng là việc của caller. Mọi nội dung chỉ đến từ corpus sách qua RAG (có Citation verify) hoặc từ dữ liệu caller gửi; cấm tự nghĩ, tự tính BMR/TDEE, tự suy ra dị ứng/sở thích/bệnh nền/chấn thương/thiết bị/quỹ thời gian. Thiếu nguồn thì bỏ phần đó và ghi `warnings`."

> Tài liệu trong `specs/` viết bằng tiếng Việt (`docs/development/agent-workflow.md` §IV.7). Giữ nguyên tên heading của template để các skill downstream phân tích được.

---

## Clarifications

### Session 2026-10-23

- Q: Trong `GenerateMealPlan`, các trường `target_calories` và target protein/carbs/fat mà Diet gửi là mục tiêu cho một ngày hay cho cả tuần? → A: Mục tiêu ngày — Diet gửi target của một ngày; service nhân với `days` để có tổng tuần rồi chia ngân sách theo ngày/bữa.
- Q: Khi `consumed_calories` đạt 105% mục tiêu ngày, response của ngày đó nên chứa gì? → A: Trường gợi ý riêng — ngày chạm trần có `meals` rỗng và thêm `suggestions` gồm nội dung + `citations` từ sách cho đồ uống 0 calo/thói quen; không dùng `food_id` rỗng trong món.
- Q: Nếu một `CandidateFood` có danh sách `allergens` rỗng, service nên hiểu là món không chứa allergen đã biết hay coi là thiếu dữ liệu an toàn và loại món? → A: Không chứa allergen — caller chịu trách nhiệm điền đầy đủ; danh sách rỗng nghĩa là món không có allergen đã biết và được dùng.
- Q: Trong một kế hoạch tuần bắt đầu từ thứ Hai, `consumed_calories` nên được áp cho ngày nào? → A: Thêm `consumed_date` — caller nói rõ calo đã ăn thuộc ngày nào; service chỉ áp rule nếu ngày đó nằm trong tuần.
- Q: Việc khớp allergen giữa `profile.allergies` và `CandidateFood.allergens` nên dùng quy tắc nào? → A: Chuẩn hoá + khớp chính xác — trim, lowercase, Unicode NFC rồi so khớp nguyên token; caller chịu trách nhiệm dùng cùng bộ từ vựng.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Diet nhận kế hoạch bữa ăn 7 ngày an toàn và bám mục tiêu (Priority: P1) 🎯 MVP

Là service Diet, tôi muốn gọi Recommendation để nhận kế hoạch ăn uống theo tuần cho một người dùng, trong đó mọi món đều nằm trong danh mục món an toàn do tôi gửi, không chứa thành phần dị ứng, tổng calo/macro từng ngày bám mục tiêu tôi đã tính, và mỗi lời giải thích đều có căn cứ từ dữ liệu tôi gửi hoặc từ sách.

**Why this priority**: Đây là giá trị cao nhất của feature và là nơi rủi ro an toàn lớn nhất (BR-03: loại trừ 100% dị ứng). Nếu chỉ làm một story, story này đã tạo ra một kế hoạch tuần dùng được cho Diet.

**Independent Test**: Gọi `GenerateMealPlan` với một hồ sơ, mục tiêu calo/macro, `candidate_foods` và một tuần bắt đầu từ thứ Hai → nhận về đủ số ngày/bữa đã yêu cầu; kiểm tra được 100% món có `food_id` thuộc danh mục gửi lên, không món nào chứa allergen của hồ sơ, tổng calo/macro từng ngày nằm trong ±10% mục tiêu ngày (khi danh mục đủ đa dạng), và mọi response kế hoạch đều có `disclaimer`.

**Acceptance Scenarios**:

1. **Given** hồ sơ dị ứng đậu phộng, danh mục món có cả món chứa và không chứa đậu phộng, **When** tôi gọi `GenerateMealPlan`, **Then** không món nào trong kế hoạch chứa đậu phộng và mọi `food_id` đều thuộc `candidate_foods` tôi đã gửi.
2. **Given** tôi gửi `target_calories` cùng target protein/carbs/fat là **mục tiêu ngày** và danh mục món đủ đa dạng, **When** tôi gọi `GenerateMealPlan`, **Then** mỗi ngày có tổng calo và macro nằm trong ±10% mục tiêu ngày.
3. **Given** tôi gửi `excluded_food_ids` gồm các món đã ăn hoặc không muốn lặp, **When** tôi gọi `GenerateMealPlan`, **Then** không món nào trong kế hoạch trùng với danh sách loại trừ.
4. **Given** tôi gửi `consumed_calories` và `consumed_date` là một ngày trong tuần đã đạt 105% mục tiêu calo của ngày đó, **When** tôi gọi `GenerateMealPlan`, **Then** ngày đó có `meals` rỗng, có cờ chạm trần và `suggestions` gồm đồ uống không calo/thói quen thư giãn có căn cứ sách; nếu không có căn cứ thì `suggestions` rỗng kèm `warnings`.
5. **Given** dịch vụ AI lỗi hoặc quá thời gian chờ, **When** tôi gọi `GenerateMealPlan`, **Then** tôi vẫn nhận một kế hoạch hợp lệ chọn hoàn toàn từ `candidate_foods`, kèm `warnings` ghi rõ đã dùng phương án dự phòng, không nhận kế hoạch rỗng.
6. **Given** kho tri thức sách chưa nạp, **When** tôi gọi `GenerateMealPlan`, **Then** tôi vẫn nhận kế hoạch từ dữ liệu request, `citations` rỗng và `warnings` có mã `corpus_not_loaded`.
7. **Given** tôi thiếu `target_calories` hoặc không gửi món ứng viên nào, **When** tôi gọi `GenerateMealPlan`, **Then** tôi nhận lỗi nghiệp vụ với mã ổn định, không nhận kế hoạch do service tự đặt mục tiêu hay tự bịa món.

---

### User Story 2 - Progress nhận kế hoạch tập luyện 7 ngày trong giới hạn người dùng (Priority: P2)

Là service Progress, tôi muốn gọi Recommendation để nhận kế hoạch tập luyện theo tuần, trong đó số buổi, thời lượng, loại hoạt động và thiết bị đều nằm trong giới hạn tôi gửi và chỉ dựa trên hướng dẫn tập luyện của sách.

**Why this priority**: Bổ sung trục vận động cho kế hoạch trường thọ; giá trị rõ nhưng phụ thuộc P1 về hạ tầng RAG, citation và fallback.

**Independent Test**: Gọi `GenerateWorkoutPlan` với hồ sơ, mục tiêu, tuần bắt đầu từ thứ Hai, số buổi, thời lượng, hoạt động ưa thích và thiết bị sẵn có → nhận về đủ 7 ngày, mỗi ngày là một buổi tập hoặc ngày nghỉ; số buổi đúng yêu cầu; không hoạt động/thiết bị nào nằm ngoài giới hạn gửi lên; mọi hướng dẫn kèm citation từ chương 5 hoặc bị bỏ kèm `warnings`.

**Acceptance Scenarios**:

1. **Given** tôi gửi 3 buổi/tuần, 30 phút/buổi, hoạt động ưa thích `walking` và không có thiết bị, **When** tôi gọi `GenerateWorkoutPlan`, **Then** kế hoạch có đúng 3 buổi và 4 ngày nghỉ, mỗi buổi dùng hoạt động thuộc giới hạn đã gửi và không đòi thiết bị.
2. **Given** tôi gửi `limitations` rỗng, **When** tôi gọi `GenerateWorkoutPlan`, **Then** service chỉ dùng hướng dẫn chung của sách, không giả định người dùng khỏe mạnh hay bị chấn thương, và ghi giả định đó vào `warnings`.
3. **Given** tôi gửi giới hạn sức khỏe khiến một hoạt động không phù hợp, **When** tôi gọi `GenerateWorkoutPlan`, **Then** hoạt động đó không xuất hiện; nếu không còn hoạt động an toàn, ngày đó là ngày nghỉ kèm `warnings`, không tự thay bằng bài tập khác ngoài sách.
4. **Given** dịch vụ AI lỗi hoặc quá thời gian chờ, **When** tôi gọi `GenerateWorkoutPlan`, **Then** tôi vẫn nhận khung kế hoạch xác định từ hướng dẫn sách và giới hạn đã gửi, kèm `warnings`, không nhận kế hoạch rỗng.
5. **Given** kho tri thức sách chưa nạp, **When** tôi gọi `GenerateWorkoutPlan`, **Then** service chỉ dùng hoạt động/thời lượng do tôi gửi, không tự thêm cường độ hay bài tập, `citations` rỗng và có `warnings` mã `corpus_not_loaded`.

---

### Edge Cases

- **Thiếu mục tiêu bắt buộc**: không có `target_calories` hoặc thiếu target protein/carbs/fat → lỗi nghiệp vụ `recommendation.missing_targets`; không tự tính BMR/TDEE, không tự đặt mục tiêu.
- **Không có món ứng viên**: `candidate_foods` rỗng → lỗi `recommendation.no_candidate_food`; không gọi AI và không bịa món.
- **Mọi món ứng viên đều bị loại**: tất cả chứa allergen, không hợp sở thích ăn uống, hoặc nằm trong `excluded_food_ids` → lỗi `recommendation.all_candidates_excluded`; không nới lỏng bộ lọc.
- **`days` lớn hơn 7 hoặc nhỏ hơn 1** → lỗi `recommendation.invalid_days`.
- **`week_start_date` không phải thứ Hai hoặc sai định dạng ISO** → lỗi `recommendation.invalid_week_start`.
- **AI trả món ngoài danh mục** → loại món đó; nếu kế hoạch không còn hợp lệ thì chuyển sang phương án dự phòng xác định từ `candidate_foods`.
- **AI trả lệch mục tiêu ngày quá ±10%** → từ chối kế hoạch AI và chuyển sang phương án dự phòng.
- **AI trả sai số ngày/bữa hoặc quá 5 món một bữa** → từ chối kế hoạch AI và chuyển sang phương án dự phòng.
- **Provider AI lỗi, quá thời gian chờ hoặc hết hạn mức** → vẫn trả kế hoạch dự phòng hợp lệ kèm `warnings`, không làm hỏng request của caller.
- **Kho tri thức rỗng/chưa nạp** → vẫn trả kế hoạch dựa trên dữ liệu request; `citations` rỗng và `warnings` có `corpus_not_loaded`; không suy diễn nội dung sách.
- **Caller gửi `limitations` rỗng** → chỉ dùng hướng dẫn chung chương 5, không giả định khỏe mạnh hay chấn thương, ghi giả định vào `warnings`.
- **`candidate_foods` vượt 120 món** → cắt bớt theo độ khớp macro với mục tiêu, giữ tối đa 120 món và ghi `warnings` mã `candidate_foods_truncated`.
- **`candidate_foods` không đủ đa dạng để đạt ±10%** → trả kế hoạch gần nhất có thể từ chính danh mục, kèm `warnings` mã `target_not_met`; không bịa món hay số liệu để lấp.
- **`consumed_calories` ≥ 105% mục tiêu ngày của `consumed_date`** → ngày đó (nếu nằm trong tuần) có `meals` rỗng, thêm `suggestions` cho đồ uống 0 calo/thói quen có căn cứ sách (nếu không có căn cứ thì `suggestions` rỗng + `warnings`) và cờ chạm trần; nếu `consumed_date` ngoài tuần thì bỏ qua rule và ghi `warnings`.
- **`consumed_calories` > 0 nhưng thiếu `consumed_date`** → lỗi `recommendation.missing_consumed_date`; **`consumed_date` sai định dạng ISO** → lỗi `recommendation.invalid_consumed_date`.
- **`sessions_per_week` lớn hơn `days`** → lỗi `recommendation.invalid_workout_params` vì không thể xếp đủ buổi trong số ngày đã yêu cầu.
- **`sessions_per_week = 0` và `days < 3`** → dùng mặc định `min(3, days)`, không trả lỗi.
- **`limitations` có giá trị không khớp bảng ánh xạ đóng** → bỏ qua giá trị đó và ghi warning `unsupported_limitation`; không suy diễn y tế.
- **`consumed_calories` âm** → lỗi `recommendation.invalid_consumed_calories`; **`target_fiber_grams` âm** → lỗi `recommendation.invalid_targets`.
- **Hoạt động ưa thích không có trong chương 5 hoặc không khả thi với thiết bị** → bỏ hoạt động đó kèm `warnings`; ngày thiếu hoạt động an toàn trở thành ngày nghỉ.
- **Hai món ứng viên trùng `food_id`** → chỉ giữ một bản ghi, ghi `warnings` mã `duplicate_candidate_food`.
- **Món ứng viên có dinh dưỡng âm hoặc thiếu trường bắt buộc** → lỗi `recommendation.invalid_candidate_food`; không tự sửa số liệu.
- **`locale` không được hỗ trợ** → dùng `vi-VN` và ghi `warnings` mã `unsupported_locale`; không đổi nội dung kế hoạch.
- **Cùng request và cùng kết quả provider** → phương án dự phòng phải cho cùng một kế hoạch; không phụ thuộc thứ tự xử lý.

---

## Requirements *(mandatory)*

### Functional Requirements

#### A. Grounding — chỉ dùng hai nguồn, không tự nghĩ

- **FR-001**: Mọi nội dung trong kế hoạch MUST đến từ đúng một trong hai nguồn: **(A)** corpus sách *Chế độ ăn trường thọ* thông qua RAG, hoặc **(B)** dữ liệu caller gửi trong request. Không được dùng nguồn thứ ba.
- **FR-002**: Mọi khẳng định lấy từ corpus MUST kèm `Citation` gồm `chapter_number`, `chapter_title`, `book_page`, `pdf_page`, `quote`, `verified`, `source_file`.
- **FR-003**: Hệ thống MUST kiểm chứng `quote` của từng citation bằng code với nội dung chunk nguồn; citation không khớp MUST bị loại. Nếu một khẳng định mất hết citation và không có căn cứ từ request thì khẳng định đó MUST bị bỏ.
- **FR-004**: Hệ thống MUST chỉ chọn món ăn trong `meals` có `food_id` thuộc `candidate_foods`; MUST trả `food_id` để caller ánh xạ ngược. Không được bịa tên món, calo, macro, khối lượng hoặc thành phần. `suggestions` (đồ uống 0 calo/thói quen) không phải món và MUST NOT mang `food_id` giả.
- **FR-005**: Calo, macro, fiber và allergen của món trong kế hoạch MUST lấy nguyên giá trị từ `candidate_foods`; hệ thống MUST NOT tự ước lượng, làm tròn thành giá trị mới hoặc hiệu chỉnh số liệu.
- **FR-006**: Hệ thống MUST NOT tính BMR/TDEE hoặc tự đặt mục tiêu calo/macro. Đó là trách nhiệm của Diet (PRD US-DIET-02). Thiếu mục tiêu bắt buộc MUST trả lỗi nghiệp vụ, không suy diễn.
- **FR-007**: Hệ thống MUST NOT tự suy ra dị ứng, sở thích ăn uống, bệnh nền, chấn thương, thiết bị tập hoặc quỹ thời gian. Mọi ràng buộc loại này chỉ được lấy đúng từ request.
- **FR-008**: Hệ thống MUST NOT tự thêm hoạt động, thiết bị hoặc bài tập không có căn cứ trong chương 5 và không nằm trong giới hạn caller gửi.
- **FR-009**: Hệ thống MUST NOT dùng kiến thức ngoài corpus sách và ngoài request, kể cả kiến thức dinh dưỡng, y tế hoặc thể dục phổ biến.
- **FR-010**: Khi một phần kế hoạch không có đủ nguồn A hoặc B, hệ thống MUST bỏ phần đó và ghi `warnings`; MUST NOT thay bằng nội dung tự nghĩ.
- **FR-011**: Mọi response kế hoạch thành công MUST có `disclaimer`; hệ thống MUST NOT đưa khuyến nghị về thuốc, liều thuốc, chẩn đoán hoặc thay thế điều trị.
- **FR-012**: Mọi `warnings` MUST có mã ổn định và mô tả ngắn gọn để caller phân biệt được lý do bỏ phần, dùng dự phòng hoặc giả định.

#### B. `GenerateMealPlan` — kế hoạch ăn uống theo tuần

- **FR-013**: `GenerateMealPlan` MUST trả tối đa 7 ngày, mỗi ngày tối đa 4 bữa (sáng/trưa/tối/phụ) theo `meals_per_day`, mỗi bữa tối đa 5 món.
- **FR-014**: Mỗi ngày MUST có ngày ISO tương ứng, tổng calo/macro/fiber của ngày, danh sách bữa và món; mỗi món MUST giữ `food_id`, tên, khẩu phần và dinh dưỡng gốc từ candidate.
- **FR-015**: Trước khi gọi AI, code MUST lọc 100% món chứa allergen khớp `profile.allergies`, lọc món không phù hợp `profile.dietary_preference`, và loại món nằm trong `excluded_food_ids`. Việc khớp allergen MUST chuẩn hoá cả hai phía bằng trim, lowercase và Unicode NFC rồi so khớp chính xác từng token; caller chịu trách nhiệm dùng cùng bộ từ vựng. `CandidateFood.allergens` rỗng MUST được hiểu là caller khai món không có allergen đã biết; caller chịu trách nhiệm điền đầy đủ dữ liệu allergen.
- **FR-016**: Code MUST dùng mục tiêu **ngày** caller gửi, nhân với `days` để có mục tiêu tuần, rồi chia ngân sách cho từng ngày và từng bữa. Tổng ngân sách các ngày MUST bằng mục tiêu ngày × `days`; ngân sách mỗi ngày MUST bằng mục tiêu ngày; tổng ngân sách các bữa trong ngày MUST bằng ngân sách ngày.
- **FR-017**: Code MUST áp rule chạm trần 105% calo/ngày. Khi caller gửi `consumed_calories` kèm `consumed_date`: nếu `consumed_date` nằm trong tuần và calo đã ăn ≥ 105% mục tiêu ngày đó, ngày đó MUST có `meals` rỗng, cờ chạm trần, và `suggestions` (tối đa 3) gồm đồ uống không calo/thói quen thư giãn có căn cứ sách; nếu không có căn cứ, `suggestions` MUST rỗng và có `warnings` mã `suggestion_source_missing`. Nếu `consumed_date` ngoài tuần, MUST bỏ qua rule và ghi `warnings` mã `consumed_date_outside_plan`. Không được dùng `food_id` rỗng trong `meals`.
- **FR-018**: Code MUST loại mọi món trùng `excluded_food_ids` khỏi mọi ngày; nếu sau khi loại không còn món an toàn, MUST trả lỗi nghiệp vụ thay vì nới lỏng bộ lọc.
- **FR-019**: Sau khi AI trả, code MUST kiểm tra: tổng calo, protein, carbs và fat mỗi ngày lệch không quá ±10% ngân sách ngày; không có allergen; đúng số ngày/bữa; không quá 5 món/bữa; mọi `food_id` thuộc candidate. Fiber chỉ được báo cáo và ghi warning nếu thiếu, không nằm trong gate cứng ±10%.
- **FR-020**: Nếu kết quả AI không đạt kiểm tra hoặc provider lỗi/timeout, hệ thống MUST chọn phương án dự phòng xác định từ chính candidate đã lọc (ưu tiên khớp macro; ưu tiên không lặp món trong cùng ngày, chỉ lặp khi không đủ candidate để giữ đủ bữa; hạn chế lặp trong tuần khi còn lựa chọn) và MUST vẫn trả kế hoạch hợp lệ, không abstain, kèm `warnings` mã `ai_fallback_used`.
- **FR-021**: Nếu chính candidate không đủ để đạt ±10%, hệ thống MUST trả kế hoạch gần nhất có thể từ candidate, kèm `warnings` mã `target_not_met`; MUST NOT bịa món, calo hoặc macro để đạt mục tiêu.
- **FR-022**: `rationale` của mỗi bữa MUST chỉ dựa trên dữ liệu request (mục tiêu, hồ sơ, sở thích, allergen, món đã chọn) hoặc trích dẫn sách; MUST NOT là diễn giải dinh dưỡng tự do ngoài hai nguồn.
- **FR-023**: `citations` của món/bữa trong kế hoạch ăn uống MUST lấy từ chương 4 và Phụ lục A (thực đơn hai tuần, nằm trong tệp `docs/reference/che-do-an-truong-tho/12-chuong-12-cach-de-giu-mai-tuoi-thanh-xuan.md`). Riêng `suggestions` của ngày chạm trần 105%, citation MAY lấy thêm chương 11 nhưng MUST chỉ là đoạn nói về nước/trà thảo mộc không đường (ví dụ câu “uống 2 đến 3 lít chất lỏng không đường (nước và trà thảo mộc)”); MUST NOT lấy ngữ cảnh bệnh nhân, FMD, nhịn ăn hoặc điều trị. Khi không có đoạn phù hợp, `suggestions` MUST rỗng và có warning `suggestion_source_missing`. Khi corpus rỗng, `citations` MUST rỗng và `warnings` có `corpus_not_loaded`.
- **FR-024**: Response MUST có `disclaimer`, `week_start_date`, `days_requested` (số ngày), tổng mục tiêu tuần (`weekly_target_*`), danh sách `days` (mỗi ngày có `meals` và `suggestions`), `warnings` và cờ cho biết có dùng phương án dự phòng.
- **FR-025**: Khi `candidate_foods` vượt 120 món, code MUST cắt theo độ khớp macro với mục tiêu trước khi lọc/gọi AI, giữ tối đa 120 món và ghi `warnings`.

#### C. `GenerateWorkoutPlan` — kế hoạch tập luyện theo tuần

- **FR-026**: `GenerateWorkoutPlan` MUST trả đúng `days_requested` ngày (tối đa 7); mỗi ngày MUST là một buổi tập hoặc ngày nghỉ (`rest_day`). Response MUST có `week_start_date`, `days_requested`, `sessions_scheduled` (số buổi thực tế sau mặc định/giới hạn an toàn) và `minutes_per_session`.
- **FR-027**: Loại hoạt động, thời lượng và cường độ MUST chỉ lấy từ hướng dẫn chương 5 (`docs/reference/che-do-an-truong-tho/05-chuong-5-tap-the-duc-va-song-lau-khoe-manh.md`) và nằm trong giới hạn caller gửi. Thời lượng MUST bằng `minutes_per_session` caller gửi; hệ thống MUST NOT tự tăng/giảm ngoài giá trị đó. Nếu không có căn cứ cường độ trong chunk, `intensity` MUST rỗng và ghi warning `intensity_unverified`.
- **FR-028**: Hệ thống MUST xếp đúng `sessions_per_week` buổi khi còn đủ hoạt động an toàn; mỗi ngày tối đa một buổi. Nếu `limitations`/thiết bị loại hết hoạt động của một ngày, số buổi có thể giảm theo FR-031 và MUST ghi `warnings`; nếu `sessions_per_week` lớn hơn `days` hoặc tham số ngoài khoảng, MUST trả lỗi.
- **FR-029**: Hoạt động MUST tương thích `activity_preference`, `equipment_available` và `limitations`. `goal` chỉ được dùng để ưu tiên trong các hoạt động đã có ở chương 5 và giới hạn caller; MUST NOT dùng `goal` để thêm bài tập mới. Hệ thống MUST NOT dùng thiết bị không có trong `equipment_available` và MUST NOT thêm bài tập ngoài chương 5.
- **FR-030**: Khi `limitations` rỗng, hệ thống MUST chỉ dùng hướng dẫn chung của sách, MUST NOT giả định người dùng khỏe mạnh hay bị chấn thương, và MUST ghi giả định đó vào `warnings`.
- **FR-031**: Khi `limitations` có giá trị, hoạt động không tương thích MUST bị loại. `limitations` MUST được khớp với bảng ánh xạ đóng trong `WorkoutLimitationRules`; giá trị không khớp MUST bị bỏ qua và ghi warning `unsupported_limitation`, không suy diễn y tế. Nếu không còn hoạt động an toàn cho một ngày, ngày đó MUST là ngày nghỉ kèm `warnings`, không tự thay bằng bài tập khác.
- **FR-032**: Nếu provider AI lỗi/timeout hoặc kết quả không đạt kiểm tra, hệ thống MUST trả khung kế hoạch xác định từ hướng dẫn sách và giới hạn caller, kèm `warnings`; MUST NOT abstain.
- **FR-033**: Khi corpus rỗng, hệ thống MUST chỉ dùng hoạt động và thời lượng caller gửi, MUST NOT tự thêm cường độ hoặc bài tập, `citations` rỗng và `warnings` có `corpus_not_loaded`.
- **FR-034**: Mỗi buổi tập MUST có ngày, loại hoạt động (key ổn định từ catalog), thời lượng, cường độ (nếu có căn cứ sách), thiết bị sử dụng, `rationale` và `citations`; ngày nghỉ MUST ghi rõ `rest_day`. Ngoại lệ: `rest_day` không có citation; khi corpus rỗng, buổi tập theo FR-033 có thể có `citations` rỗng kèm warning.
- **FR-035**: Hệ thống MUST NOT đưa khuyến nghị y tế/thuốc hoặc hướng dẫn thay thế điều trị; mọi response kế hoạch thành công MUST có `disclaimer`.
- **FR-036**: Hoạt động ưa thích không có trong chương 5 hoặc không khả thi với thiết bị MUST bị bỏ kèm `warnings` mã `unsupported_activity_preference`, không được ánh xạ sang bài tập tự nghĩ.
- **FR-037**: Khi `sessions_per_week` lớn hơn `days`, hệ thống MUST trả lỗi `recommendation.invalid_workout_params` thay vì xếp nhiều buổi một ngày hoặc cắt âm thầm.

#### D. Hợp đồng request và kiểm tra dữ liệu

- **FR-038**: `GenerateMealPlan` MUST nhận các trường bắt buộc: `profile` (age, sex, height_cm, weight_kg, activity_level, dietary_preference, allergies), `target_calories` (**mục tiêu ngày**), `target_protein_grams` (**mục tiêu ngày**), `target_carbs_grams` (**mục tiêu ngày**), `target_fat_grams` (**mục tiêu ngày**), `week_start_date` (thứ Hai, ISO `YYYY-MM-DD`), `days` (1..7), `meals_per_day` (1..4), `candidate_foods` (>= 1; nếu > 120 thì cắt theo FR-025, không phải lỗi).
- **FR-039**: `GenerateMealPlan` MUST nhận các trường tuỳ chọn: `target_fiber_grams`, `excluded_food_ids`, `consumed_calories`, `consumed_date` (ISO, ngày calo đã ăn), `locale`. `target_fiber_grams` MUST >= 0 và `0` nghĩa là không gửi; `consumed_calories` MUST >= 0. Nếu `consumed_calories` lớn hơn 0 thì `consumed_date` MUST được gửi; thiếu `consumed_date` MUST trả `recommendation.missing_consumed_date`, sai định dạng ISO MUST trả `recommendation.invalid_consumed_date`; `consumed_calories` âm MUST trả `recommendation.invalid_consumed_calories`. Rule 105% chỉ áp cho `consumed_date` nằm trong tuần.
- **FR-040**: `GenerateWorkoutPlan` MUST nhận các trường bắt buộc: `profile`, `goal`, `week_start_date` (thứ Hai, ISO), `days` (1..7), `sessions_per_week` (0 = dùng mặc định; giá trị khác phải trong 1..7 và <= `days`), `minutes_per_session` (0 = dùng mặc định; giá trị khác phải trong 10..120), `activity_preference`, `equipment_available`; `limitations` và `locale` là tuỳ chọn.
- **FR-041**: Giá trị `0` của `sessions_per_week` MUST được hiểu là `min(3, days)`; giá trị `0` của `minutes_per_session` MUST được hiểu là 30. Giá trị khác ngoài khoảng cho phép MUST trả `recommendation.invalid_workout_params`.
- **FR-042**: Thiếu hoặc sai trường bắt buộc MUST trả lỗi nghiệp vụ với mã ổn định; MUST NOT tự điền giá trị, tự chọn mặc định ngoài các mặc định đã nêu, hoặc trả kế hoạch một phần.
- **FR-043**: `week_start_date` không phải thứ Hai hoặc sai định dạng MUST trả `recommendation.invalid_week_start`.
- **FR-044**: `days` ngoài 1..7 MUST trả `recommendation.invalid_days`; `meals_per_day` ngoài 1..4 MUST trả `recommendation.invalid_meals_per_day`.
- **FR-045**: `candidate_foods` rỗng MUST trả `recommendation.no_candidate_food`; món thiếu `food_id`, tên hoặc có dinh dưỡng âm MUST trả `recommendation.invalid_candidate_food`; tất cả bị loại MUST trả `recommendation.all_candidates_excluded`.
- **FR-046**: Thiếu `target_calories` hoặc thiếu target protein/carbs/fat MUST trả `recommendation.missing_targets`. `target_calories` phải lớn hơn 0; target macro phải không âm và không đồng thời bằng 0. Giá trị target âm hoặc `target_fiber_grams` âm MUST trả `recommendation.invalid_targets`.
- **FR-047**: `profile` thiếu trường bắt buộc, sai kiểu hoặc giá trị không thuộc tập hợp cho phép MUST trả `recommendation.invalid_profile`. `activity_level` và `dietary_preference` là chuỗi thuộc tập giá trị đã khai báo, không phải enum contract. `goal` không hợp lệ trong workout MUST trả `recommendation.invalid_goal`.
- **FR-048**: Lỗi nghiệp vụ MUST có mã ổn định, thông điệp tiếng Việt dễ hiểu và MUST không chứa kế hoạch, món ăn hay số liệu do hệ thống tự nghĩ.
- **FR-049**: Các mã lỗi tối thiểu phải có: `recommendation.missing_targets`, `recommendation.invalid_targets`, `recommendation.invalid_consumed_calories`, `recommendation.no_candidate_food`, `recommendation.invalid_week_start`, `recommendation.invalid_days`, `recommendation.invalid_meals_per_day`, `recommendation.invalid_profile`, `recommendation.invalid_goal`, `recommendation.invalid_candidate_food`, `recommendation.all_candidates_excluded`, `recommendation.invalid_workout_params`, `recommendation.missing_consumed_date`, `recommendation.invalid_consumed_date`.
- **FR-050**: Hệ thống chỉ hỗ trợ locale `vi-VN`. `locale` rỗng hoặc khác `vi-VN` MUST dùng `vi-VN`, ghi `warnings` mã `unsupported_locale` và MUST NOT thay đổi nội dung kế hoạch.

#### E. An toàn, riêng tư và ràng buộc kỹ thuật

- **FR-051**: Recommendation MUST chỉ trả kế hoạch; MUST NOT lưu hồ sơ, kế hoạch, kết quả cá nhân hoá hoặc bất kỳ dữ liệu người dùng nào. `recommendation_db` MUST tiếp tục chỉ chứa corpus tri thức.
- **FR-052**: Feature này MUST NOT tạo migration, bảng mới hoặc thay đổi schema.
- **FR-053**: Hai RPC MUST là service nội bộ, gRPC-only; MUST NOT expose REST hoặc route qua Nginx.
- **FR-054**: Hai RPC MUST có deadline riêng qua `RECOMMENDATION__PLAN_DEADLINE_SECONDS` (mặc định 90 giây, hợp lệ 5–120). Các RPC cũ MUST giữ deadline 30 giây.
- **FR-055**: Thay đổi contract MUST là additive; 5 RPC và các message hiện có MUST NOT bị phá vỡ.
- **FR-056**: Feature MUST NOT tạo `frontend-guide.md` vì không thay đổi HTTP endpoint, event payload hoặc DTO client-facing; kết luận này MUST được ghi trong `tasks.md`.
- **FR-057**: Hệ thống MUST NOT ghi log hồ sơ, dị ứng, giới hạn sức khỏe hoặc nội dung kế hoạch cá nhân ở mức có thể nhận diện người dùng.
- **FR-058**: Mọi response kế hoạch thành công của hai RPC MUST có `disclaimer`; khi provider lỗi và service trả kế hoạch dự phòng, `disclaimer` MUST vẫn hiện diện cùng `warnings`. Lỗi nghiệp vụ trả mã lỗi theo FR-048 và không chứa kế hoạch.
- **FR-059**: Hệ thống MUST xử lý lỗi transport/provider một cách tường minh và MUST không để lỗi provider biến thành response rỗng hoặc lỗi không kiểm soát.
- **FR-060**: Khi caller gửi cùng request và provider cho cùng kết quả, kết quả kiểm tra, fallback và thứ tự chọn món MUST xác định, không phụ thuộc thời điểm hay thứ tự xử lý.

### Key Entities

- **Kế hoạch ăn uống tuần (Weekly Meal Plan)**: kế hoạch trả về cho một tuần; gồm ngày bắt đầu, số ngày, mục tiêu ngày và tổng tuần đã chia, danh sách ngày, `warnings`, `citations`, cờ dự phòng và `disclaimer`.
- **Ngày trong kế hoạch (Plan Day)**: một ngày của kế hoạch; giữ ngày ISO, ngân sách calo/macro, tổng dinh dưỡng thực tế, danh sách bữa, danh sách `suggestions` và cờ chạm trần.
- **Bữa ăn (Planned Meal)**: một bữa trong ngày; giữ loại bữa, ngân sách bữa, danh sách món, `rationale` và `citations`.
- **Món trong kế hoạch (Planned Food)**: món được chọn từ candidate; giữ `food_id`, tên, khẩu phần và dinh dưỡng nguyên bản.
- **Gợi ý thay thế (Suggestion)**: nội dung tư vấn đồ uống 0 calo/thói quen thư giãn; gồm nội dung và `citations`, không phải món và không mang `food_id` giả.
- **Mục tiêu ngày (Daily Targets)**: calo và macro caller gửi cho **một ngày**; service nhân với `days` để có tổng tuần rồi chia ngân sách, không tự tính mục tiêu.
- **Món ứng viên (Candidate Food)**: món trong danh mục an toàn do caller gửi; gồm `food_id`, tên, khẩu phần, calo, macro, fiber, allergen.
- **Hồ sơ người dùng (User Profile)**: dữ liệu đầu vào của mỗi lời gọi; gồm tuổi, giới, chiều cao, cân nặng, mức hoạt động, sở thích ăn uống, dị ứng.
- **Kế hoạch tập luyện tuần (Weekly Workout Plan)**: kế hoạch trả về cho một tuần; gồm ngày bắt đầu, số ngày, số buổi, danh sách ngày, `warnings`, `citations` và `disclaimer`.
- **Ngày tập luyện (Workout Day)**: một ngày của kế hoạch; là buổi tập hoặc ngày nghỉ; buổi tập giữ hoạt động, thời lượng, cường độ, thiết bị, `rationale`, `citations`.
- **Tham số tập luyện (Workout Parameters)**: số buổi/tuần, phút/buổi, hoạt động ưa thích, thiết bị sẵn có, giới hạn sức khỏe do caller gửi.
- **Trích dẫn (Citation)**: chứng cứ từ sách; gồm chương, tên chương, trang bản in, trang PDF, trích đoạn, trạng thái đối chiếu và tệp nguồn.
- **Cảnh báo (Warning)**: ghi nhận phần bị bỏ, dự phòng đã dùng, dữ liệu bị cắt hoặc giả định đã áp dụng; gồm mã và mô tả.
- **Lỗi nghiệp vụ (Business Error)**: lỗi do thiếu/sai dữ liệu đầu vào; gồm mã ổn định và thông điệp, không chứa nội dung kế hoạch.
- **Khuyến cáo (Disclaimer)**: lời nhắc tham khảo khoa học, không thay thế tư vấn chuyên gia, đi kèm mọi response.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% bữa trong bộ kiểm thử tự động không chứa allergen khai báo trong hồ sơ, sau khi chuẩn hoá trim/lowercase/Unicode NFC và so khớp chính xác.
- **SC-002**: 100% kế hoạch trả về đủ số ngày và số bữa đã yêu cầu (7/7 ngày khi `days = 7`), mỗi bữa không quá 5 món.
- **SC-003**: Trong bộ kiểm thử có candidate đủ đa dạng (định nghĩa: ít nhất 12 món, có món giàu protein/carbs/fat, món 0 calo và không allergen), 100% ngày có tổng calo, protein, carbs và fat nằm trong ±10% ngân sách ngày.
- **SC-004**: Khi provider bị ngắt hoặc timeout trong bộ kiểm thử, 100% lượt gọi vẫn trả kế hoạch hợp lệ từ candidate hoặc hướng dẫn sách tuỳ RPC, kèm `warnings`, 0 lượt trả kế hoạch rỗng.
- **SC-005**: 0 món trong kế hoạch có `food_id` nằm ngoài `candidate_foods` của request.
- **SC-006**: 100% citation trả về có `quote` khớp chunk nguồn; 0 citation không khớp còn sót trong response.
- **SC-007**: 100% response kế hoạch thành công của hai RPC có `disclaimer`.
- **SC-008**: 0 bản ghi dữ liệu người dùng hoặc kế hoạch được tạo mới trong kho dữ liệu sau khi chạy feature.
- **SC-009**: 100% request thiếu trường bắt buộc hoặc sai khoảng giá trị nhận lỗi nghiệp vụ có mã ổn định; 0 request nào nhận giá trị do service tự bịa.
- **SC-010**: 100% buổi tập chỉ dùng hoạt động, thời lượng và thiết bị nằm trong giới hạn caller gửi và hướng dẫn chương 5 khi corpus có dữ liệu; khi corpus rỗng, chỉ dùng dữ liệu caller và ghi `warnings`; 0 bài tập ngoài hai nguồn.
- **SC-011**: 0 response chứa khuyến nghị về thuốc, liều thuốc, chẩn đoán hoặc thay thế điều trị.
- **SC-012**: Ít nhất 95% lượt gọi hai RPC hoàn tất trong deadline cấu hình (mặc định 90 giây). Đây là SLO vận hành; trong phạm vi feature, verify bằng cấu hình deadline + integration smoke, không yêu cầu load harness riêng.
- **SC-013**: Khi corpus rỗng, 100% lượt gọi vẫn trả kế hoạch dựa trên request, `citations` rỗng và `warnings` có `corpus_not_loaded`.
- **SC-014**: Phương án dự phòng cho cùng một request cho cùng một kế hoạch ở 100% lần chạy kiểm thử.

---

## Assumptions

- PR #3 (`feat/recommendation-rag`) đã merge vào `main`; 5 RPC hiện có và kho tri thức RAG đã sẵn sàng. Feature này chỉ bổ sung 2 RPC, không làm lại hạ tầng RAG.
- Caller chịu trách nhiệm tính BMR/TDEE, chốt mục tiêu calo/macro, lấy danh mục món an toàn, gửi `candidate_foods` và lưu kế hoạch. Recommendation không tính mục tiêu, không quản lý danh mục món và không lưu kế hoạch.
- "Kế hoạch tháng" là việc của caller: caller gọi 4–5 lần theo tuần rồi tự lưu; service chỉ biết đơn vị tuần.
- Tuần bắt đầu từ thứ Hai; `week_start_date` là ngày đầu tiên của kế hoạch và các ngày tiếp theo là các ngày liên tiếp.
- `consumed_calories` (nếu có) đi kèm `consumed_date`; rule 105% chỉ áp cho đúng ngày đó khi ngày đó nằm trong tuần. Nếu `consumed_date` ngoài tuần, service bỏ qua rule và ghi `warnings`; nếu `consumed_calories > 0` mà thiếu `consumed_date`, service trả lỗi `recommendation.missing_consumed_date`.
- Danh mục món do caller gửi là nguồn dinh dưỡng có thẩm quyền; service tin giá trị caller gửi và không hiệu chỉnh.
- **`allergies` rỗng** nghĩa là caller khai không có dị ứng đã biết; service không suy ra thêm dị ứng.
- **Khớp allergen** dùng trim + lowercase + Unicode NFC rồi so khớp chính xác từng token; caller (Diet) chịu trách nhiệm dùng cùng bộ từ vựng cho `profile.allergies` và `CandidateFood.allergens`.
- **Lọc `dietary_preference`** dùng lại quy tắc lọc sở thích hiện có của Recommendation từ feature `duck-001-recommendation-rag` (ngưỡng carb cho Keto/LowCarb, quy tắc Vegan/Mediterranean); nếu cần điều chỉnh ngưỡng thì chốt ở `plan.md`, không đổi contract.
- **`CandidateFood.allergens` rỗng** nghĩa là caller khai món không có allergen đã biết; caller chịu trách nhiệm điền đầy đủ, service không tự suy ra thành phần.
- `limitations` rỗng nghĩa là caller chưa cung cấp giới hạn sức khỏe; service không được coi là "khỏe mạnh" cũng không được coi là "bị chấn thương".
- `equipment_available` rỗng nghĩa là không có thiết bị; service chỉ dùng hoạt động không cần thiết bị có căn cứ trong sách.
- `activity_preference` rỗng nghĩa là không có ưu tiên; service dùng hướng dẫn chung của chương 5 và không thêm thiết bị.
- `sessions_per_week = 0` dùng `min(3, days)`; `minutes_per_session = 0` dùng mặc định 30.
- Corpus sách là bản OCR đã đối chiếu một phần; citation có `verified = false` vẫn được trả nhưng service không khẳng định số liệu y tế từ trang chưa đối chiếu.
- Thực đơn hai tuần nằm trong Phụ lục A của tệp chương 12 (`docs/reference/che-do-an-truong-tho/12-chuong-12-cach-de-giu-mai-tuoi-thanh-xuan.md`); hướng dẫn tập luyện nằm ở chương 5; đoạn nước/trà thảo mộc không đường dùng cho `suggestions` nằm ở chương 11.
- `target_calories` và target protein/carbs/fat caller gửi là **mục tiêu ngày**; service nhân với `days` để có tổng tuần rồi chia ngân sách theo ngày/bữa.
- Các tỷ lệ chia ngân sách calo theo bữa là quy tắc tính toán nội bộ; spec chỉ ràng buộc tổng các bữa bằng ngân sách ngày và tổng các ngày bằng mục tiêu ngày × `days`. Tỷ lệ cụ thể sẽ chốt ở `plan.md`.
- Phạm vi ngoài feature: lưu trữ kế hoạch, API REST, rollup tháng, nhắc lịch/thông báo, thiết bị đeo, thanh toán và tích hợp thực tế phía Diet/Progress (owner khác).
- Không tạo `frontend-guide.md` vì feature không đổi bề mặt client; tạo
  `backend-integration-guide.md` cho gRPC; kết luận được ghi trong `tasks.md`.
