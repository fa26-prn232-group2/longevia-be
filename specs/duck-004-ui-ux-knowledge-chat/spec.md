# Feature Specification: UI/UX polish & Hỏi sách chat

**Feature Branch**: `feat/longevia-mvp-completion` (tiếp nối duck-003, không tách nhánh)

**Created**: 2026-11-09

**Status**: Implemented

**Input**: Góp ý UI/UX sau demo MVP: markdown + citation gọn, trang Hỏi sách thành chat streaming có lịch sử, điều hướng Dinh dưỡng thống nhất, giải thích chỉ số mục tiêu + gợi ý bằng AI, sửa lỗi trạng thái nhịn ăn, Việt hoá nhãn workout/longevity, mẹo trường thọ do AI diễn giải.

## User Scenarios & Testing

### US1 — Trích dẫn & markdown nhất quán (P1)

Người dùng đọc câu trả lời/trích dẫn ở mọi màn hình dưới dạng markdown; nguồn sách chỉ hiện icon + trang, hover/bấm mới xem trích đoạn; không còn nhãn "đã xác minh / chưa kiểm chứng".

**Acceptance**
1. Mọi nội dung sách/AI (knowledge answer, snippet, meal-plan rationale/suggestion, suggestions tip, nutrient citations, workout/longevity citations) render markdown, không lộ ký tự `**`/`#`.
2. HTML thô trong dữ liệu không thực thi (không `rehype-raw`).
3. Tra cứu sách sắp theo chương rồi trang tăng dần và dedupe.

### US2 — Hỏi sách là chat có lịch sử (P1)

Người dùng trò chuyện nhiều lượt với trợ lý; câu trả lời stream từng đoạn; hội thoại được lưu và mở lại; chào hỏi/cảm ơn không bị abstain khô khan.

**Acceptance**
1. `POST /api/diet/knowledge/ask/stream` trả SSE `delta → citation → done`; token đầu tiên tới trước khi câu trả lời hoàn tất.
2. `GET /knowledge/conversations` trả danh sách kèm `messageCount`; mở lại thấy đúng tin nhắn + trích dẫn.
3. Câu follow-up ngắn dùng ngữ cảnh lượt trước để truy xuất.
4. Câu hỏi y tế bị chặn trước khi gọi AI; câu ngoài sách trả lời thân thiện, không bịa.

### US3 — Điều hướng & nội dung khu Dinh dưỡng (P2)

Người dùng chuyển giữa các trang `/diet/*` bằng sub-nav chung, luôn có header/footer; `/knowledge` cũng nằm trong shell chung với tên "Hỏi sách".

**Acceptance**
1. `/diet/*` có `DietSubNav` sticky, active đúng trang sâu nhất.
2. Không còn nút "Về nhật ký" rời rạc; `/knowledge` có header/footer.

### US4 — Mục tiêu dinh dưỡng dễ hiểu + gợi ý AI (P2)

Người dùng không chuyên hiểu BMR/TDEE/P/C/F nhờ tooltip ngắn; có thể mô tả mong muốn để AI đề xuất chỉ số, xem giải thích, rồi mới lưu.

**Acceptance**
1. Mỗi chỉ số có tooltip 1 câu.
2. `POST /targets/me/suggest` không ghi DB; trả calo/macro + `rationale` + `source (ai|formula)` + `safetyNote`.
3. Chỉ số AI ngoài khoảng an toàn → dùng công thức (`source=formula`); calo luôn do code tính.
4. UI điền form nhưng chỉ ghi DB khi người dùng bấm "Lưu thủ công".

### US5 — Trạng thái & ngôn ngữ chính xác (P2)

`/progress/fasting` không báo lỗi khi chưa có phiên; workout/longevity không lộ key tiếng Anh hay câu máy móc; lý do fallback AI rõ ràng.

**Acceptance**
1. `GET /fasting/current` chưa có phiên → 204; UI hiện form bắt đầu phiên.
2. Kế hoạch có `usedFallbackReason`; UI hiện đúng nguyên nhân và chỉ cho "Thử tạo lại" khi phù hợp.
3. Ngày tập trả `activityLabel` tiếng Việt; rationale không còn "caller"; mẹo trường thọ do AI diễn giải.

## Out of scope

- Deploy/cloud/CD/TLS production.
- Playwright E2E (backlog D-04).
- Sửa/xoá meal plan (backlog).

## Success criteria

- Không còn chuỗi "đã xác minh/chưa kiểm chứng" trong UI.
- Chat stream hoạt động qua nginx, reload còn lịch sử.
- `make check` xanh, FE `tsc/test/build` xanh.
