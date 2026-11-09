# Tasks — duck-005

- [x] FE nav: bỏ Hỏi sách khỏi `/diet` sub-nav, bỏ "Gợi ý AI" khỏi main nav, đổi
      tên "Gợi ý bữa ăn"; targets 30/70 (mobile form trước).
- [x] FE chat: bỏ title/mô tả, khung cao cố định, scroll riêng, auto-scroll, fix
      gửi tiếp hội thoại cũ.
- [x] FE meal-plan: 2 tab Đã lưu / Tạo mới, chọn ngày tự chuẩn hoá thứ Hai.
- [x] Favicon: `app/icon.svg`, `apple-icon.png`, `favicon.ico` heart-pulse.
- [x] Meal-plan AI: per-day generation, diagnostics log, feedback retry, portion
      scaling, macro warnings, deadline riêng 90s.
- [x] Fasting history API + UI phân trang.
- [x] Timezone ADR006 + `APP_TIMEZONE` + FE business dates.
- [x] Tests: Recommendation 388 unit, Progress 152 integration, Diet 47 integration,
      FE 216 test.
- [x] `make check` full: format PASS, Release build 0 warning/error, **895/895 test**,
      vulnerability scan 28 project sạch.
- [x] `make docker-up`: 13 container Up, smoke water/meal/fasting endpoints OK.
