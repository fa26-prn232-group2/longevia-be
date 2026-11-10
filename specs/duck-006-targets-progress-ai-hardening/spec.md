# Feature Specification: targets, Progress UX, meal-plan AI-only

**Owner:** duck
**Branch:** `feat/longevia-mvp-completion`
**Created:** 2026-11-10
**Status:** Implemented

## Vấn đề

1. `PUT /diet/targets/me` xoá snapshot BMR/TDEE, nên “Lưu thủ công” làm mất chỉ số
   trong khi “Tính tự động” lại có.
2. `/progress/water` và `/progress/sleep` không xem lại được lịch sử.
3. `/progress/workout`: warning kỹ thuật về giới hạn sức khỏe, không có chỗ nhập/lưu,
   không tự tải kế hoạch đã lưu.
4. `/diet/meal-plan`: timeout client 15s gây báo lỗi dù server đã lưu; kế hoạch rơi vào
   fallback; prompt chưa đủ chặt.
5. Không có UI chọn tuần trực quan ở meal-plan/workout.

## Quyết định

- `PUT /targets/me` tự tính BMR/TDEE khi có hồ sơ + cân nặng; thiếu dữ liệu vẫn lưu.
- Thêm lịch sử nước (tổng theo ngày) và lịch sử giấc ngủ (phân trang).
- Đưa giới hạn sức khỏe vào hồ sơ Identity; trang tạo plan prefill và cho chỉnh.
- Meal-plan **chỉ dùng AI**, bỏ hoàn toàn fallback; lỗi trả 503 để thử lại.
- Giữ model `gemini-3.5-flash-lite`; tăng chất lượng bằng prompt/schema/data.
- `WeekPicker` dùng chung cho meal-plan và workout.
- Giữ nguyên rule 105% (PRD FR-004/BR-03).

## Phạm vi kỹ thuật

- Identity: `health_limitations` (field 7 gRPC) + migration.
- Diet: PUT target đính kèm snapshot; `POST /targets/me/calculate` legacy.
- Progress: `GET /water/history`, `GET /sleep/history`; workout input echo + upsert metadata.
- Recommendation: bỏ fallback meal-plan; retry 3; prompt nhóm theo bữa; corpus top 3;
  schema 3–5 món; `CandidateFood.category`.
- FE: WeekPicker, lịch sử nước/ngủ, hồ sơ giới hạn sức khỏe, timeout 150s.

## Ngoài phạm vi

- Đổi model AI, deploy production, Playwright E2E, meal-plan edit/delete.
- Rule giới hạn sức khỏe khác ngoài `joint_pain`.
