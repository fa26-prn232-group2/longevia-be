# Mô hình dữ liệu dự kiến — không phải migration

Chỉ `progress_db`. Tên/kiểu dưới đây là thiết kế đề xuất cho Phase 2–6;
các constraint PENDING phải được duyệt trước tạo schema liên quan. Không có foreign key
sang DB Identity/Diet; UserId là Guid tham chiếu logic. Instant dùng DateTimeOffset UTC.
Quyết định Q tham chiếu [research](research.md).

## BodyMetricLog — US1

| Field dự kiến | Ràng buộc |
| --- | --- |
| Id / UserId | Guid, bắt buộc; user lấy từ chủ thể xác thực. |
| LoggedAt / CreatedAt | Instant UTC bắt buộc; LoggedAt do client ghi, CreatedAt do server; future/backdate Q04. |
| WeightKg | Decimal, bắt buộc, 25–300 kg. Precision/scale schema chờ Q04. |
| BodyFatPercent / WaistCircumferenceCm | Decimal nullable; bounds Q04, không dùng 0 thay null. |
| SystolicBloodPressure / DiastolicBloodPressure / RestingHeartRate | Int nullable; bounds/pair validation Q04. |
| HeightUsedCm / HeightSourceVersion / HeightObservedAt | Snapshot chiều cao + provenance; đề xuất int theo developer, chờ Q02/Nguyên. Missing policy chưa duyệt nên chưa chốt nullable. |
| Bmi / BmiCategory | Snapshot tại lúc ghi; precision/category Q04; không computed động từ profile. |
| ActivityDate / ZoneId | DateOnly và zone snapshot nếu Q01 được duyệt. |

Index dự kiến (UserId, LoggedAt desc, Id desc) phục vụ history/current; count/query
luôn scope UserId. Không unique một metric/ngày vì PRD không giới hạn số lần đo.
Không có update/delete endpoint metric trong scope. BR-05 giữ snapshot cũ bất biến;
không chạy backfill BMI dựa trên chiều cao mới khi đọc.

## FastingSession — US2

| Field dự kiến | Ràng buộc |
| --- | --- |
| Id / UserId | Guid bắt buộc. |
| Protocol | Chỉ 16:8, 18:6, 20:4, Custom; lưu qua domain constant/enum. |
| StartedAt / TargetEndedAt | UTC bắt buộc; target không thay trong phiên; validate Q03. |
| TargetDurationMinutes | Số phút dương; preset 960/1080/1200; Custom bounds/precision Q03. |
| EndedAt / ActualDurationMinutes | Nullable khi active; khi end hợp lệ có giá trị; EndedAt không trước StartedAt. |
| Status / Version | InProgress hoặc Completed; token cạnh tranh/atomic transition theo thiết kế EF. |

Invariant DB: unique filtered index UserId ở các hàng active (EndedAt IS NULL)
để bảo vệ nhiều instance. Domain và persistence phải thống nhất active definition.
State machine: chưa có active → InProgress → Completed; end không chuyển ngược;
vượt target không tự end theo proposal Q03. Completion đạt target là giá trị so sánh
duration, không đồng nhất Completed với IsGoalReached. End/start cạnh tranh phải
có linearization rõ trong transaction, test kết quả DB thay vì chỉ response.

## WaterLog / SleepLog — US3

- WaterLog: Id, UserId, AmountMl, LoggedAtUtc, ActivityDate/ZoneId theo Q01.
  AmountMl dương; đề xuất int 1–5000 (Q05 chưa duyệt). POST hiện chỉ amountMl,
  thời điểm ghi do server; không tự thêm backdate vào client contract.
  Lưu append log rồi SUM theo user/ngày, tránh read-modify-write mất cập nhật.
- WaterTarget: chưa tạo bảng nếu Q05 chọn constant 2500; nếu chọn user target,
  cần contract và quyết định hiệu lực theo ngày trước schema, không tự mở rộng scope.
- SleepLog: Id, UserId, SleptAtUtc, WokeAtUtc, SleepQuality, DurationMinutes,
  ActivityDate/ZoneId theo Q01/Q05. Quality: Poor, Fair, Good, Excellent.
  End > start là proposal validation; đề xuất tối đa 24h và không overlap chờ Q05.
  Chống overlap cạnh tranh cần constraint/transaction thích hợp sau khi policy được duyệt,
  không chỉ validator query-before-write. Duration từ instant kể cả DST.

Index dự kiến WaterLog (UserId, ActivityDate), SleepLog (UserId, WokeAtUtc).
Không unique user/day đối với sleep trước khi chọn single/multiple sleep policy.

## Activity projection — US4 / BLOCKED Q09

Đề xuất ActivityContribution: UserId, Source (Metric/Meal), SourceLogId, Revision,
ActivityDate, IsDeleted và LastEventId. Khóa (Source, SourceLogId) cùng owner scope;
meal lưu tombstone revision để late upsert không hồi sinh log đã xóa. ActivityDay
là read model từ các contribution còn sống; unique (UserId, ActivityDate) nếu materialize.
Ngày có nhiều source chỉ đếm một; xóa contribution không xóa contribution khác.
Metric contribution cập nhật cùng transaction metric; meal projection + inbox cùng
transaction Progress DB. Revision cũ bỏ qua, unknown optional field bỏ qua an toàn.
Date/revision chưa có canonical contract nên chưa tạo consumer/schema chính thức.

Đề xuất InboxReceipt: consumer + EventId unique, state commit cùng projection;
retry lỗi rollback receipt. Backfill có checkpoint/watermark/revision và trạng thái
completeness theo hợp đồng được duyệt, không dùng thời điểm nhận làm ngày nghiệp vụ.

## Retry / outbox / dashboard

- RequestReceipt nếu Q03 chọn idempotency key: UserId, Operation, Key, PayloadHash,
  ResponseStatus/Body, ExpiresAt; unique (UserId, Operation, Key), commit cùng phiên.
  Retention 24h chỉ là đề xuất; không thêm key vào wire trước duyệt.
- MassTransit inbox/outbox chỉ tạo cho contract async đã approved; publish trong
  transaction DB nghiệp vụ. Phase 1 không thêm event, bảng hoặc migration.
- Dashboard là read DTO, không entity cross-service; dùng snapshot metric, tổng
  fasting/sleep và projection active day. Null/empty, cutoff tuần, baseline và
  completeness phải chờ Q06/Q09. Không giả số đo 0 hay streak hoàn chỉnh từ metrics-only.

## Migration và test

Phase 2 chỉ tạo migration phần foundation đã được chốt; thêm bảng từng story ở phase
tương ứng, tránh sinh schema dựa trên Q chưa duyệt. Mỗi service chỉ một feature mở
tạo migrations; resolve/registry và đồng bộ main theo quyền developer trước mỗi lần.
Auto MigrateAsync trước nhận request, lỗi fail-fast, không chỉnh DB bằng tay.
DB integration test dùng PostgreSQL thật, broker test dùng RabbitMQ thật; không EF InMemory
để chứng minh constraint cạnh tranh. Test snapshot BMI, filtered unique, ownership và
rollback/outbox phải assert số hàng/giá trị cụ thể.
