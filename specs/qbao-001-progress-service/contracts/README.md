# Đề xuất API và hợp đồng phụ thuộc — chưa được phê duyệt

Nguồn hiện hành: [API spec §4](../../../docs/api/api-spec.md), [PRD §2.3](../../../docs/requirements/prd.md).
Các shape dưới đây chỉ nằm trong feature; Phase 1 không thêm/sửa canonical contracts.
`Qxx` tham chiếu [sổ quyết định](../research.md). Field có dấu `?` là đề xuất nullable,
không có nghĩa schema chuẩn hiện tại đã cho phép null.

## REST inventory

Mọi route gateway bắt đầu `/api/progress`; service route bỏ prefix đó theo Nginx.
Tất cả 9 route yêu cầu Bearer ES256 và chỉ dữ liệu user hiện tại. Response JSON camelCase,
UTC instant ISO 8601; ngày lịch không phải instant. Không nhận userId để chọn chủ sở hữu.

| Route gateway | Request / query | Response / căn cứ | Phần chờ duyệt |
| --- | --- | --- | --- |
| POST /api/progress/metrics | loggedAt, weightKg; bodyFatPercent?, waistCircumferenceCm?, systolicBloodPressure?, diastolicBloodPressure?, restingHeartRate? | 201: id, loggedAt, weightKg, bmi, bmiCategory và 5 optional metric field; API §4.1 | Q02 height/missing; Q04 validation/precision; Location URI không tự trỏ route detail chưa tồn tại. |
| GET /api/progress/metrics/history | fromDate?, toDate?, pageNumber=1, pageSize=20 | 200: collection metrics; API §4.2 | Q01 filter date vs instant; Q07 wire pagination. |
| POST /api/progress/fasting/start | protocol, startedAt, targetHours | 201: sessionId, protocol, startedAt, targetEndedAt, status=InProgress; API §4.3 | Q03 target/retry/time rules. |
| POST /api/progress/fasting/end | sessionId, endedAt | 200: sessionId, actualHours, targetHours, isGoalReached, status=Completed; API §4.4 | Q03 retry/status; Q08 ownership. |
| GET /api/progress/fasting/current | không có body | PRD có route, API thiếu schema; đề xuất 200 object hoặc null, object: sessionId, protocol, startedAt, targetEndedAt, targetHours, elapsedHours, remainingHours, status | Q03 shape/precision/no-active. |
| POST /api/progress/water/log | amountMl | 200: date, consumedMl, targetMl, percentComplete; API §4.5 | Q01 day; Q05 target/type/limits. Không tự thêm loggedAt vào body. |
| GET /api/progress/water/today | không có body | PRD có route, API thiếu schema; đề xuất 200 cùng shape water/log | Q01/Q05. |
| POST /api/progress/sleep/log | đề xuất sleptAt, wokeAt, sleepQuality | PRD có route, API thiếu schema; đề xuất 201: id, sleptAt, wokeAt, sleepQuality, durationHours, date | Q01/Q05 field names/type/status, overlap/day. |
| GET /api/progress/dashboard/summary | không có body | 200: currentWeightKg, initialWeightKg, weightDeltaKg, currentBmi, currentStreakDays, bestStreakDays, fastingThisWeekCompleted, averageSleepHours; API §4.6 | Q06 null/windows; Q09 completeness; delta tuần trước theo PRD. |

API mô tả chi tiết 6 route, PRD liệt kê 9; 3 route cần schema: current fasting,
water today, sleep log. Không thêm endpoint chỉnh target nước, sửa/xóa metric hoặc
lịch sử fasting vào scope chỉ vì persistence hỗ trợ được.

## Validation / errors / pagination

- weightKg bắt buộc 25.0–300.0; protocol thuộc `16:8`, `18:6`, `20:4`, `Custom`.
- Quality theo PRD: `Poor`, `Fair`, `Good`, `Excellent`; biomarker limits, BMI enum,
  timestamp policy và Custom bounds chỉ là đề xuất Q03–Q05, chưa là accepted contract.
- Input invalid theo system-design: 400 `validation.failed`, Problem Details gồm
  type/title/status/detail/code/errors/traceId; không rải logic lỗi trong endpoint.
- 401 token không hợp lệ (tuân thủ Q08-A strict JWT validation đã approved: ES256, kid hợp lệ, sub Guid);
  404 ownership hiding (Q08-B PENDING cho business endpoints), 409 active/retry conflict (Q03),
  422 thiếu chiều cao (Q02), 503 dependency unavailable (Q09) là các đề xuất cần chốt khi triển khai
  phase nghiệp vụ tương ứng. Mã Progress cụ thể cần catalog được duyệt trước code, không tái dùng mã Identity.
- Nội bộ `PagedResult<T>`: items, page, pageSize, totalItems, totalPages,
  hasPreviousPage, hasNextPage. API wire hiện dùng pageNumber/totalCount.
  Q07 đề xuất DTO adapter cục bộ, không sửa Shared; Count trước Skip/Take.

## Height integration — Q02 APPROVED HƯỚNG gRPC READ (Nguyên + Bảo)

[identity.proto](../../../contracts/identity/v1/identity.proto) hiện chưa có RPC method; schema cụ thể PENDING trước Phase 5.
REST `/users/me` có height nhưng Progress tuyệt đối không gọi HTTP nội bộ.
Event `UserProfileUpdatedEvent` (PR #10) chỉ có `UserId` và `OccurredOn`, không có height và không thay thế cho gRPC read.

**Quyết định kiến trúc đã thống nhất**:
- Giao thức: gRPC read nội bộ đồng bộ; Identity sở hữu provider, Progress sở hữu consumer.
- Kiểu dữ liệu: `decimal`, đồng bộ với PR #10 của Identity (`HeightCm` dạng `decimal`, cột DB `numeric(5,2)`, validation 50.00–250.00 cm).
- Snapshot bất biến: Chiều cao nhận được dùng để tính BMI và lưu snapshot (`HeightUsedCm`, `Bmi`, `BmiCategory`) trong `BodyMetricLog`. Thay đổi chiều cao profile sau này trong Identity KHÔNG làm thay đổi BMI lịch sử (BR-05). Không recalculate khi đọc history.
- Tên làm việc RPC: `GetBodyProfile` (request `userId`; response `userId`, `heightCm` decimal, `hasProfile`/`hasHeight`, `profileVersion`/`updatedAt`). Cần hoàn thiện schema protobuf, deadline (đề xuất default 2s), retry policy (tối đa 1 read retry), và error handling (422 khi thiếu height, 503 khi unavailable) trước khi triển khai Phase 5.
- Hiện trạng:
  - Hướng kiến trúc gRPC read: **APPROVED**.
  - Protobuf schema trong `identity.proto`: **PENDING** trước Phase 5.
  - Runtime provider implementation: **NOT YET AVAILABLE** trong Identity Service.
  - Phase 5 integration: **NOT YET COMPLETE**; không tạo RPC giả hay adapter trong Phase 2.
- Progress adapter chỉ map contract-native DTO, không reference `Identity.Application`, không đọc trực tiếp DB của Identity.
- Phương án event snapshot chiều cao: Không chọn do nguy cơ stale data và phức tạp hóa outbox/bootstrap; hướng gRPC read đã được chọn chính thức.

## Meal activity — B / BLOCKED, PENDING INTEGRATION Huy

Diet tại origin/main `418d41b8` đã có Meal Logging create-only, catalog, manual
targets, daily summary, EF diet_db/auth/tests/Docker. Chưa có canonical meal event
hoặc producer transactional outbox cho streak; Progress chưa có consumer/projection/
reconciliation/bootstrap hoặc E2E Diet → Progress. Không tự thêm event vào Shared/contracts.

**Semantics hiện có**: MealLog.Date là DateOnly client cấp qua LogMealRequest;
CreatedAt do server lấy UTC. Summary mặc định ngày UTC khi không truyền date.
POST /meals tạo log mới; không có REST update/delete meal trong PR #8.
Streak dùng meal business date, không suy từ CreatedAt/publish/receive timestamp.
ProcessedAt UTC ghi khi xử lý chỉ dùng ordering/audit, không gán ngày ăn.
Q01 cần duyệt cách metric activity date/user timezone tương thích DateOnly Diet;
không tự lấy UTC hay timezone máy làm policy chung.

**MVP đề xuất chưa duyệt**: create-only event versioned với eventId, userId,
mealLogId, businessDate, createdAtUtc, processedAtUtc; Huy ghi outbox cùng transaction
meal, Bảo claim inbox cùng transaction projection. EventId dedup, mealLogId unique
contribution, nhiều meal/ngày chỉ một active day. Bootstrap phải bao phủ meal cũ
bằng snapshot/cursor/watermark + replay và completeness, không chỉ nhận event mới.

**Future lifecycle chưa triển khai**: update/delete/change-date chỉ mở sau khi duyệt
revision, old/new business date, tombstone retention, reconciliation và version/
migration cho consumer cũ. Additive chỉ khi tương thích; operation mới cần rollout/
queue version rõ, không silently drop delete. Các tình huống lifecycle dưới đây là
acceptance có điều kiện, không phải bằng chứng đã có endpoint edit/delete:
Tên làm việc event `MealActivityChangedV1` chỉ là đề xuất, không schema chính thức.
Payload cần bàn: schemaVersion, eventId, userId, mealLogId, revision, operation
(upsert/delete), businessDate cũ/mới và zone metadata nếu được Q01 duyệt,
effectiveAt và processedAt. `processedAt` dùng thứ tự phải đóng ở thời điểm xử lý,
không lấy thời điểm tạo user/log thay thế. Revision trên cùng mealLog chống đảo thứ tự;
eventId dedup không đủ để chặn event cũ ghi đè mới.

- Add: tăng nguồn meal của ngày; cùng ngày nhiều meal vẫn một active day.
- Edit chuyển ngày: bỏ contribution ngày cũ và thêm ngày mới trong một transaction.
- Delete: tombstone/revision cho meal, không bỏ active day còn metric/meal khác.
- Duplicate: inbox claim và projection trong cùng transaction, rollback claim khi lỗi;
  không để retry bị nuốt bởi claim chưa có side effect thành công.
- Late/out-of-order: chỉ áp revision mới; cập nhật ngày nghiệp vụ cũ, không coi ngày nhận là ngày ăn.
- Backfill: cần snapshot có watermark/revision và replay phần sau watermark, xử lý delete tombstone;
  công bố completeness chỉ khi bootstrap/reconcile xong, test event đến lúc backfill.
- Timezone: Diet/Progress thống nhất semantics Q01; không suy từ timestamp nhận event.
- Đề xuất topology: topic `longevia.events`, queue `progress.meal-activity`, routing key
  `diet.meal.activity-changed`, DLX/DLQ riêng và retry hữu hạn. Tên, retry/backoff,
  retention, prefetch, dedup lifetime đều phải Huy/Duy/Bảo thống nhất duyệt trước canonicalization (không có role Lead).

Phương án khác: gRPC lấy active dates theo user/range có pagination/cursor, version
và completeness; triển khai query đơn giản hơn nhưng cần deadline, availability,
ADR chiều gọi và cơ chế sửa/xóa dữ liệu cũ. Không coi DB query là phương án.

## Weight provider cho Diet BMR/TDEE — Q10 / PENDING APPROVAL

Diet sở hữu BMR/TDEE, TargetResponse.Bmr/Tdee hiện nullable và mapper trả null.
Identity sở hữu tuổi/ngày sinh, giới tính, chiều cao, mức vận động; Progress sở hữu
weight trong BodyMetricLog. Không chuyển công thức/target Diet sang Progress.

Đề xuất gRPC read do Bảo sở hữu provider, Huy sở hữu Diet consumer. Chưa định nghĩa
RPC/proto/tag: request logic userId + asOf/effectiveAt; response đề xuất presence,
weightKg, measuredAt (LoggedAt), sourceMetricId và observedAt/provenance. Cần duyệt
latest tại lúc đọc hay tại effectiveAt target; đề xuất latest LoggedAt <= asOf,
tie Id ổn định, Diet snapshot lúc tính target. Weight mới không tự sửa target cũ;
nếu muốn auto-refresh cần approval riêng. Future weight không thành latest;
backdate chỉ thắng theo measuredAt, không CreatedAt, không đoán weight quá khứ.

Missing metric khác unavailable: đề xuất absence có presence, Diet giữ Bmr/Tdee null
hoặc trạng thái duyệt; không dùng weight=0. Đề xuất deadline tổng 2s và read retry
tối đa 1 trong deadline; stale threshold, chấp nhận cache và retention còn PENDING.
Event projection giảm coupling nhưng eventual/stale và thêm outbox/version/bootstrap/
reconcile; gRPC đơn giản/fresh hơn nhưng phụ thuộc availability. Không DB/HTTP/import chéo.
Diet → Progress và Diet → Identity chưa có trong whitelist service-boundaries, cần thống nhất ADR
giữa các affected owners (Nguyên/Huy/Bảo) + canonical contracts trước code. T038 chỉ theo dõi; provider
implementation là feature riêng sau approval, không chặn Progress BMI core.

## Worker / digest — C, ngoài Phase 1 triển khai

PRD chỉ có weekly consumer scenario; chưa có schema, trigger/scheduler hoặc receipt.
Progress phải sở hữu tính summary/streak; Worker chỉ delivery, không tự tính từ DB.
Đề xuất báo cáo prepared event versioned: reportId, userId, weekStart/weekEnd/zone,
summary snapshot, generatedAt/processedAt, schemaVersion và correlationId;
email/consent lấy từ nguồn được owner duyệt, không nhét PII thiếu nhu cầu vào event.
Chưa chọn producer lịch, topology, recipient source, dedup reportId/period,
retry/DLQ và xử lý late correction. Duy/Bảo/Huy phải thống nhất bằng feature riêng.

## Recommendation — C / tương lai

[Proto hiện hành](../../../contracts/recommendation/v1/recommendation.proto) có
`GenerateWorkoutPlan` và `EvaluateLongevityHabits`. GenerateWorkoutPlan cần profile,
goal, week/date/limits; EvaluateLongevityHabits cần fasting/sleep/water/nutrition.
Không tự route REST AI, không gọi RPC ở Phase 1, không đưa AI vào task core.
Chiều Progress → Recommendation cần ADR/đồng thuận theo service-boundaries;
còn thiếu profile/nutrition contract. Đức xác nhận nếu mở feature sau, không cản
timer/water/sleep core. Mọi kết quả AI phải theo disclaimer/citation hiện hành.
