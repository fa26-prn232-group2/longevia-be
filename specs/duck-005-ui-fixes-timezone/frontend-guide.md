# Frontend Guide — duck-005

**Status**: `READY_FOR_FRONTEND` (đã implement)

## API mới

| Method | Path | Ghi chú |
| --- | --- | --- |
| GET | `/api/progress/fasting/history?page=1&pageSize=20` | Phiên đã kết thúc, `EndedAt desc`; `pageSize` 1..50 |

Response:
```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 3,
  "items": [
    {
      "sessionId": "…",
      "protocol": "16:8",
      "startedAt": "2026-11-08T15:00:00Z",
      "endedAt": "2026-11-09T07:00:00Z",
      "targetHours": 16,
      "actualHours": 16.25,
      "isGoalReached": true,
      "status": "Completed"
    }
  ]
}
```

- Không có phiên đang chạy trong history (dùng `/fasting/current`).
- `page` < 1 → 400 `progress.invalid_page`.

## Thay đổi hành vi

- **Ngày nghiệp vụ (ADR006)**: water/dashboard/meal summary/longevity/digest reset
  00:00 giờ `APP_TIMEZONE` (mặc định Việt Nam). FE gửi `DateOnly` theo ngày local;
  timestamp vẫn ISO UTC.
- Meal plan: mỗi ngày có thể fallback riêng. `warnings[]` có `code`:
  - `ai_fallback_used` (scope = ngày)
  - `macro_target_missed` (scope = ngày; calo đã đạt, macro lệch)
  - `target_not_met` (phương án dự phòng không đạt ±10%)
- Khẩu phần có thể được co giãn theo ngày (hệ số 0.5–1.8) để khớp calo; hiển thị
  `servingSize` đã điều chỉnh.

## UI đã chốt

- Main nav không còn "Gợi ý AI"; `/diet` sub-nav không còn "Hỏi sách".
- `/diet/targets`: desktop 30% AI trái, 70% form phải; mobile form trước.
- `/knowledge`: bỏ tiêu đề/mô tả; khung chat cao cố định, scroll riêng, auto-scroll;
  mở hội thoại cũ và gửi tiếp sẽ nối đúng hội thoại.
- `/diet/meal-plan`: 2 tab Đã lưu / Tạo mới; chọn ngày bất kỳ tự ra tuần thứ Hai.
- `/progress/fasting`: card lịch sử + phân trang.
- Favicon: icon heart-pulse xanh giống logo header.
