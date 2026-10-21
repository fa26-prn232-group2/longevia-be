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
  - `password`: Tối thiểu 8 ký tự, có ít nhất 1 chữ hoa, 1 chữ số, 1 ký tự đặc biệt.
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

### 2.2 `POST /api/identity/auth/login` — Đăng nhập
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

### 2.3 `POST /api/identity/auth/refresh` — Làm mới Access Token
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

### 2.4 `POST /api/identity/auth/logout` — Đăng xuất
- **Xác thực**: Bearer JWT
- **Request Body**:
  ```json
  {
    "refreshToken": "9a12c45e-88bc-4613-9031-6b2158fe2331"
  }
  ```
- **Response `204 No Content`**

---

### 2.5 `GET /api/identity/users/me` — Lấy thông tin cá nhân
- **Xác thực**: Bearer JWT
- **Response `200 OK`**:
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "alex.minh@example.com",
    "fullName": "Alex Minh",
    "avatarUrl": "https://res.cloudinary.com/longevia/avatar.jpg",
    "profile": {
      "birthDate": "1998-05-15",
      "biologicalSex": "Male",
      "heightCm": 175.5,
      "activityLevel": "ModeratelyActive",
      "dietaryPreference": "Mediterranean",
      "allergies": ["Peanuts", "Shellfish"]
    }
  }
  ```

---

### 2.6 `PUT /api/identity/users/me/profile` — Cập nhật hồ sơ thể trạng cơ sở
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
  - `heightCm`: Số thực từ 50.0 đến 250.0 cm.
  - `activityLevel`: Một trong các giá trị `["Sedentary", "LightlyActive", "ModeratelyActive", "VeryActive"]`.
  - `dietaryPreference`: Một trong `["None", "Keto", "Vegan", "Mediterranean", "LowCarb"]`.
- **Response `200 OK`**: Trả về dữ liệu profile sau khi cập nhật.

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
- **Response `200 OK`**:
  ```json
  {
    "id": "11fa24e8-466d-4ee8-a6b3-6c7081734a91",
    "bmr": 1720,
    "tdee": 2450,
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

File hợp đồng: [`contracts/recommendation/v1/recommendation.proto`](file:///d:/FPT_EDUCATION/FPTCN8/_0PRN232/longevia-be/contracts/identity/v1/identity.proto)

```protobuf
syntax = "proto3";

package longevia.recommendation.v1;
option csharp_namespace = "Longevia.Recommendation.Contracts.V1";

service RecommendationService {
  // Gợi ý bữa ăn dựa trên macro còn thiếu và sở thích dinh dưỡng
  rpc GetMealRecommendations (MealRecommendationRequest) returns (MealRecommendationResponse);
  
  // Phân tích thiếu hụt vi chất trong tuần
  rpc AnalyzeNutrientGaps (NutrientGapRequest) returns (NutrientGapResponse);
  
  // Đánh giá thói quen sống và chấm điểm Longevity
  rpc EvaluateLongevityHabits (LongevityHabitRequest) returns (LongevityHabitResponse);
}

message MealRecommendationRequest {
  string user_id = 1;
  int32 target_calories_remaining = 2;
  double target_protein_remaining = 3;
  double target_carbs_remaining = 4;
  double target_fat_remaining = 5;
  repeated string allergies = 6;
  string dietary_preference = 7;
}

message MealRecommendationResponse {
  repeated RecommendedMeal meals = 1;
  string longevity_tip = 2;
}

message RecommendedMeal {
  string name = 1;
  string description = 2;
  int32 estimated_calories = 3;
  double protein_grams = 4;
  double carbs_grams = 5;
  double fat_grams = 6;
  repeated string key_micronutrients = 7;
}

message NutrientGapRequest {
  string user_id = 1;
  repeated DailyNutrientRecord past_7_days = 2;
}

message DailyNutrientRecord {
  string date = 1;
  int32 calories = 2;
  double protein = 3;
  double carbs = 4;
  double fat = 5;
  double fiber = 6;
}

message NutrientGapResponse {
  repeated string deficient_nutrients = 1;
  repeated string suggested_foods = 2;
  string analysis_summary = 3;
}

message LongevityHabitRequest {
  string user_id = 1;
  double average_fasting_hours = 2;
  double average_sleep_hours = 3;
  int32 water_target_hit_days = 4;
}

message LongevityHabitResponse {
  int32 longevity_score = 1; // Thang điểm 1 - 100
  repeated string actionable_insights = 2;
}
```

---

## 6. Background Worker & RabbitMQ Events (Dev 5 phụ trách)
> **Nền tảng**: RabbitMQ + MassTransit Consumer | **Database**: Không sở hữu DB nghiệp vụ

### 6.1 `UserRegisteredEvent`
- **Exchange**: `identity.user-registered`
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
- **Hành động**: Gọi Brevo API gửi email kích hoạt tài khoản kèm verification link:
  `http://localhost:8080/api/identity/auth/verify?token=abc123token...`

### 6.2 `PasswordResetRequestedEvent`
- **Exchange**: `identity.password-reset-requested`
- **Producer**: Identity Service
- **Consumer**: `PasswordResetConsumer` (Background Worker)
- **Schema**:
  ```json
  {
    "email": "alex.minh@example.com",
    "resetToken": "xyz789reset...",
    "expiresAt": "2026-10-21T14:45:00Z"
  }
  ```
- **Hành động**: Gọi Brevo API gửi email chứa mã bảo mật hoặc đường link đổi mật khẩu có hạn 15 phút.
