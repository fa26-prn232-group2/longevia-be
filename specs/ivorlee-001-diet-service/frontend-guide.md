# Hướng dẫn frontend — Diet độc lập

Trạng thái: IN_PROGRESS; chỉ sáu endpoint độc lập, không khai báo toàn bộ Diet READY.
Base URL: http://localhost:8080/api/diet. Header Authorization: Bearer JWT ES256.
Canonical: [API §3](../../docs/api/api-spec.md).

| Method | Route nội bộ | Request | Response thành công |
| --- | --- | --- | --- |
| GET | /foods | search/category/pageNumber/pageSize | FoodPageResponse, 200 |
| POST | /foods | CreateFoodRequest | FoodResponse, 201 |
| GET | /targets/me | Không | TargetResponse, 200 |
| PUT | /targets/me | UpdateTargetRequest | TargetResponse, 200 |
| POST | /meals | LogMealRequest | MealResponse, 201 |
| GET | /meals/daily-summary | date=YYYY-MM-DD, tùy chọn | DailySummaryResponse, 200 |

## Quy tắc tích hợp

- Catalog trả món chung và custom của user; không gửi UserId.
- Pagination dùng pageNumber/pageSize/totalCount/totalPages/hasPreviousPage/hasNextPage;
  mặc định 1/20, pageSize giới hạn 100. Trang ngoài dữ liệu vẫn giữ totalCount.
- Dinh dưỡng food tính theo servingSize/servingUnit. Quantity dùng cùng đơn vị:
  món 100g thì quantity=150 nghĩa là 150g, không phải 150 khẩu phần.
- Input dinh dưỡng 0..1000000, servingSize/quantity >0 và <=1000000; tối đa 3
  chữ số thập phân. Tên tối đa 200 ký tự, category 50, servingUnit 30.
- TargetCalories >0; 4*protein + 4*carbs + 9*fat sai số <=5%. Bmr/tdee null
  do phần tính tự động chưa tích hợp; không hiển thị null thành 0 kcal.
- Date bắt buộc ở POST meal; GET summary mặc định ngày UTC. Mỗi POST tạo log mới;
  tránh tự retry POST khi không biết request cũ đã thành công hay chưa.
- Summary gộp bốn meal type; consumed lấy từ snapshot, target là mục tiêu hiện tại;
  remaining có thể âm. Meal response có id/date/mealType/totalCalories/items;
  item có foodId/name/servingUnit/quantity/calories và các macro/fiber.
- Response tạo có Location trỏ trang catalog/summary tương ứng. Không có GET meal ID.
- AI recommendations, BMR/TDEE và kế hoạch tuần chưa triển khai trong phase này.

## Lỗi

400 validation.failed (errors theo field), 401 chưa đăng nhập hoặc sub không hợp lệ,
404 diet.food_not_found (không tồn tại hoặc không được phép dùng),
404 diet.target_not_found (target/summary chưa có mục tiêu),
500 system.internal. Problem Details có traceId, không có SQL/stack.
Malformed JSON/query do ASP.NET Core trả 400. Quyền truy cập JWT áp dụng đủ 6 endpoint.

## Counted sync

6 operation nghiệp vụ / 4 path / 13 DTO / 75 field DTO khai báo (không nhân field
của type lồng nhau theo số lần sử dụng): foods, targets/me, meals, meals/daily-summary.
Request: 4 DTO (CreateFoodRequest, UpdateTargetRequest, LogMealRequest,
LogMealItemRequest). Response nghiệp vụ: 9 DTO (FoodResponse, FoodPageResponse,
TargetResponse, MealResponse, MealItemResponse, DailySummaryResponse,
MacroSummary, MacroProgress, MealSummary).
Test tự động đối chiếu 6 operation và 75 field của 13 schema trong Swagger đã qua.
Xem [evidence.md](evidence.md). Chưa READY cho phần tích hợp hoãn.

## Cập nhật cung cấp backend — 2026-10-25

Thêm MealLoggedEvent v1, replay/snapshot phía backend; metadata catalog thêm
allergens optional vào CreateFoodRequest và nullable vào FoodResponse. Count
hiện 6 operation / 13 DTO / 75 field (thêm 2 so với 73 ở giai đoạn độc lập).
HTTP tạo meal 201 nghĩa là meal/outbox đã commit, không có nghĩa Streak Progress
đã cập nhật đồng bộ; không tự retry POST khi kết quả không rõ.
Token hợp lệ từ login Identity đã kiểm chứng đọc/ghi Diet qua gateway; phần
profile/weight/AI và Streak consumer vẫn theo backend-integration-guide.md.

## Metadata allergens

POST /foods có optional allergens: null hoặc bỏ qua = chưa khai báo; [] = người
tạo khai báo không có tag, không phải xác nhận an toàn. GET/POST response FoodResponse
có allergens nullable. Tối đa 32 tag, mỗi tag 1..50 ký tự không trắng, server trim,
lowercase/dedup/sort; null entry bị 400. Client cũ không gửi field vẫn hoạt động.
Không tự hiển thị unknown thành "không dị ứng" hoặc dùng thông tin do user khai
báo như chứng nhận y tế. Các món seed/row cũ giữ unknown.
