# Progress business capabilities và corrective reconciliation / RabbitMQ / UTC

PR #13 bổ sung Fasting, Water, Sleep, Body Metrics và Dashboard trên foundation
Progress đã merge. Corrective này sửa lỗi reconciliation chỉ kiểm checksum nhưng
báo thành công khi chưa đối chiếu database; kiểm chứng transport thật, siết
idempotency và áp dụng Q01 UTC do Bảo phê duyệt cho Progress MVP.

## Thay đổi corrective

- Launcher PowerShell gọi utility .NET chỉ đọc progress_db: validate JSON v1 và
  hash nguyên entry UTF-8 + LF trước DB; đối chiếu contribution và inbox bằng typed
  parameters, batch 500 trong RepeatableRead READ ONLY. Exit 0/2/3/4; không PASS
  khi DB bị skip/lỗi. Extra live records ngoài snapshot không bị xóa hoặc buộc
  total DB=count; capturedAtUtc không phải cursor.
- Consumer bắt buộc EventId=MealLogId, so metadata duplicates, phục hồi partial
  pair hợp lệ atomically và chỉ xử lý SQLSTATE/constraint dedup đã biết; lỗi
  integrity khác rollback/throw. Không đổi migration cũ hoặc public Dashboard DTO.
- RabbitMQ/PostgreSQL Testcontainers thật kiểm tra topic longevia.events, key
  diet.meal.logged, durable queue progress.meal-activity, concurrency/restart,
  kill process khi unacked, broker/DB interruption và retry hữu hạn. Lỗi sau 3
  retry vào MassTransit _error; không đánh đồng với broker DLQ.
- Q01: ngày server-derived, today, Monday week và history boundaries dùng UTC;
  Diet BusinessDate giữ nguyên, không rebucket lịch sử. Broker production yêu cầu
  host/user/password tường minh; chỉ Progress compose block bổ sung cấu hình.

## Kiểm chứng

- Full solution: **733 pass, 0 fail, 0 skip**, 9 assemblies. Progress: **75 unit +
  103 integration**. Format verify, Debug/Release warnings-as-errors và scan cả
  transitive dependencies của 27 projects đều pass. Máy thiếu make nên chạy đúng
  dotnet recipes trong Makefile với MSBuild -m:1.
- Reconciliation: 20 cases chạy process thật và assert exit code, có PG thật,
  malformed/tampered snapshot, missing/mismatched data, DB unavailable, late meal,
  extra live meal và 1.003 entries qua 3 batch.
- Broker: 9 cases dùng transport thật; UTC: 3 deterministic scenarios bao phủ
  midnight 2026-10-24/25, Monday UTC, Vietnam offset, replay và union active dates.
- Harness tách biệt: **1 pass**, actual Diet producer/API/outbox từ PR #12 HEAD
  bc79173d6681ce9964b4badad6b16f5ac1d87472 → RabbitMQ → Progress PG. Actual replay
  và exporter tạo 102 entries, reconciliation matched 102/PASS. Receive context
  xác nhận đúng exchange/key; không cần producer patch trong SHA đã kiểm tra.
- Negative controls đã đỏ khi bỏ canonical-ID guard, ép reconciliation PASS và
  bỏ qua contribution còn thiếu; implementation đã khôi phục trước full gate.

Chi tiết test/môi trường/tái lập: [verification.md](verification.md),
[corrective-reconciliation.md](corrective-reconciliation.md),
[backend-integration-guide.md](backend-integration-guide.md).

## Draft / readiness

Giữ PR **Draft**. Diet PR #12 vẫn chưa merge. Fixture replay/reconciliation không
chứng minh historical bootstrap của dữ liệu triển khai hoặc broker/outbox đã drain.
Dashboard completeness vẫn là rollout blocker, không thêm field chưa phê duyệt.
Q02 Height gRPC runtime chờ Identity; Q10 Weight giữ nguyên scope proposal.

Q01 **APPROVED FOR PROGRESS MVP BY BẢO**; Huy chưa acknowledge cách frontend/Diet
chọn ngày UTC tự động. Việt Nam 00:30 có thể vẫn thuộc ngày UTC trước; user chọn
ngày lịch sử được giữ nguyên. Không claim joint approval. Tin nhắn để Bảo gửi Huy:
[handoffs/huy-q01-utc.md](handoffs/huy-q01-utc.md).

Không merge PR #12/#13, không đổi implementation Diet/Identity/Worker hoặc shared
contracts. Không có migration mới; giữ cả 5 migration và immutable BMI snapshots.

Verdict: CORRECTIVE_PARTIAL_DRAFT_PR.
