# Height gRPC integration — báo cáo hiện hành

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

---

## Tài liệu trước lượt Height — đọc theo trạng thái hiện hành ở trên
# Hướng dẫn tích hợp Frontend (Frontend Integration Guide)

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
- Q02 functional Height gRPC đã verified theo mục hiện hành phía trên; service-caller auth còn BLOCKED. Q10 Weight không đổi.

Chi tiết reconciliation và cách tái lập: [corrective-reconciliation.md](corrective-reconciliation.md).
Các mô tả Q01 PENDING ở phần lịch sử bên dưới được thay thế bởi quyết định này
cho nội bộ Progress; compatibility liên service vẫn PENDING.

Trạng thái: **PARTIAL_READY_FOR_FRONTEND**.

> [!NOTE]
> Các cụm tính năng Fasting (Start/End/Current), Water (Log/Today), Sleep (Log), Dashboard (Summary) và Metrics History đã có implementation/test. Dashboard historical streak chưa READY đầy đủ trước replay/reconciliation dữ liệu triển khai; không có DTO completeness mới được tự thêm.
> Riêng thao tác ghi mới `POST /api/progress/metrics` yêu cầu chiều cao từ Identity Service (Nguyên). Khi chưa có runtime provider từ Identity, endpoint trả về `503 Service Unavailable` (`progress.identity_unavailable`); khi người dùng chưa có chiều cao hồ sơ, trả về `422 Unprocessable Entity` (`progress.height_unavailable`).

Tài liệu này bàn giao toàn bộ các HTTP endpoints của Progress Service (Phases 2–6) cho đội ngũ phát triển Frontend.

## 1. Thông tin chung & Xác thực

- **Gateway URL**: `https://<host>/api/progress` (Qua Nginx reverse proxy với prefix stripping).
- **Swagger UI**: `/api/progress/swagger/index.html` (chỉ bật ở môi trường Development).
- **Authentication**: Bắt buộc gắn header `Authorization: Bearer <token>` trên tất cả các business endpoints.
  - Token JWT thuật toán ES256 được cấp bởi Identity Service.
  - Header JWT bắt buộc có `kid` hợp lệ.
  - Claim `sub` bắt buộc là một GUID hợp lệ đại diện cho `userId`.
  - Service tự động nhận diện người dùng qua token, không nhận `userId` trong query hay request body để đảm bảo bảo mật.
- **Resource Ownership**:
  - Áp dụng nguyên tắc **Ownership Hiding**: Khi truy cập tài nguyên không tồn tại hoặc thuộc về người dùng khác, API luôn trả về **`404 Not Found`** để tránh rò rỉ ID tài nguyên của người dùng khác.
- **Problem Details RFC 7807**:
  - Mọi phản hồi lỗi (400, 401, 404, 409, 422, 500) đều tuân thủ chuẩn RFC 7807 với các trường: `type`, `title`, `status`, `detail`, `instance`, `code`, `errors` (nếu có), và `traceId`.

---

## 2. Danh mục Endpoints

| STT | Method | Endpoint Gateway | Tóm tắt chức năng | Request Body | Phản hồi thành công | Mã lỗi nghiệp vụ |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `GET` | `/api/progress/alive` | Liveness probe | Không | 200 OK: `{ "status": "ok" }` | Không |
| 2 | `GET` | `/api/progress/health` | Readiness probe (kiểm tra kết nối DB) | Không | 200 OK: `{ "status": "ok" }` | 503 |
| 3 | `POST` | `/api/progress/fasting/start` | Bắt đầu phiên nhịn ăn | JSON: `protocol`, `startedAt`, `targetHours` | 201 Created: `FastingSessionResponse` | 400 (protocol/target invalid), 409 (đã có active session) |
| 4 | `POST` | `/api/progress/fasting/end` | Kết thúc phiên nhịn ăn | JSON: `sessionId`, `endedAt` | 200 OK: `EndFastingResponse` | 400 (endedAt < startedAt), 404 (session không tồn tại) |
| 5 | `GET` | `/api/progress/fasting/current` | Lấy phiên nhịn ăn đang hoạt động | Không | 200 OK: `CurrentFastingResponse` hoặc `null` | 401 |
| 6 | `POST` | `/api/progress/water/log` | Ghi nhận lượng nước uống | JSON: `amountMl` | 200 OK: `WaterSummaryResponse` | 400 (lượng nước ngoài 1..5000 ml) |
| 7 | `GET` | `/api/progress/water/today` | Tổng lượng nước uống trong ngày | Không | 200 OK: `WaterSummaryResponse` | 401 |
| 8 | `POST` | `/api/progress/sleep/log` | Ghi nhận giấc ngủ | JSON: `sleptAt`, `wokeAt`, `sleepQuality` | 201 Created: `SleepResponse` | 400 (wokeAt <= sleptAt, >24h), 409 (trùng lặp giấc ngủ) |
| 9 | `POST` | `/api/progress/metrics` | Ghi nhận chỉ số cơ thể & snapshot BMI | JSON: `loggedAt`, `weightKg`, + optional biomarkers | 201 Created: `BodyMetricResponse` | 400 (cân nặng/huyết áp sai), 422 (chưa có chiều cao profile) |
| 10 | `GET` | `/api/progress/metrics/history` | Tra cứu lịch sử chỉ số có phân trang | Query: `fromDate?`, `toDate?`, `pageNumber?`, `pageSize?` | 200 OK: `MetricsHistoryResponse` | 400 |
| 11 | `GET` | `/api/progress/dashboard/summary` | Tổng hợp dashboard & streak | Không | 200 OK: `DashboardSummaryResponse` | 401 |

---

## 3. Chi tiết Schema DTOs

### 3.1 Fasting
- **`StartFastingRequest`**:
  ```json
  {
    "protocol": "16:8", // "16:8", "18:6", "20:4", "Custom"
    "startedAt": "2026-10-25T08:00:00Z", // Không được ở tương lai
    "targetHours": 16 // Bắt buộc khớp với preset protocol hoặc 1..24 nếu là "Custom"
  }
  ```
- **`EndFastingRequest`**:
  ```json
  {
    "sessionId": "b6a7b738-f58c-4a34-bcf2-76332bf143a5",
    "endedAt": "2026-10-26T00:15:00Z"
  }
  ```
- **`CurrentFastingResponse`**:
  ```json
  {
    "sessionId": "b6a7b738-f58c-4a34-bcf2-76332bf143a5",
    "protocol": "16:8",
    "startedAt": "2026-10-25T08:00:00Z",
    "targetEndedAt": "2026-10-26T00:00:00Z",
    "targetHours": 16,
    "elapsedHours": 16.25,
    "remainingHours": 0.0,
    "status": "InProgress"
  }
  ```

### 3.2 Water & Sleep
- **`LogWaterRequest`**:
  ```json
  {
    "amountMl": 350 // 1..5000 ml
  }
  ```
- **`WaterSummaryResponse`**:
  ```json
  {
    "date": "2026-10-25",
    "consumedMl": 1000,
    "targetMl": 2500, // Cố định MVP constant 2500 ml
    "percentComplete": 40.0 // consumedMl / targetMl * 100
  }
  ```
- **`LogSleepRequest`**:
  ```json
  {
    "sleptAt": "2026-10-24T23:00:00Z",
    "wokeAt": "2026-10-25T07:00:00Z",
    "sleepQuality": "Good" // "Poor", "Fair", "Good", "Excellent"
  }
  ```

### 3.3 Body Metrics & History
- **`LogBodyMetricRequest`**:
  ```json
  {
    "loggedAt": "2026-10-25T07:30:00Z",
    "weightKg": 70.0, // 25.0 .. 300.0 kg
    "bodyFatPercent": 18.5, // Optional, >0 và <=100
    "waistCircumferenceCm": 80.0, // Optional, >0 và <=300
    "systolicBloodPressure": 120, // Optional, đi theo cặp với Diastolic, 50..250
    "diastolicBloodPressure": 80, // Optional, đi theo cặp, 30..150, Systolic > Diastolic
    "restingHeartRate": 65 // Optional, 25..250 bpm
  }
  ```
- **`BodyMetricResponse`**:
  ```json
  {
    "id": "e0e27170-c7ba-4a57-9db6-e9185a98bf68",
    "loggedAt": "2026-10-25T07:30:00Z",
    "weightKg": 70.0,
    "bmi": 22.86,
    "bmiCategory": "NormalWeight", // "Underweight", "NormalWeight", "Overweight", "Obese"
    "bodyFatPercent": 18.5,
    "waistCircumferenceCm": 80.0,
    "systolicBloodPressure": 120,
    "diastolicBloodPressure": 80,
    "restingHeartRate": 65
  }
  ```
- **`MetricsHistoryResponse`**:
  ```json
  {
    "items": [ /* Danh sách BodyMetricResponse */ ],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 15,
    "totalPages": 1
  }
  ```

### 3.4 Dashboard Summary
- **`DashboardSummaryResponse`**:
  ```json
  {
    "currentWeightKg": 68.5, // Cân nặng mới nhất
    "initialWeightKg": 72.0, // Cân nặng ghi nhận sớm nhất
    "weightDeltaKg": -1.5, // So sánh với mốc tuần trước (bản ghi tại/trước now - 7 days), null nếu thiếu baseline
    "currentBmi": 22.37, // BMI của cân nặng mới nhất
    "currentStreakDays": 2, // Hợp ngày MealLog và BodyMetricLog
    "bestStreakDays": 5, // Chuỗi ngày streak dài nhất trong lịch sử
    "fastingThisWeekCompleted": 1, // Số phiên fasting hoàn thành trong tuần (từ thứ Hai UTC)
    "averageSleepHours": 8.0 // Trung bình thời gian ngủ các giấc kết thúc trong 7 ngày gần nhất
  }
  ```
