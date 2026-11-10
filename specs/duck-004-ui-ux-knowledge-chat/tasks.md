# Tasks — duck-004

Trạng thái: hoàn thành các phase đã chốt (deploy/Playwright vẫn backlog).

## Phase 1 — FE quick wins

- [x] `MarkdownText`, `CitationChip(List)`, `InfoTooltip`, `DietSubNav`, `knowledge/layout.tsx`.
- [x] Bỏ nhãn verified, sort/tra cứu theo chương+trang, dedupe.
- [x] Warning meal plan gắn theo ngày (`partitionWarnings`).
- [x] Nhãn tiếng Việt workout/longevity; tooltip P/C/F, BMR/TDEE.
- [x] Fasting mapper coi body rỗng/204 là "không có phiên".
- Gate: `tsc`, 205 test, build 22 route.

## Phase 2 — BE quick fixes

- [x] `GET /fasting/current` → 204.
- [x] `usedFallbackReason` (meal/workout) + `activityLabel`.
- [x] Rationale bỏ "caller", dùng nhãn tiếng Việt.
- Gate: `make lint`, Recommendation 378 unit + 66 integration, Progress 152, Diet 46.

## Phase 3 — Hỏi sách chat + SSE

- [x] gRPC `AskLongevityQuestionStream` + `ConversationTurn`.
- [x] Recommendation `IChatStreamModel`/`AskStreamingUseCase` (meta-intent, retrieval, citation 3 đoạn, history).
- [x] Diet SSE `POST /knowledge/ask/stream`, conversations GET/DELETE.
- [x] Migration `AddKnowledgeConversations`; `AddMessage` tường minh + test.
- [x] Nginx SSE location; FE BFF route + `useKnowledgeChat` + UI chat.
- Verify: SSE thật qua nginx trả `delta → citation → done`, `messageCount=2`.
- Gate: Recommendation 383 unit/66 integration, Diet 19 unit/47 integration, FE 216 test.

## Phase 4 — AI tips & target suggestion

- [x] Prompt `LongevityTip` diễn giải chunk; fallback curate.
- [x] gRPC `SuggestNutritionTargets` + validator khoảng an toàn + fallback công thức.
- [x] Diet `POST /targets/me/suggest` (không ghi DB).
- [x] FE khối "Gợi ý bằng AI" + điền form + xác nhận lưu.
- Gate: Recommendation 387 unit/66 integration, Diet 47 integration, FE 217 test.

## Phase 5 — Docs & close-out

- [x] `docs/api/api-spec.md` §7.4–7.5.
- [x] Guide FE/BE trong spec này.
- [x] `make check` full: format PASS, Release build 0 warning/error, **894/894 test**, vulnerability scan 28 project sạch.

## Backlog

- Playwright E2E (D-04), deploy production, meal plan edit/delete.
