# Frontend Guide — duck-006

**Status:** `READY_FOR_FRONTEND`

## API mới/đổi

| Method | Path | Ghi chú |
| --- | --- | --- |
| PUT | `/api/diet/targets/me` | Tự đính kèm `bmr`/`tdee` khi có hồ sơ + cân nặng |
| GET | `/api/progress/water/history?days=30` | Tổng theo ngày, `days` 1..90, cũ → mới |
| GET | `/api/progress/sleep/history?page=1&pageSize=10` | Mới nhất trước |
| PUT | `/api/identity/users/me/profile` | Thêm `healthLimitations: string[]` |
| POST | `/api/progress/workout-plans` | Thêm `activityPreference`, `equipmentAvailable`, `limitations` |

`healthLimitations` hiện chỉ nhận `joint_pain`.

## Meal-plan

- Bỏ fallback: response không còn kế hoạch dự phòng; lỗi AI trả 503
  `recommendation.plan_generation_failed` (FE hiển thị thử lại).
- `warnings` chỉ còn cảnh báo dinh dưỡng/corpus/rule 105%, không còn
  `ai_fallback_used`/`target_not_met`.
- Generate có thể mất 30–90 giây; FE phải đặt timeout riêng ≥150 giây.

## Gợi ý UI đã áp dụng

- `WeekPicker` dùng chung: lùi/tới tuần + lịch tháng, chọn ngày tự về thứ Hai.
- Water/sleep có card lịch sử; sleep cho xoá bản ghi.
- Hồ sơ có checkbox giới hạn sức khỏe; trang workout prefill từ hồ sơ và cho chỉnh.
