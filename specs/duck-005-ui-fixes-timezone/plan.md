# Plan — duck-005

## Phase

1. FE: nav/sub-nav, targets 30/70, chat layout + scroll + continue conversation,
   meal-plan 2 tab, favicon heart-pulse.
2. Meal-plan AI: per-day prompt, retry có feedback, numeric diagnostics log,
   portion scaling, calo gate cứng/macro warning, deadline 90s.
3. Fasting history: `GET /fasting/history` phân trang + UI.
4. Timezone: `BusinessTime` (Shared) + `UtcCalendar` business-local, ADR006,
   `APP_TIMEZONE`, FE `business-date.ts`.
5. Docs + full gate.

## Shared files

- `contracts/*` (không đổi trong đợt này)
- `deployments/docker-compose.yml`, `.env.example`
- `src/Shared/Time/BusinessTime.cs`
- `docs/decisions/006-business-timezone-vietnam.md`

## Rủi ro

- AI quota: per-day + retry tăng số lời gọi; fallback theo ngày vẫn giữ plan dùng được.
- Portion scaling thay đổi khẩu phần hiển thị (hệ số 0.5–1.8), cần ghi rõ trong kế hoạch.
- Dữ liệu cũ quanh 00:00–07:00 giữ nhãn cũ (không backfill, đã chốt).
