# Feature Specification: UI fixes, meal-plan reliability, fasting history, timezone +7

**Feature Branch**: `feat/longevia-mvp-completion` (tiếp nối duck-004)
**Created**: 2026-11-09
**Status**: Implemented

## Vấn đề

1. `/diet`: sub-nav chứa "Hỏi sách" (không thuộc `/diet`), navbar chính còn "Gợi ý AI".
2. `/diet/targets`: hai khối xếp dọc, khó quét.
3. `/diet/meal-plan`: AI gần như luôn fallback (`invalid_model_output`), UX trộn đọc/tạo,
   chọn "tuần" nhưng thực chất chọn ngày.
4. `/knowledge`: thiếu title? (đã bỏ theo yêu cầu), khung chat không scroll/auto-scroll,
   mở lại hội thoại cũ rồi gửi tạo hội thoại mới.
5. `/progress/fasting`: chưa có lịch sử.
6. Reset ngày theo UTC (ADR005) sai với người dùng Việt Nam.

## Quyết định

- Meal-plan sinh **theo từng ngày**, log `FailureReason`, co giãn khẩu phần theo ngày,
  chỉ fallback ngày lỗi; calo là gate cứng, macro lệch chỉ cảnh báo.
- Ngày nghiệp vụ theo `APP_TIMEZONE`, mặc định `Asia/Ho_Chi_Minh`; ADR006 thay ADR005;
  không backfill dữ liệu cũ.
- Lịch sử nhịn ăn phân trang toàn bộ (page/pageSize 1..50).
- Targets 30/70 desktop, mobile form trước AI.
- Meal-plan 2 tab Đã lưu / Tạo mới; chọn ngày tự chuẩn hoá thứ Hai.
- Favicon dùng heart-pulse xanh giống header, bỏ asset veggie.

## Acceptance

- Không còn "Hỏi sách" trong `/diet` sub-nav; main nav không còn "Gợi ý AI"; tiêu đề
  trang là "Gợi ý bữa ăn".
- Chat cao cố định, scroll riêng, auto-scroll, gửi tiếp đúng hội thoại đang mở.
- Meal-plan: ngày do AI chọn được dùng khi cấu trúc hợp lệ và calo ±10%; fallback
  theo ngày kèm lý do; deadline riêng 90s.
- Fasting history API + UI phân trang.
- Water/dashboard/meal summary/longevity/digest reset 00:00 giờ Việt Nam.
- Favicon heart-pulse hiển thị trên tab.

## Ngoài phạm vi

Deploy production, Playwright E2E, meal-plan edit/delete.
