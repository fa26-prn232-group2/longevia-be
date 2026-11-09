# Backend Integration Guide — duck-005

**Status**: `READY_FOR_BACKEND`

## Thay đổi contract

Không đổi gRPC/proto trong đợt này. Chỉ mở rộng REST Progress:

- `GET /api/progress/fasting/history?page&pageSize` → `FastingHistoryPageResponse`
  (`page`, `pageSize`, `totalCount`, `items[]`).
- `IFastingRepository.GetCompletedPageAsync` + `CountCompletedAsync`.

## Hành vi meal-plan (Recommendation)

- `GenerateMealPlan` sinh **theo từng ngày** (1 prompt/ngày, tối đa 40 candidate),
  retry 1 lần với feedback tổng calo/macro.
- Sau khi parse, code co giãn khẩu phần cả ngày theo hệ số `targetCalories / actual`
  nếu hệ số nằm trong `[0.5, 1.8]`; ngoài khoảng → retry/fallback ngày đó.
- Validator:
  - Cứng: cấu trúc ngày/bữa/số món/candidate/allergen + **calo ±10%**.
  - Mềm: protein/carb/fat lệch → warning `macro_target_missed` (không fallback).
- Lý do fallback ghi log Warning kèm `FailureReason`, `AbstainReason`, traceId,
  số candidate, độ dài prompt, attempt.
- `used_fallback_reason`: `provider_timeout | provider_unavailable | invalid_model_output`.
- Diet dùng `Recommendation__MealPlanDeadlineMilliseconds` (mặc định 90s) cho meal
  plan; các call khác vẫn 20s.

## Timezone (ADR006)

- `Longevia.Shared.Time.BusinessTime` + `Progress.Domain.UtcCalendar` resolve
  `APP_TIMEZONE` (mặc định `Asia/Ho_Chi_Minh`) khi host khởi động.
- Timestamp vẫn UTC; chỉ ngày/tuần/digest/streak đổi.
- Không backfill; dữ liệu cũ giữ nhãn cũ.

## Kiểm thử

- Recommendation unit: per-day fallback có scope, validator calo cứng/macro mềm.
- Progress integration: water rollover 00:00 VN, weekly start Monday VN, sleep date.
- Diet integration: daily summary mặc định theo ngày VN.
- Fasting history: start→end→history + phân trang.
