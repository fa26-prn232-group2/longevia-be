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
| GET /api/progress/metrics/history | fromDate?, toDate?, pageNumber=1, pageSize=20 | 200: collection metrics; API §4.2 | Q01 UTC date inclusive/exclusive đã APPROVED nội bộ; Q07 wire pagination. |
| POST /api/progress/fasting/start | protocol, startedAt, targetHours | 201: sessionId, protocol, startedAt, targetEndedAt, status=InProgress; API §4.3 | Q03 target/retry/time rules. |
| POST /api/progress/fasting/end | sessionId, endedAt | 200: sessionId, actualHours, targetHours, isGoalReached, status=Completed; API §4.4 | Q03 retry/status; Q08 ownership. |
| GET /api/progress/fasting/current | không có body | PRD có route, API thiếu schema; đề xuất 200 object hoặc null, object: sessionId, protocol, startedAt, targetEndedAt, targetHours, elapsedHours, remainingHours, status | Q03 shape/precision/no-active. |
| POST /api/progress/water/log | amountMl | 200: date, consumedMl, targetMl, percentComplete; API §4.5 | Q01 UTC day đã APPROVED; Q05 target/type/limits. Không tự thêm loggedAt vào body. |
| GET /api/progress/water/today | không có body | PRD có route, API thiếu schema; đề xuất 200 cùng shape water/log | Q01 UTC đã APPROVED; Q05 theo quyết định hiện hành. |
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

## Q02 hiện hành — Identity Height gRPC (2026-10-26)

PR #14 đã merge: source 72555e139e31e94bc014be4ada97532cd486974c,
main merge c679b6f174a3cfcdfc0097b03ee81b3729bb72e2. Nhánh Progress merge main
không rewrite 14 commit cũ; Rabbit/inbox/reconciliation và Q01 UTC giữ nguyên.

**Q02 FUNCTIONAL INTEGRATION: DONE / VERIFIED** — client thật đã chạy với actual
Identity process, HTTP/2 và hai PostgreSQL riêng. **Authenticated service-to-service
trust: PENDING; secure production readiness: BLOCKED**. Đây không phải service
flow đã authenticated chỉ vì frontend JWT pass.

Canonical: longevia.identity.v1.IdentityService/GetUserHeight, C# namespace
Longevia.Identity.Contracts.V1. Request user_id field 1 (GUID); response optional
height_cm_x100 field 1 (int32 cm × 100). Một RPC, hai message, hai field; không thêm
GetBodyProfile hoặc wire DTO khác. HasHeightCmX100=false khác present zero.
17525 / 100m = 175.25 cm, validate 50.00–250.00, không integer division.

Progress.Infrastructure codegen Client từ canonical proto bằng Grpc.Tools;
Grpc.Net.Client và singleton channel/client do DI dispose, không reference
Identity implementation. IdentityGrpc:Address bắt buộc (Compose http://identity:50051);
DeadlineMilliseconds mặc định 2000, cho phép 50–10000. Không application retry;
deadline tổng mỗi call hữu hạn; truyền incoming cancellation tới HTTP/2.

| Kết quả Identity | HTTP Progress | Code / xử lý |
| --- | --- | --- |
| Height hợp lệ | 201 | Tính/lưu BMI snapshot hiện hữu |
| Optional unset | 422 | progress.height_unavailable |
| NOT_FOUND | 404 | progress.profile_not_found |
| UNAVAILABLE / DEADLINE_EXCEEDED | 503 | progress.identity_unavailable |
| Caller cancellation | Hủy request | Không tiếp tục ghi |
| INVALID_ARGUMENT / INTERNAL / auth fault / returned height sai | 502 | progress.identity_integration_failure |

ProfileHeightResult.GrpcStatus và structured log giữ status gRPC nội bộ, không đưa
URL, RpcException detail, credentials hoặc profile data vào Problem Details.

### Bảo mật và follow-up

ADR 001 §2.2 và microservices/02-grpc-and-events hiện chấp nhận Compose network trust:
không caller auth, không mTLS/token service. Shared ES256 JWT là frontend user
authentication, không service identity. Identity Program.MapGrpcService và service
không RequireAuthorization; test gọi không credentials thành công xác nhận gap.
Network isolation là defense-in-depth, không authenticated identity.

Theo yêu cầu Bảo trong lượt này, secure readiness vẫn BLOCKED. Nguyên và Bảo cần
ghi quyết định trust model cho Height: triển khai service identity/token hoặc mTLS
với authorization provider + contract cấp/rotate/revoke credential và negative tests,
hoặc phê duyệt tường minh mô hình network-trust hiện tại cho phạm vi MVP. Không có
phê duyệt chung mới được suy diễn; không forward user JWT làm service credential.
Progress không sửa Identity hoặc thêm API key giả.

17 gRPC scenarios và 9 options unit cases đã pass ở scoped run. Success tests dùng
Identity entry point/repository/migrations thật; seed/update qua REST của chính
Identity trong fixture, không DB/source import chéo. Controlled server chỉ cho
unset, malformed response, fault, deadline/cancellation. Full gates/Docker/CI mới
được báo ở verification.md và PR #13, không dùng lại số 733 của commit cũ.
Q10 Weight và Huy Q01 acknowledgment/Diet PR #12 giữ trạng thái riêng.
## Meal activity — create-only v1, rollout PENDING

Canonical MealLoggedEvent có EventId=MealLogId, UserId, BusinessDate, CreatedAt,
OccurredOn. Topic longevia.events / diet.meal.logged; queue progress.meal-activity.
Diet producer/outbox/replay và JSON snapshot v1 nằm trên PR #12 chưa merge; Progress
consumer/inbox/projection hiện có, corrective bổ sung đối soát DB thật và broker tests.
Không có CSV hoặc public snapshot API. capturedAtUtc là audit, không phải cursor.

BusinessDate là DateOnly canonical Diet gửi, không đổi bởi replay, receive timestamp,
CreatedAt hoặc OccurredOn. Metric activity dùng UTC theo Q01 Bảo approve MVP;
frontend auto-select current meal date UTC cần Huy acknowledgment, còn PENDING.
Không thêm field DTO completeness; historical streak readiness vẫn chờ rollout,
replay và reconciliation dữ liệu triển khai. Future edit/delete/change-date không
thuộc v1, cần contract version/revision/tombstone và migration riêng.

[Backend guide](../backend-integration-guide.md) và [reconciliation](../corrective-reconciliation.md)
phân biệt transport test, actual Diet interoperability fixture và production rollout.
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

## Corrective PR #13 — Q01 có hiệu lực ngày 2026-10-25

**Q01 — APPROVED FOR PROGRESS MVP BY BẢO** (ủy quyền trực tiếp trong corrective).

- Mọi ngày server-derived của Progress dùng UTC calendar: DateOnly của instant.UtcDateTime;
  today lấy từ TimeProvider.GetUtcNow(). Không dùng timezone máy hoặc Asia/Ho_Chi_Minh.
- Water date lấy instant ghi; water/today đổi tại 00:00 UTC. Sleep date lấy WokeAt UTC,
  duration/overlap so full instants; giữ quy tắc overlap hiện hành.
- Body metric activity lấy LoggedAt UTC; history fromDate inclusive, toDate exclusive
  tại đầu ngày kế tiếp. Không rebucket hoặc tính lại snapshot BMI/height lịch sử.
- Dashboard neo UTC today/yesterday theo Q06, tuần bắt đầu Monday 00:00 UTC,
  cửa sổ ngày dùng UTC; fasting tính theo instant hoàn thành, tuần [Monday,next Monday).
- Diet MealLoggedEvent.BusinessDate là canonical DateOnly bất biến: consume nguyên văn;
  CreatedAt/OccurredOn/replay/receive time không đổi ngày ăn. Meal + metric cùng nhãn
  DateOnly chỉ tạo một active day.
- Việt Nam 00:30 ngày 25/10 vẫn là UTC ngày 24/10. MVP không có user-local midnight,
  không cần timezone Identity. Chuyển IANA timezone là quyết định versioned riêng,
  phải có migration/rebucketing và phê duyệt liên service.
- **Cross-service acknowledgment: PENDING Huy** cho Diet/frontend tự chọn ngày hiện tại
  theo UTC; client chọn ngày lịch sử được giữ nguyên. Không claim Huy đã approve.
- Q02 Height gRPC và Q10 Weight gRPC giữ nguyên phạm vi/blocker; không thêm RPC.

Chi tiết reconciliation và cách tái lập: [corrective-reconciliation.md](../corrective-reconciliation.md).
Các mô tả Q01 PENDING ở phần lịch sử bên dưới được thay thế bởi quyết định này
cho nội bộ Progress; compatibility liên service vẫn PENDING.
