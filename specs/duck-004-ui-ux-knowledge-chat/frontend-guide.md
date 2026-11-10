# Frontend Guide — duck-004

**Feature**: `duck-004-ui-ux-knowledge-chat`
**Status**: `READY_FOR_FRONTEND` (đã implement và verify)

## REST thay đổi / mới

| Method | Path | Ghi chú |
| --- | --- | --- |
| POST | `/api/diet/knowledge/ask/stream` | SSE; body `{ conversationId?: uuid, question: 1..500 ký tự, chapterFilter?: int }` |
| GET | `/api/diet/knowledge/conversations` | `[{ id, title, messageCount, updatedAt }]`, mới nhất trước |
| GET | `/api/diet/knowledge/conversations/{id}` | `{ id, title, messages: [{ id, role, content, citations[], abstainReason, createdAt }] }` |
| DELETE | `/api/diet/knowledge/conversations/{id}` | 204; 404 `diet.conversation_not_found` nếu không thuộc owner |
| POST | `/api/diet/targets/me/suggest` | body `{ description?: string ≤500, goal?: LoseWeight\|Maintain\|BuildMuscle\|Longevity }` |
| GET | `/api/progress/fasting/current` | **204** khi chưa có phiên (trước đây 200 body rỗng) |

### SSE event

```
event: delta      data: { "text": "..." }
event: citation   data: { chapterNumber, chapterTitle, bookPage, pdfPage, quote, verified, sourceFile }
event: done       data: { conversationId, messageId, abstained, abstainReason, disclaimer }
event: error      data: { code, message }
```

Luồng: 0..n `delta` → 0..3 `citation` → đúng một `done` (hoặc `error` rồi dừng).
Không dựa vào `abstainReason` để chặn hiển thị nội dung; chỉ dùng để gợi ý.

### Trường additive trong response cũ

- `MealPlanResponse.usedFallbackReason`: `"" | provider_timeout | provider_unavailable | invalid_model_output`.
- `WorkoutPlanResponse.usedFallbackReason`: như trên + `corpus_missing | no_safe_activity`.
- `WorkoutPlanDayResponse.activityLabel`: nhãn tiếng Việt; rỗng ở ngày nghỉ.

## Hành vi UI đã chốt

- Trích dẫn: `CitationChip` (icon + popover hover; mobile bấm mở), không hiện `verified`.
- Mọi nội dung sách/AI dùng `MarkdownText` (không HTML thô).
- `/knowledge`: 2 tab Hỏi sách (chat) / Tra cứu; kết quả tra cứu sort chương→trang.
- `/diet/*`: `DietSubNav` chung, giữ `SiteHeader` + `SiteFooter`.
- Kế hoạch: cảnh báo có `scope` = ngày được đưa vào card ngày tương ứng.
- Target suggestion không tự lưu; chỉ ghi DB khi người dùng bấm "Lưu thủ công".
- Fallback AI: map `usedFallbackReason` qua `describeAiFallback`, chỉ hiện "Thử tạo lại" khi `retryable`.

## Điều kiện tích hợp

- SSE đi qua BFF `/api/knowledge/ask/stream` (cookie `lv_at`), không gọi trực tiếp Diet từ browser.
- Không cache SSE; nếu sau này thêm proxy/CDN phải tắt buffer.
