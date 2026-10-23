# Backend Integration Guide — <feature>

> Chỉ tạo guide này khi feature thay đổi contract gRPC, event payload/topology
> hoặc hành vi tích hợp service-to-service. Lưu tại
> `specs/<feature>/backend-integration-guide.md`. Contract canonical vẫn là file
> trong `contracts/`; guide chỉ ghi delta/handoff và evidence, không chép lại
> toàn bộ schema.

## 1. Summary

- Feature/spec: <path>
- Requirement: <đường dẫn trong `docs/requirements/`>
- Handoff status: <DRAFT | IN_PROGRESS | READY_FOR_BACKEND | CHANGED | DEPRECATED>
- Canonical contracts: <đường dẫn protobuf/event schema và version>
- Counts: new RPCs N · changed N · removed N · events N · messages N · fields N · enum values N

Status rules:

- `DRAFT`: guide đã tạo nhưng contract chưa được chốt.
- `IN_PROGRESS`: contract đã đủ để implement nhưng code/test chưa hoàn tất.
- `READY_FOR_BACKEND`: contract ổn định, implementation và test evidence đầy đủ,
  sync checklist hoàn tất.
- `CHANGED`: surface từng ở `READY_FOR_BACKEND` vừa đổi; giữ trạng thái này
  trong lúc cập nhật contract/implementation/evidence, sau đó chuyển lại
  `READY_FOR_BACKEND` khi sync checklist hoàn tất.
- `DEPRECATED`: surface đang bị loại bỏ; ghi rõ replacement và compatibility window.

## 2. Consumers and transport

| Producer | Consumer | Transport | Canonical contract | Sync/async | Network boundary |
|---|---|---|---|---|---|
|  |  | gRPC / event / internal HTTP |  |  |  |

Chỉ ghi internal HTTP nếu kiến trúc hiện hành cho phép; nếu không, ghi `N/A` và
lý do.

## 3. gRPC

### 3.1 Method matrix

| Service | Method | Request | Response | Status (new/changed/removed) | Caller → Callee | Deadline |
|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |

### 3.2 Call behavior

- Address/service discovery: <ví dụ `recommendation:50051` trong network nội bộ>
- Auth/authorization: <policy hoặc `none` kèm lý do>
- Deadline/timeout: <giá trị và cấu hình>
- Retry: <điều kiện, số lần, backoff hoặc `none`>
- Idempotency: <khóa/cơ chế hoặc `N/A`>
- Reflection: <enabled/disabled; client generated từ proto hay dùng `-proto`>

### 3.3 Errors and metadata

| Code / trailer | gRPC status | Điều kiện | Caller xử lý thế nào |
|---|---|---|---|
|  |  |  |  |

Metadata response: <field/khóa và ý nghĩa hoặc `N/A`>.

## 4. Events

| Exchange | Routing key | Queue | Event | Producer | Consumer | Status (new/changed/removed) |
|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |

- Payload canonical: <đường dẫn schema/contract>
- Ordering: <có/không, theo khóa nào>
- Idempotency: <khóa/cơ chế>
- Outbox: <có/không, bảng/transaction>
- Retry/DLQ: <policy>
- Versioning/compatibility: <before → after, rollout window>

## 5. Internal HTTP

- Trạng thái: <N/A | danh sách endpoint>
- Nếu có: auth, idempotency, status/error, compatibility

## 6. Compatibility and rollout

- Proto field number: <field mới dùng số nào; field bỏ phải `reserved`>
- Event schema: <thêm field optional / version mới / consumer bỏ qua field lạ>
- Before → after: <tóm tắt thay đổi>
- Rollout/fallback: <thứ tự deploy, feature flag, compatibility window>

## 7. Test evidence

- Contract/integration test: <file + lệnh chạy + kết quả>
- Manual smoke: <lệnh `grpcurl`/producer-consumer + kết quả>
- Không chạy được phần nào: <lý do>

## 8. Sync checklist (fill counts before closing the feature)

- [ ] Every RPC in the canonical proto appears above with request/response
      (count: N)
- [ ] Every documented message field and enum value compared against the
      canonical proto in both directions (count: N)
- [ ] Every error code/trailer and deadline compared against implementation
      (count: N)
- [ ] Every event, routing key, queue and producer/consumer pair compared
      against canonical contract/topology (count: N)
- [ ] Every claimed test evidence was actually run and its result recorded
      (count: N)

Phần không thể đối chiếu phải ghi rõ lý do. Không được đặt
`READY_FOR_BACKEND` nếu còn ô chưa hoàn tất hoặc số đếm vẫn là `N`.
