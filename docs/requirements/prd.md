# Product Requirement Document (PRD) — Longevia Backend

---

## 1. Tổng quan sản phẩm & Kiến trúc hệ thống

Tài liệu này đặc tả chi tiết các yêu cầu kỹ thuật và chức năng cho **Longevia Backend**, được thiết kế theo kiến trúc **Microservices** chạy trên nền **ASP.NET Core (.NET 10)**, cơ sở dữ liệu **PostgreSQL 17**, giao tiếp bất đồng bộ qua **RabbitMQ**, và định tuyến công khai duy nhất qua **Nginx Gateway**.

Hệ thống bao gồm 5 dịch vụ thành phần:
1. **Identity Service** (Client-facing qua Nginx, sở hữu `identity_db`)
2. **Diet Service** (Client-facing qua Nginx, sở hữu `diet_db`)
3. **Progress Service** (Client-facing qua Nginx, sở hữu `progress_db`)
4. **Recommendation Service** (Nội bộ gRPC-only, sở hữu `recommendation_db` chỉ chứa corpus tri thức đã nhúng — không chứa dữ liệu người dùng)
5. **Background Worker** (Consumer ngầm từ RabbitMQ, tích hợp Brevo Email API)

```
                       [ Client: Web / Mobile ]
                                  │ (HTTP/HTTPS)
                                  ▼
                   ┌──────────────────────────────┐
                   │    Nginx API Gateway (:8080) │
                   └──────────────┬───────────────┘
                                  │
         ┌────────────────────────┼────────────────────────┐
         │ /api/identity/         │ /api/diet/             │ /api/progress/
         ▼                        ▼                        ▼
 ┌───────────────┐        ┌───────────────┐        ┌───────────────┐
 │ Identity Svc  │        │   Diet Svc    │        │  Progress Svc │
 │ `identity_db` │        │   `diet_db`   │        │ `progress_db` │
 └───┬───────────┘        └───┬───────┬───┘        └───────┬───────┘
     │                        │       │                    │
      │ (Publish Event)        │       │ (gRPC :50051)      │ (Publish Event)
      │                        ▼       ▼                    │
      │                  ┌──────────────────────┐           │
      │                  │  Recommendation Svc  │           │
      │                  │ (RAG + AI Engine)    │           │
      │                  └──────────┬───────────┘           │
      │                             │ (read/write kb)       │
      │                             ▼                       │
      │                  ┌──────────────────────┐           │
      │                  │ `recommendation_db`  │           │
      │                  └──────────────────────┘           │
      │                                                     │
      └────────────────────────┬────────────────────────────┘
                               ▼ (Transactional Outbox)
                      ┌──────────────────┐
                      │     RabbitMQ     │
                      └────────┬─────────┘
                               │ (Consume Event)
                               ▼
                      ┌──────────────────┐
                      │ Background Worker│ ──► [Brevo Email API]
                      └──────────────────┘
```

> Tri thức cho RAG được nạp **offline** bằng pipeline `scripts/book-kb` từ
> [`docs/reference/che-do-an-truong-tho/`](../reference/che-do-an-truong-tho/README.md)
> vào `recommendation_db` — không phải traffic runtime.

---

## 2. Đặc tả chi tiết từng Microservice

---

### 2.1 Identity Service

#### A. Mục tiêu & Trách nhiệm
Quản lý vòng đời tài khoản người dùng, phân quyền truy cập, phát hành mã xác thực JWT bất đối xứng (ES256) và lưu trữ hồ sơ thể trạng cơ sở.

#### B. Danh sách User Stories & Acceptance Criteria

##### US-ID-01: Đăng ký tài khoản (User Registration)
- **Mô tả**: Là người dùng mới, tôi muốn đăng ký tài khoản bằng email và mật khẩu để bắt đầu sử dụng hệ thống.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Email phải đúng định dạng chuẩn RFC 5322; từ chối và trả lỗi `409 Conflict` (`Code = "identity.email_taken"`) nếu email đã tồn tại.
  - `AC-2`: Mật khẩu tối thiểu 8 ký tự, có ít nhất 1 ký tự in hoa, 1 ký tự số và 1 ký tự đặc biệt.
  - `AC-3`: Mật khẩu được băm an toàn bằng thuật toán BCrypt (hoặc Argon2id) trước khi lưu vào DB.
  - `AC-4`: Lưu thành công tài khoản ở trạng thái `PendingVerification`, đồng thời ghi một bản ghi sự kiện `UserRegisteredEvent` vào bảng Outbox trong cùng một transaction DB.
  - `AC-5`: Trả về mã HTTP `201 Created` kèm thông tin cơ bản của tài khoản (không bao gồm hash mật khẩu).

##### US-ID-02: Đăng nhập & Cấp phát Token (Authentication & Token Issuance)
- **Mô tả**: Là người dùng đã có tài khoản, tôi muốn đăng nhập bằng email và mật khẩu để nhận token truy cập các tính năng.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Xác thực mật khẩu khớp với bản hash trong DB; nếu sai thông tin, trả về `401 Unauthorized` (`Code = "identity.invalid_credentials"`).
  - `AC-2`: Khi đăng nhập thành công, hệ thống sử dụng **Signing Private Key ES256** để ký phát hành `AccessToken` (thời hạn sống 15 phút). Header của JWT chứa `kid` xác định key version (theo [ADR 001](../decisions/001-authn-authz-architecture.md)).
  - `AC-3`: Sinh ngẫu nhiên mã `RefreshToken` bảo mật cao (thời hạn 7 ngày), lưu vào DB kèm `UserId`, `ExpiresAt`, và trạng thái `IsRevoked = false`.
  - `AC-4`: Trả về `200 OK` chứa `accessToken`, `refreshToken`, `tokenType: "Bearer"`, và `expiresIn: 900`.

##### US-ID-03: Cấp mới Access Token (Token Refreshing)
- **Mô tả**: Là client ứng dụng, tôi muốn dùng Refresh Token để đổi lấy Access Token mới khi token cũ hết hạn mà không bắt người dùng đăng nhập lại.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Kiểm tra `RefreshToken` có tồn tại, chưa hết hạn và chưa bị thu hồi (`IsRevoked == false`). Nếu không hợp lệ, trả về `401 Unauthorized` (`Code = "identity.invalid_refresh_token"`).
  - `AC-2`: Áp dụng kỹ thuật Refresh Token Rotation: Hủy token cũ và phát hành cặp `AccessToken` + `RefreshToken` mới.

##### US-ID-04: Quản lý Hồ sơ thể trạng cơ bản (User Health Baseline)
- **Mô tả**: Là người dùng, tôi muốn cập nhật thông tin cá nhân và thể trạng nền tảng để hệ thống cá nhân hóa dinh dưỡng.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Cho phép xem thông tin qua `GET /api/identity/users/me`.
  - `AC-2`: Cho phép cập nhật thông tin qua `PUT /api/identity/users/me/profile` gồm: `BirthDate`, `BiologicalSex` (Male/Female), `HeightCm` (chiều cao cm), `ActivityLevel` (Sedentary, LightlyActive, ModeratelyActive, VeryActive), `DietaryPreference` (None, Keto, Vegan, Mediterranean, LowCarb), và danh sách `Allergies` (dị ứng thực phẩm).
  - `AC-3`: Validate dữ liệu: `HeightCm` trong khoảng 50–250 cm; ngày sinh phải chứng minh người dùng ≥ 13 tuổi.

#### C. Thiết kế REST Endpoints (Identity Service)
| Method | Route | Chức năng | Xác thực |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/identity/auth/register` | Đăng ký tài khoản | Public |
| `POST` | `/api/identity/auth/login` | Đăng nhập lấy cặp Token | Public |
| `POST` | `/api/identity/auth/refresh` | Đổi Access Token mới qua Refresh Token | Public |
| `POST` | `/api/identity/auth/logout` | Thu hồi Refresh Token | Bearer JWT |
| `GET` | `/api/identity/users/me` | Lấy thông tin tài khoản hiện tại | Bearer JWT |
| `PUT` | `/api/identity/users/me/profile` | Cập nhật hồ sơ thể chất & dị ứng | Bearer JWT |

---

### 2.2 Diet Service

#### A. Mục tiêu & Trách nhiệm
Quản lý danh mục thực phẩm dinh dưỡng, tính toán nhu cầu năng lượng (TDEE/BMR/Macro), quản lý nhật ký ăn uống 4 bữa trong ngày, và kết nối gRPC với Recommendation Service để nhận gợi ý thực đơn.

#### B. Danh sách User Stories & Acceptance Criteria

##### US-DIET-01: Quản lý & Tra cứu Thực phẩm (Food Catalog)
- **Mô tả**: Là người dùng, tôi muốn tìm kiếm món ăn hoặc tự thêm món mới để ghi vào nhật ký bữa ăn.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `GET /api/diet/foods` hỗ trợ tìm kiếm theo từ khóa `search`, lọc theo `category` (Rau củ, Thịt cá, Ngũ cốc, Đồ uống), hỗ trợ phân trang chuẩn `pageNumber`, `pageSize`.
  - `AC-2`: Mỗi thực phẩm chứa: `Name`, `ServingSize`, `ServingUnit` (g, ml, chén, đĩa), `Calories`, `Protein`, `Carbs`, `Fat`, `Fiber`.
  - `AC-3`: `POST /api/diet/foods`: Người dùng có thể thêm món ăn tùy chỉnh (Custom food); các món này chỉ hiển thị riêng cho người tạo (`UserId`).

##### US-DIET-02: Thiết lập Mục tiêu Dinh dưỡng hàng ngày (Daily Nutrition Targets)
- **Mô tả**: Là người dùng, tôi muốn thiết lập mục tiêu calo và tỷ lệ macro hàng ngày dựa trên thể trạng của mình.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Cung cấp API tự động tính toán BMR (Mifflin-St Jeor) và TDEE dựa trên hồ sơ thể trạng.
  - `AC-2`: `PUT /api/diet/targets/me`: Lưu mục tiêu năng lượng: `TargetCalories`, `TargetProteinGrams`, `TargetCarbsGrams`, `TargetFatGrams`. Tổng năng lượng từ Protein (4 kcal/g) + Carbs (4 kcal/g) + Fat (9 kcal/g) phải sai số không quá ±5% so với `TargetCalories`.

##### US-DIET-03: Ghi nhận Nhật ký Bữa ăn (Meal Logging)
- **Mô tả**: Là người dùng, tôi muốn ghi nhận những món mình đã ăn trong từng bữa để kiểm soát lượng năng lượng nạp vào.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `POST /api/diet/meals`: Cho phép thêm nhiều món ăn vào một bữa trong ngày (`MealType`: Breakfast, Lunch, Dinner, Snack) kèm khối lượng thực tế (`Quantity`).
  - `AC-2`: Tự động nhân tỷ lệ dinh dưỡng theo khối lượng và lưu vào `MealLogItem`.
  - `AC-3`: `GET /api/diet/meals/daily-summary?date=YYYY-MM-DD`: Tổng hợp lượng calo và macro đã ăn trong ngày, tính toán tỷ lệ % hoàn thành so với mục tiêu (`RemainingCalories`, `ConsumedProtein`, v.v.).

##### US-DIET-04: Nhận Gợi ý Thực đơn từ AI qua gRPC
- **Mô tả**: Là người dùng, tôi muốn nhận gợi ý món ăn phù hợp cho bữa kế tiếp dựa trên lượng calo/macro còn thiếu trong ngày.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `GET /api/diet/meals/ai-recommendations`: Backend của Diet Service tính toán số calo/macro còn thiếu trong ngày hiện tại.
  - `AC-2`: Diet Service đóng vai trò gRPC Client, gọi phương thức `GetMealRecommendations` sang **Recommendation Service** qua mạng nội bộ `internal_net`.
  - `AC-3`: Nhận danh sách các món ăn gợi ý từ AI, kiểm tra loại trừ dị ứng và trả kết quả cho client trong vòng dưới 3 giây.

#### C. Thiết kế REST Endpoints (Diet Service)
| Method | Route | Chức năng | Xác thực |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/diet/foods` | Tìm kiếm món ăn trong thư viện | Bearer JWT |
| `POST` | `/api/diet/foods` | Tạo món ăn tùy chỉnh cá nhân | Bearer JWT |
| `GET` | `/api/diet/targets/me` | Lấy mục tiêu dinh dưỡng hiện tại | Bearer JWT |
| `PUT` | `/api/diet/targets/me` | Cập nhật mục tiêu Calo & Macro | Bearer JWT |
| `POST` | `/api/diet/meals` | Ghi log bữa ăn (Sáng, Trưa, Tối, Phụ) | Bearer JWT |
| `GET` | `/api/diet/meals/daily-summary` | Xem báo cáo tổng nạp trong ngày | Bearer JWT |
| `GET` | `/api/diet/meals/ai-recommendations`| Nhận gợi ý món ăn từ AI (qua gRPC) | Bearer JWT |

---

### 2.3 Progress Service

#### A. Mục tiêu & Trách nhiệm
Theo dõi và lưu vết sự biến đổi của các chỉ số sinh học cơ thể (Biometrics/Biomarkers), quản lý các thói quen trường thọ (nhịn ăn gián đoạn, nước uống, giấc ngủ), và tính toán biểu đồ xu hướng thể trạng.

#### B. Danh sách User Stories & Acceptance Criteria

##### US-PROG-01: Ghi nhận Chỉ số Thể trạng (Body Metrics Tracking)
- **Mô tả**: Là người dùng, tôi muốn lưu lại cân nặng và các số đo cơ thể theo thời gian để theo dõi tiến độ.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `POST /api/progress/metrics`: Lưu bản ghi gồm `LoggedAt`, `WeightKg`, `BodyFatPercent` (tùy chọn), `WaistCircumferenceCm` (tùy chọn), `SystolicBloodPressure` (huyết áp tâm thu - tùy chọn), `DiastolicBloodPressure` (huyết áp tâm trương - tùy chọn), `RestingHeartRate` (nhịp tim nghỉ - tùy chọn).
  - `AC-2`: Tự động tính chỉ số `BMI = WeightKg / (HeightM^2)` và lưu cùng bản ghi.
  - `AC-3`: `GET /api/progress/metrics/history`: Trả về danh sách lịch sử đo đạc, sắp xếp theo thời gian mới nhất, hỗ trợ phân trang.

##### US-PROG-02: Bộ đếm Nhịn ăn Gián đoạn (Intermittent Fasting Timer)
- **Mô tả**: Là người theo đuổi lối sống trường thọ, tôi muốn sử dụng đồng hồ nhịn ăn để kích hoạt quá trình tự thực bào (Autophagy) của tế bào.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `POST /api/progress/fasting/start`: Bắt đầu phiên nhịn ăn với chế độ lựa chọn (`Protocol`: 16:8, 18:6, 20:4 hoặc Tùy chỉnh).
  - `AC-2`: `POST /api/progress/fasting/end`: Kết thúc phiên nhịn ăn, ghi nhận tổng thời gian nhịn thực tế (tính bằng phút) và đánh giá đạt/chưa đạt mục tiêu.
  - `AC-3`: `GET /api/progress/fasting/current`: Xem trạng thái phiên nhịn ăn đang diễn ra (đã trôi qua bao nhiêu giờ, còn lại bao nhiêu giờ).

##### US-PROG-03: Nhật ký Uống nước & Giấc ngủ (Water & Sleep Logging)
- **Mô tả**: Là người dùng, tôi muốn theo dõi lượng nước uống và thời lượng giấc ngủ mỗi ngày.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `POST /api/progress/water/log`: Ghi nhận lượng nước nạp vào (ml).
  - `AC-2`: `GET /api/progress/water/today`: Tổng hợp lượng nước đã uống trong ngày so với mục tiêu (mặc định 2000–2500 ml).
  - `AC-3`: `POST /api/progress/sleep/log`: Ghi nhận giờ đi ngủ, giờ thức dậy, và chất lượng giấc ngủ (`SleepQuality`: Poor, Fair, Good, Excellent).

##### US-PROG-04: Thống kê Bảng điều khiển & Hệ thống Chuỗi (Dashboard & Streaks)
- **Mô tả**: Là người dùng, tôi muốn xem biểu đồ xu hướng cân nặng và chuỗi ngày duy trì thói quen để có thêm động lực.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `GET /api/progress/dashboard/summary`: Báo cáo tổng thể gồm cân nặng hiện tại, mức chênh lệch so với tuần trước, chuỗi ngày liên tiếp hiện tại (Current Streak) và chuỗi dài nhất (Best Streak).
  - `AC-2`: Một ngày được tính vào chuỗi Streak nếu người dùng hoàn thành ít nhất một trong hai hành động: Ghi log bữa ăn hoặc ghi số đo cơ thể.

#### C. Thiết kế REST Endpoints (Progress Service)
| Method | Route | Chức năng | Xác thực |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/progress/metrics` | Lưu chỉ số cân nặng, số đo cơ thể | Bearer JWT |
| `GET` | `/api/progress/metrics/history` | Lấy lịch sử biến đổi thể trạng | Bearer JWT |
| `POST` | `/api/progress/fasting/start` | Bắt đầu đếm giờ nhịn ăn | Bearer JWT |
| `POST` | `/api/progress/fasting/end` | Kết thúc phiên nhịn ăn | Bearer JWT |
| `GET` | `/api/progress/fasting/current` | Lấy trạng thái nhịn ăn hiện tại | Bearer JWT |
| `POST` | `/api/progress/water/log` | Ghi nhận lượng nước uống (ml) | Bearer JWT |
| `GET` | `/api/progress/water/today` | Xem tổng lượng nước trong ngày | Bearer JWT |
| `POST` | `/api/progress/sleep/log` | Ghi nhận thời gian và chất lượng giấc ngủ| Bearer JWT |
| `GET` | `/api/progress/dashboard/summary`| Lấy dữ liệu tổng quan & chuỗi Streak | Bearer JWT |

---

### 2.4 Recommendation Service

#### A. Mục tiêu & Trách nhiệm
Bộ não thông minh ứng dụng **Google AI (Gemini)**, giao tiếp qua giao thức **gRPC nội bộ**, hỗ trợ đưa ra các nhận định chuyên sâu về dinh dưỡng trường thọ và chống lão hóa.

Service có **tầng tri thức RAG**: mọi khuyến nghị phải bám vào **corpus tri thức** — cuốn sách *Chế độ ăn trường thọ* đã được số hoá tại [`docs/reference/che-do-an-truong-tho/`](../reference/che-do-an-truong-tho/README.md) — và **phải truy ngược được** về chương + trang sách bản in. Khi corpus không đủ để trả lời, service **phải nói ra điều đó** thay vì suy diễn.

Ngoài ra, service giữ **knowledge store** riêng (`recommendation_db`) chứa corpus đã nhúng; DB này **không chứa dữ liệu người dùng** (chi tiết ở [ADR 002](../decisions/002-recommendation-knowledge-base.md)).

#### B. Danh sách User Stories & Acceptance Criteria

##### US-REC-01: Gợi ý bữa ăn từ danh mục an toàn
- **Mô tả**: Là người dùng, tôi muốn nhận gợi ý món ăn hợp với mục tiêu dinh dưỡng hôm nay, chắc chắn không chứa thành phần tôi dị ứng.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Caller (Diet) gửi kèm `candidate_foods` — danh mục món an toàn kèm dinh dưỡng. Service **chỉ được chọn/pha trong danh mục này**, được trả về `food_id` để map về `FoodItems`; **không** được tự sinh món ngoài danh mục.
  - `AC-2`: Lọc dị ứng thực hiện **bằng code** (không phải bằng prompt): mọi món có thành phần khớp `allergies` của hồ sơ bị loại **100%** (BR-03).
  - `AC-3`: Nếu calo đã ăn hôm nay ≥ **105%** mục tiêu, service **không** gợi ý bữa ăn chính; chỉ trả gợi ý đồ uống không calo hoặc thói quen thư giãn/giấc ngủ, kèm cờ `calorie_cap_reached = true` (BR-03).
  - `AC-4`: Mỗi món gợi ý kèm `rationale` (vì sao chọn) và `citations[]` trỏ về chương + trang sách trong corpus.
  - `AC-5`: Mọi response kèm `disclaimer` theo BR-04.
  - `AC-6`: Phần dinh dưỡng (`estimated_calories`, macro) lấy từ `candidate_foods` đã chọn — **không** để LLM tự bịa.
  - `AC-7`: Hỗ trợ bữa sáng/trưa/tối/phụ và trả về **tối đa 5 món** mỗi bữa (FR-007).

##### US-REC-02: Phân tích thiếu hụt vi chất 7 ngày
- **Mô tả**: Là người dùng, tôi muốn biết trong tuần qua tôi đang thiếu vi chất nào và nên ăn gì để bù.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Request phải nhận hồ sơ người dùng (tuổi, giới, chiều cao, cân nặng, mức hoạt động, dị ứng, sở thích ăn uống) và mục tiêu dinh dưỡng — không có ngữ cảnh thì không phân tích được.
  - `AC-2`: Phần **gap vi chất tính bằng rule** (bảng RDA), `severity` theo ngưỡng % — **không** giao cho LLM tự suy luận số liệu.
  - `AC-3`: `analysis_summary` và `suggested_foods` phải **có căn cứ trong sách** (`citations[]`); phần giải thích không có trong sách phải được nói rõ là suy luận từ dữ liệu người dùng.
  - `AC-4`: Trả về đủ `deficient_nutrients[]`, `suggested_foods[]`, `analysis_summary` (giữ tương thích với contract cũ) **và** `gaps[]` chi tiết.

##### US-REC-03: Đánh giá thói quen trường thọ
- **Mô tả**: Là người dùng, tôi muốn biết điểm số và phần nào trong thói quen sống của tôi đang kéo điểm xuống.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: `longevity_score` (0–100) tính **deterministic** theo rubric cố định: fasting 35% / sleep 30% / hydration 20% / nutrition 15%.
  - `AC-2`: Trả `score_breakdown[]` gồm điểm, trọng số và lý do của từng nhánh — cùng đầu vào phải cho cùng điểm.
  - `AC-3`: `actionable_insights[]` phải có `citations[]` khi phát xuất từ sách.
  - `AC-4`: Kèm `disclaimer` (BR-04).

##### US-REC-04: Hỏi tri thức trong sách
- **Mô tả**: Là người dùng, tôi muốn hỏi trực tiếp về chế độ ăn trường thọ và nhận câu trả lời có dẫn đích.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Câu trả lời chỉ được dựa trên đoạn corpus được truy xuất; mỗi khẳng định kèm `citations[]` gồm `chapter_number`, `chapter_title`, `book_page` (trang bản in), `pdf_page` (trang file scan) và `quote` (trích đoạn nguyên văn).
  - `AC-2`: `quote` phải được **kiểm chứng khớp chuỗi** với chunk nguồn. Citation không khớp thì bị loại; mất hết citation thì response chuyển sang `abstained = true`.
  - `AC-3`: Khi corpus **không đủ** để trả lời, phải trả `abstained = true` kèm `abstain_reason` — **không** suy diễn ngoài sách.
  - `AC-4`: Nội dung từ trang chưa đối chiếu bản in phải được đánh dấu `verified = false` và không được khẳng định như sự thật y tế.
  - `AC-5`: Kèm `disclaimer` (BR-04).

##### US-REC-05: Tra cứu tri thức
- **Mô tả**: Là người dùng, tôi muốn tìm các đoạn trong sách liên quan đến một chủ đề.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Trả danh sách `snippets[]` gồm nội dung đoạn, chương, trang sách, trang PDF và điểm liên quan.
  - `AC-2`: Hỗ trợ lọc theo chương; `top_k` mặc định 8, tối đa 20.

##### US-REC-06: Vòng đời tri thức (nội bộ, offline)
- **Mô tả**: Là người bảo trì, tôi muốn nạp lại corpus khi tài liệu tham chiếu thay đổi mà không phải sửa code.
- **Tiêu chí nghiệm thu (Acceptance Criteria)**:
  - `AC-1`: Pipeline offline (`scripts/book-kb`) chunk → embed → nạp `recommendation_db`; **không** nằm trong runtime service.
  - `AC-2`: Chunk lưu `book_page_from/to`, `pdf_page_from/to`; nguồn số trang là marker `<!-- sách N · PDF M -->` trong bản Markdown.
  - `AC-3`: Nạp lại idempotent, không tạo chunk trùng (`corpus_version` = hash nội dung corpus).
  - `AC-4`: Chunk từ trang có nghi vấn số liệu (`review/numbers.md`, `review/qa.md`) mang cờ `needs_review = true`.

#### C. Định nghĩa Hợp đồng Protobuf (`contracts/recommendation/v1/recommendation.proto`)

```protobuf
syntax = "proto3";

package longevia.recommendation.v1;

option csharp_namespace = "Longevia.Recommendation.Contracts.V1";

service RecommendationService {
  rpc GetMealRecommendations   (MealRecommendationRequest)    returns (MealRecommendationResponse);
  rpc AnalyzeNutrientGaps      (NutrientGapRequest)           returns (NutrientGapResponse);
  rpc EvaluateLongevityHabits  (LongevityHabitRequest)       returns (LongevityHabitResponse);
  rpc AskLongevityQuestion     (AskLongevityQuestionRequest)  returns (AskLongevityQuestionResponse);
  rpc SearchLongevityKnowledge (SearchKnowledgeRequest)       returns (SearchKnowledgeResponse);
}

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

// ---- Dữ liệu dùng chung ----

message UserProfile {
  int32 age = 1;
  BiologicalSex sex = 2;
  double height_cm = 3;
  double weight_kg = 4;
  string activity_level = 5;      // Sedentary | LightlyActive | ModeratelyActive | VeryActive
  string dietary_preference = 6;  // None | Keto | Vegan | Mediterranean | LowCarb
  repeated string allergies = 7;
}

message Citation {
  int32 chapter_number = 1;       // 0 = phần đầu sách
  string chapter_title = 2;
  int32 book_page = 3;             // trang BẢN IN - nguồn chuẩn
  int32 pdf_page = 4;              // trang file PDF scan - đối chiếu
  string quote = 5;                // trích đoạn nguyên văn, đã verify khớp chunk
  bool verified = 6;               // false = trang chưa đối chiếu tay / có cờ số liệu
  string source_file = 7;
}

message ScoreComponent {
  string key = 1;                  // fasting | sleep | hydration | nutrition
  int32 score = 2;                 // 0-100 của nhánh
  int32 weight = 3;                // trọng số %
  string reason = 4;
}

message Disclaimer {
  string text = 1;
}

// ---- 1. Gợi ý bữa ăn ----

message CandidateFood {
  string food_id = 1;              // map về FoodItems của Diet
  string name = 2;
  string serving_size = 3;
  string serving_unit = 4;
  int32 calories = 5;
  double protein_grams = 6;
  double carbs_grams = 7;
  double fat_grams = 8;
  double fiber_grams = 9;
  repeated string allergens = 10;  // đã chuẩn hoá lowercase
}

message MealRecommendationRequest {
  string user_id = 1;
  MealType meal_type = 2;
  UserGoal goal = 3;
  UserProfile profile = 4;
  int32 consumed_calories = 5;     // calo đã ăn hôm nay
  int32 target_calories = 6;       // mục tiêu ngày
  double target_protein_remaining = 7;
  double target_carbs_remaining = 8;
  double target_fat_remaining = 9;
  repeated CandidateFood candidate_foods = 10;  // danh mục an toàn do caller gửi
  string locale = 11;                          // "vi-VN"
}

message RecommendedMeal {
  string food_id = 1;                // rỗng nếu là gợi ý ngoài danh mục
  string name = 2;
  string description = 3;
  int32 estimated_calories = 4;
  double protein_grams = 5;
  double carbs_grams = 6;
  double fat_grams = 7;
  repeated string key_micronutrients = 8;
  string rationale = 9;              // vì sao chọn món này
  repeated Citation citations = 10;
}

message MealRecommendationResponse {
  repeated RecommendedMeal meals = 1;
  string longevity_tip = 2;
  repeated Citation tip_citations = 3;
  Disclaimer disclaimer = 4;
  bool calorie_cap_reached = 5;      // BR-03: đã chạm trần 105%
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
  string nutrient = 1;          // "Vitamin D", "Omega-3", "Fiber"...
  string unit = 2;              // mg | g | IU
  double target = 3;
  double average_intake = 4;
  double deficit = 5;
  string severity = 6;          // mild | moderate | severe
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
  double nutrition_adherence_rate = 6;   // % đạt target macro trong 7 ngày
}

message LongevityHabitResponse {
  int32 longevity_score = 1;              // 0-100, deterministic
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
  int32 chapter_filter = 4;               // 0 = tất cả
}

message AskLongevityQuestionResponse {
  string answer = 1;
  repeated Citation citations = 2;
  bool abstained = 3;                     // không đủ căn cứ trong sách
  string abstain_reason = 4;
  Disclaimer disclaimer = 5;
}

// ---- 5. Tra cứu tri thức ----

message SearchKnowledgeRequest {
  string query = 1;
  int32 chapter_filter = 2;
  int32 top_k = 3;                        // mặc định 8, tối đa 20
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

#### D. Quy tắc kỹ thuật & Xử lý ngoại lệ

**D.1. Ranh giới dữ liệu**
- **Không lưu dữ liệu người dùng**: service không giữ hồ sơ, nhật ký, token hay kết quả cá nhân hoá của bất kỳ ai. Mọi dữ liệu người dùng phải được truyền qua message request và trả về qua message response.
- **Knowledge store**: service sở hữu `recommendation_db` (schema `kb`, extension `vector` của pgvector) chứa **corpus tri thức đã nhúng** — chunks + embeddings, kèm `book_page_from/to`, `pdf_page_from/to`, cờ `verified` / `needs_review`. Đây là **ngoại lệ duy nhất** so với nguyên tắc "mỗi service một DB" của Constitution, và được mô tả trong [ADR 002](../decisions/002-recommendation-knowledge-base.md).
- **Ingest corpus là pipeline offline** (`scripts/book-kb`), không thuộc runtime service.

**D.2. RAG & Grounding**
- **Truy xuất hybrid**: vector similarity (pgvector, cosine) + BM25 (tiếng Việt, bỏ dấu) → merge bằng RRF → `top_k = 8`, candidate vector = 50.
- **Grounding bắt buộc**: prompt phải ràng buộc *"chỉ dùng thông tin trong CONTEXT; không đủ thì abstain"*. Corpus được đưa vào prompt như **dữ liệu**, không phải chỉ dẫn (chống prompt injection).
- **Citation bắt buộc**: mỗi khẳng định gắn `citations[]`; `quote` phải khớp chuỗi với chunk nguồn, nếu không khớp thì bỏ citation và hạ response xuống `abstained`.
- **Abstain**: khi corpus không đủ căn cứ, **không** suy diễn — trả `abstained = true` kèm lý do.
- **Chất lượng corpus**: chunk từ trang chưa đối chiếu bản in mang `verified = false`; nội dung đó **không được** dùng để khẳng định số liệu hay khuyến nghị y tế.

**D.3. Phần tính toán không giao cho LLM**
- Lọc dị ứng (100%), lọc sở thích ăn uống, rule chạm trần 105% calo: thực hiện bằng **code**.
- Gap vi chất: tính bằng bảng RDA; `longevity_score`: tính bằng rubric cố định (fasting 35 / sleep 30 / hydration 20 / nutrition 15) → cùng đầu vào cho cùng kết quả.
- LLM chỉ đóng vai trò diễn giải và chọn lựa trong dữ liệu đã được ràng buộc.

**D.4. Timeout & Resilience**
- Mỗi lời gọi gRPC áp dụng deadline tối đa **5000ms**. Sử dụng `Polly` để retry **2 lần** với Exponential Backoff khi gặp lỗi mạng với Google AI.
- Provider lỗi, hết deadline hoặc parse lỗi → trả kết quả rỗng kèm `safety_note`/`abstain_reason`; **không** trả nội dung bịa, **không** làm sập caller.

**D.5. Structured Prompting**
- Bắt buộc ép kiểu phản hồi từ Google AI Gemini sang JSON có cấu trúc (`response_mime_type = application/json` + schema) để parse thành công sang Protobuf message response. Parse lỗi → retry 1 lần với prompt rút gọn, sau đó trả `abstained`.

**D.6. Disclaimer**
- Mọi response có trường `disclaimer` theo BR-04: mang tính chất tham khảo khoa học, không thay thế tư vấn bác sĩ chuyên khoa.

---

### 2.5 Background Worker

#### A. Mục tiêu & Trách nhiệm
Xử lý các tác vụ nền bất đồng bộ, lắng nghe các sự kiện từ **RabbitMQ**, và tích hợp với **Brevo API** để gửi email thông báo tự động.

#### B. Danh sách Consumers & Kịch bản xử lý

##### 1. `UserRegisteredConsumer`
- **Sự kiện tiêu thụ**: `UserRegisteredEvent` (chứa `UserId`, `Email`, `FullName`, `VerificationToken`).
- **Nghiệp vụ**:
  - Gọi adapter `BrevoEmailAdapter` gửi email xác thực tài khoản kèm đường dẫn kích hoạt (Verification URL).
  - Xử lý Idempotency: Kiểm tra trạng thái gửi thông qua `MessageId` của MassTransit để không gửi trùng lặp email.

##### 2. `PasswordResetRequestedConsumer`
- **Sự kiện tiêu thụ**: `PasswordResetRequestedEvent` (chứa `Email`, `ResetToken`, `ExpiresAt`).
- **Nghiệp vụ**: Gửi email chứa mã bảo mật hoặc liên kết đặt lại mật khẩu với thời hạn hiệu lực 15 phút.

##### 3. `WeeklyHealthDigestConsumer` (Định kỳ hàng tuần)
- **Kịch bản**: Chạy cronjob hoặc nhận message định kỳ vào sáng thứ Hai hàng tuần để gửi email tổng kết tiến độ dinh dưỡng, số giờ nhịn ăn và chuỗi Streak đạt được trong tuần trước đó.

---

## 3. Yêu cầu Phi chức năng (Non-Functional Requirements - NFR)

### 3.1 Hiệu năng (Performance)
- Thời gian phản hồi API đọc (Read API p95): `< 150ms`.
- Thời gian phản hồi API ghi (Write API p95): `< 250ms`.
- Thời gian phản hồi gRPC nội bộ từ `Recommendation Service`: `< 3500ms` (bao gồm thời gian sinh văn bản của Google AI).
- **Truy xuất tri thức (RAG)**: bước retrieval (vector + BM25 + RRF) p95 `< 150ms`; bước sinh câu trả lời `< 2500ms`; tổng vẫn nằm trong budget `< 3500ms`.
- **Chi phí & cache**: kết quả sinh được cache theo hash (context + prompt + hồ sơ đã chuẩn hoá) để tránh gọi lại Google AI cho câu hỏi lặp lại; embedding corpus chỉ tạo một lần theo phiên bản corpus.
- Xử lý đồng thời (Concurrency): Chịu tải tối thiểu 100 RPS trên mỗi service container đơn lẻ.

### 3.2 Bảo mật & Quyền riêng tư (Security & Privacy)
- **Chuẩn xác thực**: Tuân thủ tuyệt đối [ADR 001](../decisions/001-authn-authz-architecture.md):
  - Ký JWT bằng thuật toán bất đối xứng **ES256**.
  - Chỉ `Identity Service` giữ Private Key để ký; các service khác chỉ giữ Public Key để xác thực.
  - Phân phối Public Key qua biến môi trường dạng Base64 của SPKI PEM một dòng.
- **Tách biệt mạng nội bộ (Network Segmentation)**:
  - Cấu hình 3 docker networks: `edge`, `internal_net`, `data_net`.
  - Cổng của `recommendation` và `postgres` tuyệt đối không expose ra ngoài Internet.
- **Bảo vệ dữ liệu**: Không log thông tin mật khẩu, token, hoặc private key ở bất kỳ log output nào (theo `code-hygiene.md`).

### 3.3 Tính toàn vẹn & Tin cậy (Reliability & Consistency)
- **Transactional Outbox Pattern**: Mọi domain event cần publish lên RabbitMQ phải được lưu cùng transaction DB với entity chính, bảo đảm không bao giờ mất sự kiện ngay cả khi RabbitMQ bị tạm ngắt.
- **Idempotent Consumers**: Mọi consumer trong Worker phải kiểm tra trạng thái trước khi thực thi để tránh tạo tác dụng phụ lần hai khi có retry.
- **Zero Cross-DB Access**: Tuyệt đối không có lời gọi SQL cross-database giữa các service. Ngoại lệ duy nhất là `recommendation_db` — knowledge store của Recommendation, chỉ chứa corpus tri thức đã nhúng, không chứa dữ liệu người dùng (xem [ADR 002](../decisions/002-recommendation-knowledge-base.md)).

---

## 4. Ma trận Truy xuất Yêu cầu (Traceability Matrix)

| User Story / Tính năng | Service phụ trách | Endpoints / Contracts | Database / Entities | Event Xuất bản |
| :--- | :--- | :--- | :--- | :--- |
| **Đăng ký tài khoản** | Identity | `POST /api/identity/auth/register` | `identity_db`: `Users` | `UserRegisteredEvent` |
| **Đăng nhập & Cấp JWT** | Identity | `POST /api/identity/auth/login` | `identity_db`: `RefreshTokens` | — |
| **Hồ sơ thể chất cá nhân** | Identity | `PUT /api/identity/users/me/profile` | `identity_db`: `UserProfiles` | `UserProfileUpdatedEvent` |
| **Tra cứu thực phẩm** | Diet | `GET /api/diet/foods` | `diet_db`: `FoodItems` | — |
| **Thiết lập mục tiêu calo** | Diet | `PUT /api/diet/targets/me` | `diet_db`: `DailyDietTargets` | — |
| **Log nhật ký ăn uống** | Diet | `POST /api/diet/meals` | `diet_db`: `MealLogs`, `MealLogItems` | — |
| **Gợi ý món ăn AI** (US-REC-01) | Diet ⇄ Recom. | `GET /api/diet/meals/ai-recommendations` ➔ `GetMealRecommendations` | `recommendation_db`: `kb.chunks` (read) | — |
| **Phân tích thiếu hụt vi chất** (US-REC-02) | Diet ⇄ Recom. | `AnalyzeNutrientGaps` | `recommendation_db`: `kb.chunks` (read) | — |
| **Đánh giá thói quen trường thọ** (US-REC-03) | Diet/Progress ⇄ Recom. | `EvaluateLongevityHabits` | `recommendation_db`: `kb.chunks` (read) | — |
| **Hỏi tri thức sách** (US-REC-04) | Diet ⇄ Recom. | `AskLongevityQuestion` | `recommendation_db`: `kb.chunks` (read) | — |
| **Tra cứu tri thức sách** (US-REC-05) | Diet ⇄ Recom. | `SearchLongevityKnowledge` | `recommendation_db`: `kb.chunks` (read) | — |
| **Nạp corpus tri thức** (US-REC-06) | Recom. (offline) | `scripts/book-kb` | `recommendation_db`: `kb.chunks` (write) | — |
| **Ghi nhận cân nặng/BMI** | Progress | `POST /api/progress/metrics` | `progress_db`: `BodyMetricLogs`| — |
| **Đếm giờ nhịn ăn 16:8** | Progress | `POST /api/progress/fasting/start` | `progress_db`: `FastingSessions` | — |
| **Gửi email chào mừng** | Worker | MassTransit Consumer | — | — |
