# Tài Liệu Đặc Tả API Chi Tiết (API Specification) — Longevia Backend

---

## 1. Quy chuẩn chung (Global API Standards)

- **Cổng vào duy nhất**: `http://localhost:8080` (qua Nginx Gateway).
- **Quy tắc định tuyến**: Nginx tự động cắt bỏ tiền tố `/api/<service>` trước khi chuyển tiếp tới container đích.
  - Gateway Route: `/api/identity/auth/login` ➔ Service Internal Route: `/auth/login`
- **Xác thực**:
  - Header: `Authorization: Bearer <access_token>`
  - Thuật toán ký: **ES256** (chỉ Identity ký; Diet & Progress tự verify bằng Public Key).
- **Quy chuẩn dữ liệu**:
  - Format: `application/json; charset=utf-8`
  - Đặt tên trường (Naming Convention): `camelCase`
  - Thời gian: ISO 8601 UTC (Ví dụ: `2026-10-21T14:30:00Z`)
- **Định dạng phân trang chuẩn (`PagedResponse<T>`)**:
  ```json
  {
    "items": [],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 125,
    "totalPages": 7,
    "hasPreviousPage": false,
    "hasNextPage": true
  }
  ```
- **Định dạng lỗi chuẩn (RFC 7807 Problem Details)**:
  ```json
  {
    "type": "https://longevia.com/errors/validation",
    "title": "Dữ liệu không hợp lệ",
    "status": 422,
    "code": "identity.validation_failed",
    "detail": "Một hoặc nhiều trường thông tin chưa chính xác.",
    "instance": "/api/identity/auth/register",
    "errors": [
      { "field": "email", "message": "Email không đúng định dạng." }
    ]
  }
  ```

---

## 2. Identity Service APIs (Dev 1 phụ trách)
> Service Route Prefix tại Gateway: `/api/identity` | Database: `identity_db`

### 2.1 `POST /api/identity/auth/register` — Đăng ký tài khoản
- **Xác thực**: Public
- **Mô tả**: Tạo tài khoản người dùng mới và lưu outbox event để gửi email kích hoạt.
- **Request Body**:
  ```json
  {
    "email": "alex.minh@example.com",
    "password": "Password123!",
    "fullName": "Alex Minh"
  }
  ```
- **Validation**:
  - `email`: Bắt buộc, đúng định dạng RFC 5322, tối đa 255 ký tự.
  - `password`: Tối thiểu 8 ký tự, có ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số, 1 ký tự đặc biệt.
  - `fullName`: Bắt buộc, độ dài 2–100 ký tự.
- **Response `201 Created`**:
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "alex.minh@example.com",
    "fullName": "Alex Minh",
    "status": "PendingVerification",
    "createdAt": "2026-10-21T14:30:00Z"
  }
  ```
- **Lỗi thường gặp**:
  - `409 Conflict`: `code = "identity.email_taken"` (Email đã được đăng ký).
  - `422 Unprocessable Entity`: Vi phạm ràng buộc mật khẩu hoặc email.

---

### 2.2 `GET /api/identity/auth/verify?token={token}` — Xác minh email
- **Xác thực**: Public; token là bí mật dùng một lần.
- **Mô tả**: Kích hoạt tài khoản `PendingVerification` nếu token khớp và chưa hết hạn. Link email hiện tại mở endpoint này bằng GET.
- **Thời hạn**: 24 giờ từ lúc đăng ký. Token đã dùng, sai hoặc hết hạn đều không kích hoạt tài khoản.
- **Response `204 No Content`**: Xác minh thành công.
- **Lỗi `400 Bad Request`**: `code = "identity.invalid_verification_token"` khi token sai, hết hạn hoặc đã dùng.
- **Lưu ý**: GET xác minh trực tiếp theo quyết định sản phẩm; trình quét link trong email có thể tự mở link và kích hoạt tài khoản.

### 2.3 `POST /api/identity/auth/login` — Đăng nhập
- **Xác thực**: Public
- **Mô tả**: Xác thực thông tin đăng nhập, ký phát hành Access Token (ES256) và cấp Refresh Token.
- **Request Body**:
  ```json
  {
    "email": "alex.minh@example.com",
    "password": "Password123!"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "accessToken": "eyJhbGciOiJFUzI1NiIsImtpZCI6ImxvbmdldmlhLWRldi0wMSJ9...",
    "refreshToken": "d8f6d7b5-234e-48a1-b847-19d424b9102c",
    "tokenType": "Bearer",
    "expiresIn": 900
  }
  ```
- **Lỗi thường gặp**:
  - `401 Unauthorized`: `code = "identity.invalid_credentials"` (Sai email hoặc mật khẩu).

---

### 2.4 `POST /api/identity/auth/refresh` — Làm mới Access Token
- **Xác thực**: Public
- **Mô tả**: Sử dụng Refresh Token để đổi lấy cặp Access Token + Refresh Token mới (Rotation).
- **Request Body**:
  ```json
  {
    "refreshToken": "d8f6d7b5-234e-48a1-b847-19d424b9102c"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "accessToken": "eyJhbGciOiJFUzI1NiIsImtpZCI6ImxvbmdldmlhLWRldi0wMSJ9...",
    "refreshToken": "9a12c45e-88bc-4613-9031-6b2158fe2331",
    "tokenType": "Bearer",
    "expiresIn": 900
  }
  ```
- **Lỗi thường gặp**:
  - `401 Unauthorized`: `code = "identity.invalid_refresh_token"` (Token hết hạn hoặc đã bị thu hồi).

---

### 2.5 `POST /api/identity/auth/logout` — Đăng xuất
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "refreshToken": "9a12c45e-88bc-4613-9031-6b2158fe2331"
  }
  ```
- **Response `204 No Content`**

### 2.6 `POST /api/identity/auth/change-password` — Đổi mật khẩu
- **Xác thực**: Bearer JWT.
- **Request Body**:
  ```json
  {
    "currentPassword": "CurrentPassword123!",
    "newPassword": "NewPassword456!"
  }
  ```
- **Validation**: mật khẩu mới tối thiểu 8 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt, cùng policy với đăng ký.
- **Response `204 No Content`**: đổi mật khẩu thành công; mọi refresh token của user bị thu hồi. Access JWT hiện tại có thể dùng tới khi hết hạn (tối đa 15 phút).
- **Lỗi**: `400 identity.invalid_current_password` nếu mật khẩu hiện tại không khớp; `401 identity.unauthorized` nếu JWT không còn được chấp nhận; lỗi validation theo `422 identity.validation_failed`.

### 2.7 `POST /api/identity/auth/forgot-password` — Yêu cầu đặt lại mật khẩu
- **Xác thực**: Public.
- **Request Body**: `{ "email": "alex.minh@example.com" }`.
- **Response `202 Accepted`** không có body trong mọi trường hợp, kể cả email không tồn tại, chưa kích hoạt hoặc không thể nhận reset; response không tiết lộ trạng thái tài khoản.
- Với tài khoản Active, Identity ghi reset token hash (TTL 15 phút) và `PasswordResetRequestedEvent` vào transactional outbox trong cùng transaction. Reset token trước đó còn hiệu lực của user bị vô hiệu hóa.
- Email được gửi bởi Worker; Identity không gọi SMTP/Brevo.

### 2.8 `POST /api/identity/auth/reset-password` — Đặt lại mật khẩu
- **Xác thực**: Public.
- **Request Body**:
  ```json
  {
    "token": "reset-token-from-email",
    "newPassword": "NewPassword456!"
  }
  ```
- **Validation**: policy mật khẩu giống endpoint đăng ký.
- **Response `204 No Content`**: token được tiêu thụ nguyên tử, mật khẩu được đổi và mọi refresh token của user bị thu hồi. Access JWT đã phát hành hết hạn tự nhiên trong tối đa 15 phút.
- **Lỗi `400 Bad Request`**: `identity.invalid_password_reset_token` nếu token sai, hết hạn, đã dùng hoặc bị thay thế.

### 2.9 `GET /api/identity/users/me` — Lấy thông tin cá nhân
- **Xác thực**: Bearer JWT
- **Response `200 OK`**: DTO gồm `id`, `email`, `fullName`, `status`, `createdAt` và `profile` (nullable). `profile` có `id`, `birthDate`, `biologicalSex`, `heightCm`, `activityLevel`, `dietaryPreference`, `allergies`, `updatedAt`.
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "alex.minh@example.com",
    "fullName": "Alex Minh",
    "status": "Active",
    "createdAt": "2026-10-21T14:30:00Z",
    "profile": {
      "id": "0d5d3d36-d8ef-43fb-b50c-b393b874205c",
      "birthDate": "1998-05-15",
      "biologicalSex": "Male",
      "heightCm": 175.5,
      "activityLevel": "ModeratelyActive",
      "dietaryPreference": "Mediterranean",
      "allergies": ["Peanuts", "Shellfish"],
      "updatedAt": "2026-10-21T14:30:00Z"
    }
  }
  ```

---

### 2.10 `PUT /api/identity/users/me/profile` — Cập nhật hồ sơ thể trạng cơ sở
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "birthDate": "1998-05-15",
    "biologicalSex": "Male",
    "heightCm": 175.5,
    "activityLevel": "ModeratelyActive",
    "dietaryPreference": "Mediterranean",
    "allergies": ["Peanuts", "Shellfish"]
  }
  ```
- **Validation**:
  - `biologicalSex`: Chỉ chấp nhận `"Male"` hoặc `"Female"`.
  - `heightCm`: Số thực từ 50.00 đến 250.00 cm, tối đa 2 chữ số thập phân.
  - `activityLevel`: Một trong các giá trị `["Sedentary", "LightlyActive", "ModeratelyActive", "VeryActive"]`.
  - `dietaryPreference`: Một trong `["None", "Keto", "Vegan", "Mediterranean", "LowCarb"]`.
- **Response `200 OK`**: `ProfileResponse` gồm `id`, `birthDate`, `biologicalSex`, `heightCm`, `activityLevel`, `dietaryPreference`, `allergies` và `updatedAt`; `heightCm` là số thực.

---

## 3. Diet Service APIs (Dev 2 phụ trách)
> Service Route Prefix tại Gateway: `/api/diet` | Database: `diet_db`

### 3.1 `GET /api/diet/foods` — Tra cứu danh mục món ăn
- **Xác thực**: Bearer JWT
- **Query Parameters**:
  - `search` (string, optional): Từ khóa tìm kiếm tên thực phẩm.
  - `category` (string, optional): Lọc theo nhóm (Ví dụ: `Vegetables`, `Meat`, `Seafood`, `Grains`).
  - `pageNumber` (int, default: 1)
  - `pageSize` (int, default: 20, max: 100)
- **Response `200 OK`**:
  ```json
  {
    "items": [
      {
        "id": "e2a30b2e-2f22-487e-913a-6ff1484be7cf",
        "name": "Ức gà áp chảo",
        "category": "Meat",
        "servingSize": 100.0,
        "servingUnit": "g",
        "calories": 165,
        "proteinGrams": 31.0,
        "carbsGrams": 0.0,
        "fatGrams": 3.6,
        "fiberGrams": 0.0,
        "isCustom": false
      }
    ],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 450,
    "totalPages": 23,
    "hasPreviousPage": false,
    "hasNextPage": true
  }
  ```

---

### 3.2 `POST /api/diet/foods` — Tạo món ăn tùy chỉnh cá nhân
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "name": "Sinh tố bơ yến mạch",
    "category": "Beverage",
    "servingSize": 1.0,
    "servingUnit": "Ly",
    "calories": 280,
    "proteinGrams": 8.5,
    "carbsGrams": 32.0,
    "fatGrams": 14.0,
    "fiberGrams": 6.2
  }
  ```
- **Response `201 Created`**: Trả về thông tin món ăn vừa tạo kèm `id`.

---

### 3.3 `GET /api/diet/targets/me` — Lấy mục tiêu dinh dưỡng hiện tại
- **Xác thực**: Bearer JWT
- **Giai đoạn Diet độc lập**: mục tiêu được nhập tay; `bmr` và `tdee` trả `null`
  cho đến khi contract hồ sơ/cân nặng được tích hợp. Chưa có mục tiêu trả
  `404` với code `diet.target_not_found`, không tự tạo mục tiêu mặc định.
- **Response `200 OK`**:
  ```json
  {
    "id": "11fa24e8-466d-4ee8-a6b3-6c7081734a91",
    "bmr": null,
    "tdee": null,
    "targetCalories": 2100,
    "targetProteinGrams": 150.0,
    "targetCarbsGrams": 210.0,
    "targetFatGrams": 70.0,
    "updatedAt": "2026-10-20T08:00:00Z"
  }
  ```

---

### 3.4 `PUT /api/diet/targets/me` — Cập nhật mục tiêu dinh dưỡng
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "targetCalories": 2100,
    "targetProteinGrams": 150.0,
    "targetCarbsGrams": 210.0,
    "targetFatGrams": 70.0
  }
  ```
- **Validation**:
  - Tổng năng lượng từ macro `(protein*4 + carbs*4 + fat*9)` không được lệch quá 5% so với `targetCalories`.
- **Response `200 OK`**: Trả về mục tiêu dinh dưỡng mới.

---

### 3.5 `POST /api/diet/meals` — Ghi nhật ký món ăn trong bữa
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "date": "2026-10-21",
    "mealType": "Lunch",
    "items": [
      {
        "foodId": "e2a30b2e-2f22-487e-913a-6ff1484be7cf",
        "quantity": 150.0
      }
    ]
  }
  ```
- **Validation**:
  - `mealType`: Một trong `["Breakfast", "Lunch", "Dinner", "Snack"]`.
  - `quantity`: Số dương > 0 (tương ứng với khối lượng theo đơn vị `servingUnit`).
- **Response `201 Created`**: Trả về chi tiết bữa ăn vừa lưu.

---

### 3.6 `GET /api/diet/meals/daily-summary` — Tổng hợp dinh dưỡng trong ngày
- **Xác thực**: Bearer JWT
- **Quy ước**: ngày mặc định UTC; `consumed` cộng snapshot của mọi log cùng ngày,
  nhóm theo `mealType`. Target là mục tiêu hiện tại; remaining có thể âm khi
  vượt mục tiêu. Chưa có target trả `404 diet.target_not_found`.
- **Query Parameters**:
  - `date` (string YYYY-MM-DD, default: ngày hiện tại)
- **Response `200 OK`**:
  ```json
  {
    "date": "2026-10-21",
    "targetCalories": 2100,
    "consumedCalories": 1450,
    "remainingCalories": 650,
    "macros": {
      "protein": { "consumed": 105.0, "target": 150.0, "remaining": 45.0 },
      "carbs": { "consumed": 140.0, "target": 210.0, "remaining": 70.0 },
      "fat": { "consumed": 52.0, "target": 70.0, "remaining": 18.0 }
    },
    "meals": [
      {
        "mealType": "Breakfast",
        "totalCalories": 450,
        "itemsCount": 2
      },
      {
        "mealType": "Lunch",
        "totalCalories": 1000,
        "itemsCount": 3
      }
    ]
  }
  ```

---

### 3.7 `GET /api/diet/meals/ai-recommendations` — Nhận gợi ý món ăn từ AI
- **Xác thực**: Bearer JWT
- **Mô tả**: Diet Service tự tính lượng calo/macro còn thiếu trong ngày và gọi nội bộ sang Recommendation Service qua gRPC.
- **Response `200 OK`**:
  ```json
  {
    "remainingCalories": 650,
    "recommendedMeals": [
      {
        "name": "Cá hồi nướng măng tây và quinoa",
        "description": "Giàu Omega-3 và chất xơ, hỗ trợ chống viêm và cân bằng vi chất buổi tối mà không gây đầy bụng.",
        "estimatedCalories": 580,
        "proteinGrams": 42.0,
        "carbsGrams": 45.0,
        "fatGrams": 18.0,
        "keyMicronutrients": ["Omega-3", "Magie", "Vitamin D"]
      }
    ],
    "longevityTip": "Hạn chế tinh bột nhanh sau 19h để giữ mức Insulin ổn định, kích hoạt quá trình tự thực bào trong đêm."
  }
  ```

---

## 4. Progress Service APIs (Dev 3 phụ trách)
> Service Route Prefix tại Gateway: `/api/progress` | Database: `progress_db`

### 4.1 `POST /api/progress/metrics` — Lưu chỉ số cơ thể & Biomarkers
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "loggedAt": "2026-10-21T07:00:00Z",
    "weightKg": 68.5,
    "bodyFatPercent": 16.5,
    "waistCircumferenceCm": 78.0,
    "systolicBloodPressure": 118,
    "diastolicBloodPressure": 76,
    "restingHeartRate": 62
  }
  ```
- **Validation**:
  - `weightKg`: Bắt buộc, số dương từ 25.0 đến 300.0 kg.
  - Các chỉ số huyết áp, nhịp tim: Không bắt buộc, nhưng nếu có phải nằm trong giới hạn sinh lý thực tế.
- **Response `201 Created`**:
  ```json
  {
    "id": "78b45a6c-918b-4b11-9a72-192a83719482",
    "loggedAt": "2026-10-21T07:00:00Z",
    "weightKg": 68.5,
    "bmi": 22.25,
    "bmiCategory": "NormalWeight",
    "bodyFatPercent": 16.5,
    "waistCircumferenceCm": 78.0,
    "systolicBloodPressure": 118,
    "diastolicBloodPressure": 76,
    "restingHeartRate": 62
  }
  ```

---

### 4.2 `GET /api/progress/metrics/history` — Lấy lịch sử biến đổi thể trạng
- **Xác thực**: Bearer JWT
- **Query Parameters**:
  - `fromDate` (ISO 8601, optional)
  - `toDate` (ISO 8601, optional)
  - `pageNumber` (int, default: 1)
  - `pageSize` (int, default: 20)
- **Response `200 OK`**: Danh sách `PagedResponse<BodyMetricLogDto>`.

---

### 4.3 `POST /api/progress/fasting/start` — Bắt đầu phiên nhịn ăn gián đoạn
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "protocol": "16:8",
    "startedAt": "2026-10-21T20:00:00Z",
    "targetHours": 16
  }
  ```
- **Validation**:
  - `protocol`: Một trong `["16:8", "18:6", "20:4", "Custom"]`.
- **Response `201 Created`**:
  ```json
  {
    "sessionId": "45f918e1-561b-410a-b28e-561928a4918e",
    "protocol": "16:8",
    "startedAt": "2026-10-21T20:00:00Z",
    "targetEndedAt": "2026-10-22T12:00:00Z",
    "status": "InProgress"
  }
  ```

---

### 4.4 `POST /api/progress/fasting/end` — Kết thúc phiên nhịn ăn
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "sessionId": "45f918e1-561b-410a-b28e-561928a4918e",
    "endedAt": "2026-10-22T12:15:00Z"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "sessionId": "45f918e1-561b-410a-b28e-561928a4918e",
    "actualHours": 16.25,
    "targetHours": 16,
    "isGoalReached": true,
    "status": "Completed"
  }
  ```

---

### 4.5 `POST /api/progress/water/log` — Ghi nhận lượng nước uống
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "amountMl": 350
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "date": "2026-10-21",
    "consumedMl": 1850,
    "targetMl": 2500,
    "percentComplete": 74.0
  }
  ```

---

### 4.6 `GET /api/progress/dashboard/summary` — Tổng quan thể trạng & Streaks
- **Xác thực**: Bearer JWT
- **Response `200 OK`**:
  ```json
  {
    "currentWeightKg": 68.5,
    "initialWeightKg": 72.0,
    "weightDeltaKg": -3.5,
    "currentBmi": 22.25,
    "currentStreakDays": 12,
    "bestStreakDays": 24,
    "fastingThisWeekCompleted": 5,
    "averageSleepHours": 7.4
  }
  ```

---

## 5. Recommendation Service gRPC API (Dev 4 phụ trách)
> **Giao thức**: gRPC Protobuf | **Cổng nội bộ**: `:50051` | **Mạng**: `internal_net` (Không expose ra Nginx)
> **Tri thức**: RAG trên corpus *Chế độ ăn trường thọ* — mọi khuyến nghị phải có `citations[]` (chương + trang sách bản in + trích đoạn) hoặc phải `abstained`.
> **Kế hoạch tuần**: `GenerateMealPlan`/`GenerateWorkoutPlan` không có trường `abstained`; khi corpus rỗng hoặc provider lỗi, service trả kế hoạch degraded/tất định kèm `warnings[]` và `used_fallback`, chỉ rõ nguồn không kiểm chứng được.

File hợp đồng: [`contracts/recommendation/v1/recommendation.proto`](../../contracts/recommendation/v1/recommendation.proto)

```protobuf
syntax = "proto3";

package longevia.recommendation.v1;
option csharp_namespace = "Longevia.Recommendation.Contracts.V1";

service RecommendationService {
  // Gợi ý bữa ăn: chỉ chọn trong candidate_foods do caller gửi, lọc dị ứng bằng code
  rpc GetMealRecommendations   (MealRecommendationRequest)    returns (MealRecommendationResponse);
  // Phân tích thiếu hụt vi chất 7 ngày (gap tính deterministic theo bảng RDA)
  rpc AnalyzeNutrientGaps      (NutrientGapRequest)           returns (NutrientGapResponse);
  // Đánh giá thói quen sống và chấm điểm Longevity (rubric cố định)
  rpc EvaluateLongevityHabits  (LongevityHabitRequest)       returns (LongevityHabitResponse);
  // Hỏi tri thức trong sách, có trích dẫn; abstain khi corpus không đủ
  rpc AskLongevityQuestion     (AskLongevityQuestionRequest)  returns (AskLongevityQuestionResponse);
  // Tra cứu các đoạn trong sách theo truy vấn
  rpc SearchLongevityKnowledge (SearchKnowledgeRequest)       returns (SearchKnowledgeResponse);
  // Kế hoạch ăn uống theo tuần: chỉ chọn trong candidate_foods, lọc dị ứng bằng code
  rpc GenerateMealPlan         (GenerateMealPlanRequest)      returns (GenerateMealPlanResponse);
  // Kế hoạch tập luyện theo tuần: hoạt động lấy từ catalog chương 5 + giới hạn của caller
  rpc GenerateWorkoutPlan      (GenerateWorkoutPlanRequest)   returns (GenerateWorkoutPlanResponse);
}

// ---- Dùng chung ----
enum MealType {
  MEAL_TYPE_UNSPECIFIED = 0;
  BREAKFAST = 1;
  LUNCH = 2;
  DINNER = 3;
  SNACK = 4;
}

enum BiologicalSex {
  SEX_UNSPECIFIED = 0;
  MALE = 1;
  FEMALE = 2;
}

enum UserGoal {
  GOAL_UNSPECIFIED = 0;
  LOSE_WEIGHT = 1;
  MAINTAIN = 2;
  BUILD_MUSCLE = 3;
  LONGEVITY = 4;
}

message UserProfile {
  int32 age = 1;
  BiologicalSex sex = 2;               // MALE | FEMALE
  double height_cm = 3;
  double weight_kg = 4;
  string activity_level = 5;            // Sedentary | LightlyActive | ModeratelyActive | VeryActive
  string dietary_preference = 6;        // None | Keto | Vegan | Mediterranean | LowCarb
  repeated string allergies = 7;
}

message Citation {
  int32 chapter_number = 1;             // 0 = phần đầu sách
  string chapter_title = 2;
  int32 book_page = 3;                  // trang BẢN IN - nguồn chuẩn
  int32 pdf_page = 4;                   // trang file PDF scan - đối chiếu
  string quote = 5;                     // trích đoạn nguyên văn, đã verify khớp chunk
  bool verified = 6;                    // false = trang chưa đối chiếu tay
  string source_file = 7;
}

message ScoreComponent {
  string key = 1;                       // fasting | sleep | hydration | nutrition
  int32 score = 2;
  int32 weight = 3;                     // trọng số %
  string reason = 4;
}

message Disclaimer { string text = 1; }

// ---- 1. Gợi ý bữa ăn ----
message CandidateFood {
  string food_id = 1;                   // map về FoodItems của Diet
  string name = 2;
  string serving_size = 3;
  string serving_unit = 4;
  int32 calories = 5;
  double protein_grams = 6;
  double carbs_grams = 7;
  double fat_grams = 8;
  double fiber_grams = 9;
  repeated string allergens = 10;       // đã chuẩn hoá lowercase
}

message MealRecommendationRequest {
  string user_id = 1;
  MealType meal_type = 2;               // BREAKFAST | LUNCH | DINNER | SNACK
  UserGoal goal = 3;
  UserProfile profile = 4;
  int32 consumed_calories = 5;
  int32 target_calories = 6;
  double target_protein_remaining = 7;
  double target_carbs_remaining = 8;
  double target_fat_remaining = 9;
  repeated CandidateFood candidate_foods = 10;
  string locale = 11;                   // "vi-VN"
}

message RecommendedMeal {
  string food_id = 1;
  string name = 2;
  string description = 3;
  int32 estimated_calories = 4;
  double protein_grams = 5;
  double carbs_grams = 6;
  double fat_grams = 7;
  repeated string key_micronutrients = 8;
  string rationale = 9;
  repeated Citation citations = 10;
}

message MealRecommendationResponse {
  repeated RecommendedMeal meals = 1;
  string longevity_tip = 2;
  repeated Citation tip_citations = 3;
  Disclaimer disclaimer = 4;
  bool calorie_cap_reached = 5;         // BR-03: đã chạm trần 105%
  string safety_note = 6;
}

// ---- 2. Thiếu hụt vi chất ----
message DailyNutrientSummary {
  string date = 1;
  int32 calories = 2;
  double protein = 3;
  double carbs = 4;
  double fat = 5;
  double fiber = 6;
  // Vi chất (vitamin/khoáng) của ngày; caller quyết định gửi vi chất nào.
  // Không gửi một vi chất nghĩa là "chưa có dữ liệu", KHÔNG phải "đã nạp 0".
  repeated MicronutrientIntake micronutrients = 7;
}

// Một vi chất đã nạp trong ngày. `nutrient` phải khớp khoá trong bảng nhu cầu
// tham chiếu của service (ví dụ "Vitamin D", "Calcium", "Omega-3").
message MicronutrientIntake {
  string nutrient = 1;
  string unit = 2;               // mg | mcg | g | kcal
  double amount = 3;
}

message NutrientGapRequest {
  string user_id = 1;
  UserProfile profile = 2;
  repeated DailyNutrientSummary past_7_days = 3;
  int32 target_calories = 4;
  double target_protein_grams = 5;
  double target_carbs_grams = 6;
  double target_fat_grams = 7;
  double target_fiber_grams = 8;
}

message NutrientGap {
  string nutrient = 1;
  string unit = 2;
  double target = 3;
  double average_intake = 4;
  double deficit = 5;
  string severity = 6;                 // mild | moderate | severe
}

message NutrientGapResponse {
  repeated NutrientGap gaps = 1;
  repeated string deficient_nutrients = 2;
  repeated string suggested_foods = 3;
  string analysis_summary = 4;
  repeated Citation citations = 5;
  Disclaimer disclaimer = 6;
}

// ---- 3. Đánh giá thói quen trường thọ ----
message LongevityHabitRequest {
  string user_id = 1;
  double average_fasting_hours = 2;
  double average_sleep_hours = 3;
  int32 water_target_hit_days = 4;
  UserProfile profile = 5;
  double nutrition_adherence_rate = 6;
}

message LongevityHabitResponse {
  int32 longevity_score = 1;            // 0-100, deterministic
  repeated string actionable_insights = 2;
  repeated ScoreComponent score_breakdown = 3;
  repeated Citation citations = 4;
  Disclaimer disclaimer = 5;
}

// ---- 4. Hỏi tri thức sách ----
message AskLongevityQuestionRequest {
  string user_id = 1;
  string question = 2;
  UserProfile profile = 3;
  int32 chapter_filter = 4;             // 0 = tất cả
}

message AskLongevityQuestionResponse {
  string answer = 1;
  repeated Citation citations = 2;
  bool abstained = 3;
  string abstain_reason = 4;
  Disclaimer disclaimer = 5;
}

// ---- 5. Tra cứu tri thức ----
message SearchKnowledgeRequest {
  string query = 1;
  int32 chapter_filter = 2;
  int32 top_k = 3;                      // mặc định 8, tối đa 20
}

message KnowledgeSnippet {
  int32 chapter_number = 1;
  string chapter_title = 2;
  int32 book_page = 3;
  int32 pdf_page = 4;
  string text = 5;
  double score = 6;
  bool verified = 7;
}

message SearchKnowledgeResponse {
  repeated KnowledgeSnippet snippets = 1;
}

// ---- 6. Kế hoạch ăn uống theo tuần ----
message GenerateMealPlanRequest {
  UserProfile profile = 1;                  // bắt buộc
  int32 target_calories = 2;                // mục tiêu NGÀY, > 0
  double target_protein_grams = 3;          // mục tiêu NGÀY, >= 0
  double target_carbs_grams = 4;            // mục tiêu NGÀY, >= 0
  double target_fat_grams = 5;              // mục tiêu NGÀY, >= 0
  double target_fiber_grams = 6;            // tuỳ chọn; >= 0; 0 = không gửi
  string week_start_date = 7;               // ISO YYYY-MM-DD, phải là thứ Hai
  int32 days = 8;                           // 1..7
  int32 meals_per_day = 9;                  // 1..4
  repeated CandidateFood candidate_foods = 10; // >= 1; nếu > 120 cắt theo FR-025
  repeated string excluded_food_ids = 11;   // tuỳ chọn
  int32 consumed_calories = 12;             // tuỳ chọn; >= 0; 0 = không gửi
  string consumed_date = 13;                // ISO; bắt buộc nếu consumed_calories > 0
  string locale = 14;                       // chỉ hỗ trợ vi-VN
}

message GenerateMealPlanResponse {
  repeated MealPlanDay days = 1;
  repeated Citation citations = 2;          // hợp nhất citation đã verify
  repeated PlanWarning warnings = 3;
  Disclaimer disclaimer = 4;
  bool used_fallback = 5;
  string week_start_date = 6;               // ISO
  int32 days_requested = 7;                 // số ngày caller yêu cầu
  int32 weekly_target_calories = 8;         // target ngày × days_requested
  double weekly_target_protein_grams = 9;
  double weekly_target_carbs_grams = 10;
  double weekly_target_fat_grams = 11;
  double weekly_target_fiber_grams = 12;    // 0 nếu không gửi
}

message MealPlanDay {
  string date = 1;                          // ISO YYYY-MM-DD
  int32 target_calories = 2;
  double target_protein_grams = 3;
  double target_carbs_grams = 4;
  double target_fat_grams = 5;
  double target_fiber_grams = 6;
  int32 total_calories = 7;
  double total_protein_grams = 8;
  double total_carbs_grams = 9;
  double total_fat_grams = 10;
  double total_fiber_grams = 11;
  repeated PlannedMeal meals = 12;          // rỗng nếu calorie_cap_reached
  repeated PlanSuggestion suggestions = 13; // tối đa 3/ngày; đồ uống 0 calo/thói quen
  bool calorie_cap_reached = 14;
}

message PlannedMeal {
  MealType meal_type = 1;
  int32 target_calories = 2;
  repeated PlannedFood foods = 3;           // tối đa 5, thuộc candidate_foods
  string rationale = 4;                     // do code dựng từ request/citation
  repeated Citation citations = 5;          // đã verify; có thể rỗng
}

message PlannedFood {
  string food_id = 1;
  string name = 2;
  string serving_size = 3;
  string serving_unit = 4;
  int32 calories = 5;
  double protein_grams = 6;
  double carbs_grams = 7;
  double fat_grams = 8;
  double fiber_grams = 9;
  repeated string allergens = 10;
}

message PlanSuggestion {
  string text = 1;
  repeated Citation citations = 2;
}

message PlanWarning {
  string code = 1;                          // mã ổn định (xem §5.3)
  string message = 2;                       // tiếng Việt, ngắn gọn
  string scope = 3;                         // "plan" hoặc ngày ISO
}

// ---- 7. Kế hoạch tập luyện theo tuần ----
message GenerateWorkoutPlanRequest {
  UserProfile profile = 1;                  // bắt buộc
  UserGoal goal = 2;                        // bắt buộc, khác GOAL_UNSPECIFIED
  string week_start_date = 3;               // ISO YYYY-MM-DD, thứ Hai
  int32 days = 4;                           // 1..7
  int32 sessions_per_week = 5;              // 0 = min(3, days); khác 1..7; <= days
  int32 minutes_per_session = 6;            // 0 = mặc định 30; khác 10..120
  repeated string activity_preference = 7;  // có thể rỗng
  repeated string equipment_available = 8;  // rỗng = không có thiết bị
  repeated string limitations = 9;          // tuỳ chọn
  string locale = 10;                       // chỉ hỗ trợ vi-VN
}

message GenerateWorkoutPlanResponse {
  repeated WorkoutPlanDay days = 1;
  repeated Citation citations = 2;          // hợp nhất citation đã verify
  repeated PlanWarning warnings = 3;
  Disclaimer disclaimer = 4;
  bool used_fallback = 5;
  string week_start_date = 6;               // ISO
  int32 days_requested = 7;                 // số ngày caller yêu cầu
  int32 sessions_scheduled = 8;             // số buổi thực tế sau mặc định/giới hạn
  int32 minutes_per_session = 9;
}

message WorkoutPlanDay {
  string date = 1;                          // ISO YYYY-MM-DD
  bool rest_day = 2;
  string activity = 3;                      // rỗng nếu rest_day
  int32 duration_minutes = 4;               // 0 nếu rest_day
  string intensity = 5;                     // rỗng nếu không có căn cứ sách
  repeated string equipment_used = 6;
  string rationale = 7;                     // do code dựng từ request/citation
  repeated Citation citations = 8;          // đã verify; rỗng nếu rest_day/corpus rỗng
}
```

**Quy tắc bắt buộc**

- `candidate_foods` là **danh mục an toàn** do caller gửi; service **chỉ được chọn/pha trong danh mục này** và trả về `food_id` map ngược về `FoodItems`.
- Lọc dị ứng 100% và rule chạm trần 105% calo thực hiện bằng **code**, không phải bằng prompt.
- Mỗi bữa trả về **tối đa 5 món** (`meals[]`). `food_id` rỗng chỉ dùng cho gợi ý ngoài danh mục: khi `calorie_cap_reached = true` service chỉ trả đồ uống 0 calo cùng mẹo thư giãn/giấc ngủ.
- `estimated_calories` và macro của mỗi món lấy **nguyên giá trị từ `candidate_foods`** đã chọn; mô hình chỉ chọn món, viết `rationale` và trả `citations`.
- `quote` trong `Citation` phải khớp chuỗi với chunk nguồn; không khớp thì bỏ citation, mất hết citation thì response chuyển sang `abstained`.
- Mọi response có `disclaimer`; `abstained = true` khi corpus không đủ căn cứ.
- **Từ chối vì lý do y tế** (thuốc, liều, chẩn đoán, thay thế điều trị): `answer` là thông điệp hướng người dùng tới bác sĩ, `citations` **rỗng**, `abstained = false`. Client phân biệt "từ chối" với "có câu trả lời" bằng **không có citation**; contract chưa có trường `refused` (xem `specs/duck-001-recommendation-rag/deferred.md`).
- **Hỏi về chế độ ăn cho người mắc bệnh** vẫn được trả lời ở mức lối sống, kèm `disclaimer`.
- **Deadline phía server**: cấu hình qua `RECOMMENDATION__DEADLINE_SECONDS` (mặc định **30s**, hợp lệ 5–120). Deadline phải **lớn hơn** latency sinh văn bản thực tế của provider.
- `DailyNutrientSummary.micronutrients`: caller gửi vi chất nào thì service tính gap cho vi chất đó; **không gửi** một vi chất nghĩa là *chưa có dữ liệu*, **không** phải đã nạp 0. Đơn vị không quy đổi được (ví dụ `IU`) bị bỏ qua thay vì đoán.
- Knowledge store: `recommendation_db` (schema `kb`, pgvector) — chỉ chứa corpus tri thức đã nhúng, **không** chứa dữ liệu người dùng. Xem [ADR 002](../decisions/002-recommendation-knowledge-base.md).

### 5.1 Hai RPC kế hoạch tuần

Hai RPC chỉ **đọc** corpus qua `IKnowledgeStore`, không ghi DB, không tạo bảng/migration và không lưu kế hoạch: caller (Diet/Progress) chịu trách nhiệm lưu kết quả. Corpus rỗng hoặc provider lỗi không làm RPC thất bại; service trả kế hoạch tất định (fallback), kèm `warnings[]` nêu rõ phần không kiểm chứng được.

- `GenerateMealPlan`: dựng kế hoạch `days` ngày × `meals_per_day` bữa; chỉ chọn `food_id` trong `candidate_foods`; lọc dị ứng + dietary preference + `excluded_food_ids` **bằng code**; dinh dưỡng sao chép nguyên từ candidate. Ngày đã ăn ≥ 105% mục tiêu (`consumed_calories` + `consumed_date` trong tuần) có `calorie_cap_reached = true`, `meals` rỗng và tối đa 3 `suggestions`.
- `GenerateWorkoutPlan`: `days` ngày liên tiếp, đúng `sessions_per_week` buổi khi còn hoạt động an toàn; ngày còn lại là `rest_day` (không `activity`, `duration_minutes = 0`). `activity` là khoá catalog (`walking`, `cycling`, …), không phải tên hiển thị; `intensity` chỉ có khi tìm được căn cứ chương 5.
- Cả hai RPC luôn trả `disclaimer`; `rationale` do code dựng từ request + citation, không để LLM viết tự do.
- Citation meal plan: món/bữa chỉ chương 4 + Phụ lục A; riêng `suggestions` ngày chạm trần có thể dùng chương 11 cho đoạn nước/trà thảo mộc không đường, không lấy ngữ cảnh bệnh nhân/FMD/điều trị.

### 5.2 Mã lỗi nghiệp vụ

Lỗi trả qua `RpcException`; mã ổn định nằm ở trailer `code`, mô tả tiếng Việt ở `Status.Detail`.

| Mã | gRPC status | Điều kiện |
| --- | --- | --- |
| `recommendation.missing_targets` | `INVALID_ARGUMENT` | Thiếu `target_calories` hoặc target protein/carbs/fat |
| `recommendation.invalid_targets` | `INVALID_ARGUMENT` | Target calo/macro/fiber âm hoặc không hợp lệ |
| `recommendation.invalid_consumed_calories` | `INVALID_ARGUMENT` | `consumed_calories` âm |
| `recommendation.invalid_week_start` | `INVALID_ARGUMENT` | `week_start_date` sai định dạng ISO hoặc không phải thứ Hai |
| `recommendation.invalid_days` | `INVALID_ARGUMENT` | `days` ngoài 1..7 |
| `recommendation.invalid_meals_per_day` | `INVALID_ARGUMENT` | `meals_per_day` ngoài 1..4 |
| `recommendation.invalid_profile` | `INVALID_ARGUMENT` | Profile thiếu/sai trường bắt buộc |
| `recommendation.invalid_goal` | `INVALID_ARGUMENT` | `goal = GOAL_UNSPECIFIED` hoặc không hợp lệ |
| `recommendation.invalid_candidate_food` | `INVALID_ARGUMENT` | Món thiếu `food_id`/`name` hoặc dinh dưỡng âm |
| `recommendation.invalid_workout_params` | `INVALID_ARGUMENT` | `sessions_per_week`/`minutes_per_session` ngoài khoảng hoặc `sessions_per_week > days` |
| `recommendation.missing_consumed_date` | `INVALID_ARGUMENT` | `consumed_calories > 0` nhưng thiếu `consumed_date` |
| `recommendation.invalid_consumed_date` | `INVALID_ARGUMENT` | `consumed_date` sai định dạng ISO |
| `recommendation.no_candidate_food` | `FAILED_PRECONDITION` | `candidate_foods` rỗng |
| `recommendation.all_candidates_excluded` | `FAILED_PRECONDITION` | Sau lọc allergen/dietary/`excluded_food_ids` không còn món |

### 5.3 Mã warning ổn định

Warning nằm trong `warnings[]` (`PlanWarning.code` + `message` tiếng Việt + `scope` = `"plan"` hoặc ngày ISO) và **không** làm RPC thất bại.

| Mã | Scope | Khi nào |
| --- | --- | --- |
| `corpus_not_loaded` | `plan` | Không truy xuất được chunk nào |
| `ai_fallback_used` | `plan` | AI lỗi, timeout hoặc kết quả không hợp lệ nên dùng fallback tất định |
| `target_not_met` | `plan`/ngày | Candidate không đủ để đạt ±10% ngân sách ngày |
| `fiber_below_target` | ngày | Tổng fiber ngày thấp hơn mục tiêu fiber caller gửi (target > 0) |
| `candidate_foods_truncated` | `plan` | Cắt `candidate_foods` > 120 |
| `duplicate_candidate_food` | `plan` | Trùng `food_id` trong `candidate_foods` |
| `unsupported_locale` | `plan` | `locale` khác `vi-VN`; service dùng `vi-VN` |
| `consumed_date_outside_plan` | `plan` | `consumed_date` ngoài tuần nên bỏ qua rule 105% |
| `suggestion_source_missing` | ngày | Ngày chạm trần nhưng không có căn cứ sách cho `suggestions` |
| `no_limitations_assumed` | `plan` | `limitations` rỗng; chỉ dùng hướng dẫn chung |
| `unsupported_activity_preference` | `plan` | Preference không có trong catalog hoặc không khả thi với thiết bị |
| `no_safe_activity` | ngày | Limitations/thiết bị loại hết hoạt động an toàn của ngày |
| `unsupported_limitation` | `plan` | Limitation không khớp bảng ánh xạ đóng nên bị bỏ qua |
| `intensity_unverified` | ngày | Không có câu chương 5 hỗ trợ cường độ nên `intensity` rỗng |
| `activity_source_unverified` | `plan`/ngày | Corpus rỗng, hoạt động/thời lượng chỉ lấy từ dữ liệu caller gửi |

### 5.4 Deadline

- Hai RPC plan (`GenerateMealPlan`, `GenerateWorkoutPlan`): `RECOMMENDATION__PLAN_DEADLINE_SECONDS`, mặc định **90s**, hợp lệ 5–120.
- Năm RPC còn lại: `RECOMMENDATION__DEADLINE_SECONDS`, mặc định **30s**, hợp lệ 5–120.
- Deadline phía server phải lớn hơn latency sinh văn bản thực tế của provider; giá trị ngoài khoảng làm service fail-fast lúc khởi động.

### 5.5 Metadata phản hồi

- `GenerateMealPlanResponse`: `week_start_date`, `days_requested`, `weekly_target_calories`, `weekly_target_protein_grams`, `weekly_target_carbs_grams`, `weekly_target_fat_grams`, `weekly_target_fiber_grams` (0 nếu không gửi), `used_fallback`, `citations`, `warnings`, `disclaimer`.
- `GenerateWorkoutPlanResponse`: `week_start_date`, `days_requested`, `sessions_scheduled` (số buổi thực tế sau mặc định/giới hạn an toàn), `minutes_per_session`, `used_fallback`, `citations`, `warnings`, `disclaimer`.
- `MealPlanDay`: ngân sách (`target_*`) và tổng thực tế (`total_*`) của ngày, `calorie_cap_reached`.
- `WorkoutPlanDay`: `rest_day`, `activity`, `duration_minutes`, `intensity`, `equipment_used`, `rationale`, `citations`.
- `PlanWarning`: `code`, `message`, `scope` — caller dựa vào `code` (không dựa vào `message`) để xử lý.

---

## 6. Background Worker & RabbitMQ Events (Dev 5 phụ trách)
> **Nền tảng**: RabbitMQ + MassTransit Consumer | **Database**: Không sở hữu DB nghiệp vụ

### 6.1 `UserRegisteredEvent`
- **Exchange**: `longevia.events` (`topic`)
- **Routing key**: `identity.user.registered`
- **Producer**: Identity Service (qua Transactional Outbox)
- **Consumer**: `UserRegisteredConsumer` (Background Worker)
- **Schema**:
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "alex.minh@example.com",
    "fullName": "Alex Minh",
    "verificationToken": "abc123token...",
    "occurredOn": "2026-10-21T14:30:00Z"
  }
  ```
- **Hành động**: Worker gửi email qua SMTP relay được cấu hình (Mailpit local, Brevo production), kèm verification link dùng GET trực tiếp:
  `http://localhost:8080/api/identity/auth/verify?token=abc123token...`

### 6.2 `PasswordResetRequestedEvent`
- **Exchange**: `longevia.events` (`topic`)
- **Routing key**: `identity.user.password-reset-requested`
- **Producer**: Identity Service
- **Consumer**: `PasswordResetRequestedConsumer` (Background Worker, queue `worker.email`)
- **Schema**:
  ```json
  {
    "email": "alex.minh@example.com",
    "resetToken": "xyz789reset...",
    "expiresAt": "2026-10-21T14:45:00Z",
    "occurredOn": "2026-10-21T14:30:00Z"
  }
  ```
- **Schema version**: v1 (`src/Shared/Events/PasswordResetRequestedEvent.cs`).
- **Hành động**: Worker gửi email chứa mã/link đặt lại mật khẩu có hạn 15 phút qua SMTP relay cấu hình (Mailpit local, Brevo production).
