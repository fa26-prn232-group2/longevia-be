# Frontend Guide — Longevia MVP completion

**Feature**: `duck-003-longevia-mvp-completion`  
**Status**: `READY_FOR_FRONTEND` cho các surface đã implement; các mục ghi `PENDING` bên dưới chưa xong.

## REST mới

| Method | Path | Ghi chú |
| --- | --- | --- |
| POST | `/api/diet/meal-plans` | Sinh + lưu kế hoạch tuần; body `{ weekStartDate, days, mealsPerDay }` |
| GET | `/api/diet/meal-plans/{weekStartDate}` | Đọc kế hoạch đã lưu |
| GET | `/api/diet/nutrition/nutrient-gaps?days=7` | Phân tích vi chất; có `dataWarnings` khi thiếu dữ liệu |
| POST | `/api/diet/knowledge/ask` | Body `{ question, chapterFilter? }` |
| GET | `/api/diet/knowledge/search?query=&chapterFilter=&topK=` | `topK` 1..20 |
| PUT/DELETE | `/api/diet/foods/{id}` | Chỉ custom food của owner; DELETE là soft delete |
| POST | `/api/progress/workout-plans` | Body `{ weekStartDate, goal, days, sessionsPerWeek, minutesPerSession }` |
| GET | `/api/progress/workout-plans/{weekStartDate}` | Đọc kế hoạch tập |
| GET | `/api/progress/longevity-habits?days=7` | Điểm + breakdown + citations |
| GET/PUT | `/api/identity/users/me/preferences` | `{ receiveWeeklyDigest }`, mặc định false |

## UI đã nối

- `/diet/meal-plan` — kế hoạch ăn tuần.
- `/diet/nutrients` — thiếu hụt vi chất.
- `/knowledge` — hỏi/tra cứu sách.
- `/progress/workout` — kế hoạch tập.
- `/progress/longevity` — điểm thói quen.
- `/profile` — toggle báo cáo tuần.
- Navbar chỉ highlight mục khớp sâu nhất.

## PENDING

- FE sửa/xoá custom food (backend đã có endpoint).
- FE xoá body metric/sleep (backend chưa có endpoint).
- E2E Playwright.

## Lỗi cần hiển thị

`diet.meal_plan_not_found`, `diet.ai_timeout`, `diet.ai_unavailable`, `diet.no_safe_candidate`,
`progress.workout_plan_not_found`, `progress.weight_not_found`, `progress.ai_timeout`.
Luôn hiển thị citation + disclaimer; `verified=false` ghi “chưa kiểm chứng”.
