# Plan — duck-004 UI/UX & Hỏi sách chat

## Quyết định

| # | Chủ đề | Quyết định |
|---|---|---|
| D1 | Thứ tự | 5 phase, FE quick-wins → BE quick fixes → chat/SSE → AI tips/targets → docs & full gate |
| D2 | Streaming | Gemini `GenerateContentStream` → gRPC server-streaming → Diet SSE → BFF `ReadableStream` |
| D3 | Grounding | Giữ cite-or-abstain; meta-intent trả câu cố định; follow-up dùng history |
| D4 | Lịch sử | Lưu ở `diet_db` (`knowledge_conversations`, `knowledge_messages`) |
| D5 | Target suggestion | gRPC `SuggestNutritionTargets`; calo do code tính, AI tinh chỉnh macro trong khoảng an toàn; user xác nhận mới lưu |
| D6 | Fallback | `usedFallbackReason` additive; UI map lý do → thông điệp + hành động |
| D7 | Điều hướng | `DietSubNav` dùng chung; `/knowledge` có layout shell |
| D8 | Markdown | `react-markdown` + `remark-gfm` + `remark-breaks`, chặn HTML thô |
| D9 | Citation | Icon + popover hover (desktop), bấm mở (touch); bỏ nhãn verified |

## Shared Files & Conflict Hotspots

- `contracts/recommendation/v1/recommendation.proto` (additive fields + 2 RPC)
- `deployments/nginx/nginx.conf` (SSE location)
- `src/Services/Diet/Diet.Infrastructure/Persistence/DietDbContext.cs` + migrations
- `src/Services/Diet/Diet.Application/DependencyInjection.cs`
- `src/Services/Recommendation/Recommendation.Infrastructure/DependencyInjection/KnowledgeStoreRegistration.cs`

## Phase map

1. FE shell/sub-nav/markdown/citation/ngày cảnh báo/nhãn Việt/fasting tolerant.
2. BE 204 fasting, `usedFallbackReason`, `activityLabel`, rationale không "caller".
3. Chat: proto streaming, Recommendation stream use case, Diet SSE + conversations, FE chat UI.
4. AI tips paraphrase + nutrition target suggestion (BE + FE).
5. Docs, guides, full `make check`.

## Rủi ro

- SSE buffer ở nginx/Next → `proxy_buffering off`, `X-Accel-Buffering: no`, BFF stream proxy.
- EF navigation thêm message mới bị đánh dấu `Modified` → `AddMessage` tường minh + test 2 lần SaveChanges.
- AI trả macro ngoài khoảng → clamp + fallback công thức, luôn hiện `source`.
