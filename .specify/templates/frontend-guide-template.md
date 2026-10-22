# Frontend Integration Guide — <feature>

> Chỉ tạo guide này khi feature thay đổi HTTP endpoint, event payload hoặc DTO
> client-facing. Lưu tại `specs/<feature>/frontend-guide.md`, tham chiếu contract
> canonical và giữ nội dung có thể đối chiếu, đếm được.

## 1. Summary

- Feature/spec: <path>
- Requirement: <đường dẫn trong `docs/requirements/`>
- Handoff status: <DRAFT | IN_PROGRESS | READY_FOR_FRONTEND | CHANGED | DEPRECATED>
- Canonical contracts: <đường dẫn contract/protobuf/OpenAPI và version>
- Counts: new endpoints N · changed N · removed N · events N · DTOs N

Status rules:

- `DRAFT`: guide đã tạo nhưng contract chưa được chốt.
- `IN_PROGRESS`: contract đã đủ để implement nhưng code/test chưa hoàn tất.
- `READY_FOR_FRONTEND`: contract ổn định, implementation và test evidence đầy đủ, sync checklist hoàn tất.
- `CHANGED`: surface từng ở `READY_FOR_FRONTEND` vừa đổi; giữ trạng thái này
  trong lúc cập nhật contract/implementation/evidence, sau đó chuyển lại
  `READY_FOR_FRONTEND` khi sync checklist hoàn tất. Không cần frontend
  acknowledgement để thực hiện transition này.
- `DEPRECATED`: surface đang bị loại bỏ; ghi rõ replacement và compatibility window.

## 2. Authentication and behavior

- Authentication/authorization: <canonical policy or contract reference>
- Idempotency: <required header/behavior or N/A>
- Pagination: <cursor/offset behavior or N/A>
- Realtime/SSE behavior and REST fallback: <reference or N/A>

## 3. Endpoints

| Method | Path | Status (new/changed/removed) | Before → After (changed only) |
|---|---|---|---|
|  |  |  |  |

Với mỗi endpoint, trỏ tới request/response schema canonical, nêu success/error
status và chỉ thêm ví dụ lấy từ contract hoặc fixture đã verify. Không chép lại
toàn bộ schema bằng văn xuôi.

## 4. Events

| Topic / routing key | Payload summary | Status (new/changed/removed) | Before → After |
|---|---|---|---|
|  |  |  |  |

## 5. Client-facing DTOs

| DTO | Field | Type | Status | Before → After |
|---|---|---|---|---|
|  |  |  |  |  |

Liệt kê mọi field client nhận được để phép đếm có ý nghĩa; liên kết về schema
canonical thay vì biến bảng này thành nguồn contract thứ hai.

## 6. Enums

| Enum | Value | Status (new/changed/removed) |
|---|---|---|
|  |  |  |

## 7. Errors and compatibility

- Problem Details/error codes: <contract references and changed behavior>
- Migration/compatibility: <before → after, rollout window, fallback>
- Test evidence: <contract/integration/e2e commands and results>

## 8. Sync checklist (fill counts before closing the feature)

- [ ] Every `json:"…"` tag the feature's DTOs emit appears above (count: N)
- [ ] Every value of every enum compared against its source constant block, in
      both directions (count: N)
- [ ] Every route compared after normalizing path params (`:param` vs `{param}`)
      (count: N)
- [ ] Every HTTP status claimed above is one the handler can actually return
      (count: N)

Phần không thể đối chiếu phải ghi rõ lý do. Không được đặt
`READY_FOR_FRONTEND` nếu còn ô chưa hoàn tất hoặc số đếm vẫn là `N`. Một lần
đọc lại không tạo ra số liệu không được coi là sync.
