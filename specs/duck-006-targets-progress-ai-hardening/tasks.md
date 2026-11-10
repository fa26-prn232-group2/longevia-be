# Tasks — duck-006

- [x] Diet: `PUT /targets/me` tự tính BMR/TDEE, fallback snapshot cũ, không chặn lưu.
- [x] Progress: `GET /water/history?days`, `GET /sleep/history?page&pageSize`.
- [x] Identity: `healthLimitations` + migration `AddHealthLimitations` (default `'{}'`).
- [x] Progress workout: limitations mặc định từ hồ sơ, echo input, upsert metadata,
      message warning thân thiện.
- [x] Recommendation meal-plan: bỏ `MealPlanFallbackSelector`, lỗi
      `recommendation.plan_generation_failed`, retry 3 với feedback theo bữa.
- [x] Prompt/schema: nhóm món theo bữa, khoảng calo bữa, 3–5 món, corpus top 3/600 ký tự.
- [x] gRPC: `CandidateFood.category`; sửa trailer title non-ASCII gây HTTP 500.
- [x] FE: WeekPicker, lịch sử nước/ngủ, hồ sơ giới hạn sức khỏe, workout prefill,
      timeout 150s, bỏ UI fallback meal-plan.
- [x] Tests: Recommendation 381 unit + 66 integration; Diet 49 integration + 19 unit;
      Progress 115 unit + 154 integration; Identity 70 unit + 22 integration; FE 221 test.
- [x] `make lint` + `make check` PASS: 892/892 test, vulnerability scan 28 project sạch.
- [x] Commit 1 lần/repo; chưa push.

## Contract impact

- REST: `PUT /targets/me` hành vi mới; thêm 2 endpoint lịch sử; workout request/response
  thêm input; profile thêm `healthLimitations`.
- gRPC: `GetUserProfileResponse.health_limitations = 7`; `CandidateFood.category = 11`.
- Event: không đổi.
