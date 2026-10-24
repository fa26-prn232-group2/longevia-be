# Hướng dẫn tích hợp Frontend (Frontend Integration Guide)

Trạng thái: **READY_FOR_FRONTEND**.

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
