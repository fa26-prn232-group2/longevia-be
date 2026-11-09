# Backend Integration Guide — duck-004

**Feature**: `duck-004-ui-ux-knowledge-chat`
**Status**: `READY_FOR_BACKEND` (additive, tương thích ngược)

## gRPC mới (RecommendationService)

### `AskLongevityQuestionStream` — server streaming

```proto
rpc AskLongevityQuestionStream (AskLongevityQuestionStreamRequest)
    returns (stream AskLongevityQuestionEvent);

message ConversationTurn { string role = 1; string content = 2; } // "user" | "assistant"
message AskLongevityQuestionStreamRequest {
  string user_id = 1;
  string question = 2;
  UserProfile profile = 3;
  int32 chapter_filter = 4;
  repeated ConversationTurn history = 5; // tối đa client gửi 6 lượt gần nhất
}
message AskLongevityQuestionEvent {
  oneof event {
    string delta = 1;
    Citation citation = 2;
    AskLongevityQuestionDone done = 3;
    AskLongevityQuestionError error = 4;
  }
}
message AskLongevityQuestionDone { bool abstained = 1; string abstain_reason = 2; Disclaimer disclaimer = 3; }
message AskLongevityQuestionError { string code = 1; string message = 2; }
```

Quy ước: `delta` là markdown thuần (không JSON); citation do code gắn từ chunk đã
truy xuất (tối đa 3, đã dedupe theo chương+trang); `done` luôn là event cuối khi
thành công. Lỗi provider giữa chừng → `error` rồi kết thúc; client vẫn giữ phần
`delta` đã nhận. Meta-intent (chào/cảm ơn/ngoài phạm vi) trả delta cố định,
`abstain_reason = meta_intent`, không gọi AI.

### `SuggestNutritionTargets`

```proto
rpc SuggestNutritionTargets (SuggestNutritionTargetsRequest)
    returns (SuggestNutritionTargetsResponse);
```

- Request: `profile`, `goal` (`LOSE_WEIGHT | MAINTAIN | BUILD_MUSCLE | LONGEVITY`),
  `description` (≤500), `current_calories`, `locale`.
- Response: `calories`, `protein_grams`, `carbs_grams`, `fat_grams`, `rationale`,
  `source` (`formula | ai`), `safety_note`.
- Calo luôn do code tính (Mifflin-St Jeor + TDEE × mục tiêu, clamp 1200–4000).
- AI chỉ tinh chỉnh protein/fat; ngoài khoảng an toàn → `source=formula`.
  Carbs luôn tính lại từ calo còn lại.

## Field additive trong message cũ

| Message | Field | Ghi chú |
| --- | --- | --- |
| `GenerateMealPlanResponse` | `used_fallback_reason = 13` | `provider_timeout`, `provider_unavailable`, `invalid_model_output`, rỗng khi không fallback |
| `GenerateWorkoutPlanResponse` | `used_fallback_reason = 10` | thêm `corpus_missing`, `no_safe_activity` |
| `WorkoutPlanDay` | `activity_label = 9` | nhãn tiếng Việt; rỗng khi `rest_day` |

## Phía Diet

- `IKnowledgeProvider.AskStreamAsync` proxy stream gRPC → SSE; map `RpcException`
  sang AppException (`diet.ai_timeout`, `diet.ai_unavailable`, …).
- Lưu hội thoại ở `diet_db`: `knowledge_conversations`, `knowledge_messages`
  (`citations` JSONB). Message mới phải thêm qua `IKnowledgeConversationRepository.AddMessage`
  để EF đánh dấu `Added` (thêm qua navigation có thể bị hiểu là `Modified`).
- Nginx cần location riêng cho `/api/diet/knowledge/ask/stream` với
  `proxy_buffering off`, `X-Accel-Buffering: no`.

## Kiểm thử bắt buộc

- Unit: stream use case (meta-intent, no chunk, medical refusal, delta/citation order, history).
- Unit: target suggestion (AI hợp lệ, AI ngoài khoảng → formula, provider abstain, profile sai).
- Integration: gRPC `used_fallback_reason`, `activity_label`; Diet SSE thủ công qua nginx.
- Regression: hai lần `SaveChanges` cho conversation + message.
