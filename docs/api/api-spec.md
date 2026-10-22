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
> **Tri thức**: RAG trên corpus *Chế độ ăn trường thọ* — mọi khuyến nghị phải có `citations[]` (chương + trang sách bản in + trích đoạn) hoặc phải `abstained`.

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
```

**Quy tắc bắt buộc**

- `candidate_foods` là **danh mục an toàn** do caller gửi; service **chỉ được chọn/pha trong danh mục này** và trả về `food_id` map ngược về `FoodItems`.
- Lọc dị ứng 100% và rule chạm trần 105% calo thực hiện bằng **code**, không phải bằng prompt.
- Mỗi bữa trả về **tối đa 5 món** (`meals[]`). `food_id` rỗng chỉ dùng cho gợi ý ngoài danh mục: khi `calorie_cap_reached = true` service chỉ trả đồ uống 0 calo cùng mẹo thư giãn/giấc ngủ.
- `estimated_calories` và macro của mỗi món lấy **nguyên giá trị từ `candidate_foods`** đã chọn; mô hình chỉ chọn món, viết `rationale` và trả `citations`.
- `quote` trong `Citation` phải khớp chuỗi với chunk nguồn; không khớp thì bỏ citation, mất hết citation thì response chuyển sang `abstained`.
- Mọi response có `disclaimer`; `abstained = true` khi corpus không đủ căn cứ.
- Knowledge store: `recommendation_db` (schema `kb`, pgvector) — chỉ chứa corpus tri thức đã nhúng, **không** chứa dữ liệu người dùng. Xem [ADR 002](../decisions/002-recommendation-knowledge-base.md).

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
