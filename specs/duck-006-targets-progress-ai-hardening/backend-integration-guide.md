# Backend Integration Guide — duck-006

**Status:** `READY_FOR_BACKEND`

## Contract thay đổi

- `contracts/identity/v1/identity.proto`: `GetUserProfileResponse.health_limitations = 7`
  (`repeated string`, text tự do). Tương thích ngược.
- `contracts/recommendation/v1/recommendation.proto`: `CandidateFood.category = 11`;
  `WorkoutPlanDay.activities = 10` (1–3 hoạt động/buổi).
- REST Progress: thêm `GET /water/history`, `GET /sleep/history`, `GET /sleep/today`;
  workout request/response thêm `activityPreference/equipmentAvailable/limitations/activities`.
- REST Diet: `DailySummaryResponse.Meals[].items[]` trả món đã ăn.
- REST Identity: profile có `healthLimitations` text tự do.

## Hành vi quan trọng

- `PUT /targets/me`: tính BMR/TDEE nếu có profile + weight; lỗi provider bị nuốt để không
  chặn lưu; nếu không tính được thì tái dùng snapshot cũ.
- Meal-plan: **không fallback**; hết 3 lần thử trả `recommendation.plan_generation_failed`
  (503). Prompt gửi candidate theo nhóm bữa, khoảng calo bữa, corpus tối đa 3 chunk/600 ký tự.
- `food_indices`: 3–5 món/bữa; validator vẫn giữ calo ±10% là gate cứng, macro lệch chỉ warning.
- gRPC trailer `title` phải ASCII; title tiếng Việt bị bỏ để tránh HTTP 500.

## Test evidence

- Recommendation: 381 unit, 66 integration.
- Diet: 49 integration, 19 unit.
- Progress: 115 unit, 154 integration.
- Identity: 70 unit, 22 integration.
