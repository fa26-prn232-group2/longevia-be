# Bàn giao frontend Diet

Trạng thái: đã triển khai 8 operation; evidence kiểm chứng xem evidence.md.
Gateway prefix `/api/diet`, Bearer JWT; ngày mặc định UTC.

| Method | Route | Dùng khi |
| --- | --- | --- |
| GET/POST | /foods | Tra cứu/tạo món; allergens null là unknown, [] là tự khai không có tag |
| GET/PUT | /targets/me | Đọc/lưu mục tiêu nhập tay; PUT clear snapshot BMR/TDEE |
| POST | /targets/me/calculate | Tính BMR/TDEE từ profile/cân nặng thật và lưu cùng target |
| POST | /meals | Ghi snapshot dinh dưỡng, không tự retry khi chưa rõ kết quả |
| GET | /meals/daily-summary | Summary theo date YYYY-MM-DD, mặc định hôm nay UTC |
| GET | /meals/ai-recommendations | Query mealType và goal bắt buộc, xem bên dưới |

Calculate body giống PUT: targetCalories, targetProteinGrams, targetCarbsGrams,
targetFatGrams; 4P+4C+9F lệch tối đa ±5%. Không gửi tuổi/giới tính/cân nặng từ
frontend. Response vẫn TargetResponse; bmr/tdee null cho manual, số snapshot cho
calculated. Không tự hiển thị null thành 0. Không tự đặt target calo bằng TDEE.

AI query: mealType Breakfast/Lunch/Dinner/Snack; goal LoseWeight/Maintain/
BuildMuscle/Longevity. Response remainingCalories, recommendedMeals[],
longevityTip, tipCitations[], disclaimer, calorieCapReached, safetyNote. Meal có
foodId/name/servingSize/servingUnit/calories/proteinGrams/carbsGrams/fatGrams/
description/rationale/citations[]. Không tự ghi log kết quả. Citation có
chapterNumber/chapterTitle/bookPage/pdfPage/quote/verified/sourceFile; hiển thị
trạng thái chưa kiểm chứng khi verified=false. Luôn hiển thị disclaimer/safetyNote,
cho phép danh sách rỗng khi abstain hoặc vượt trần calo; không tự dựng món thay thế.

400 validation.failed hoặc diet.invalid_recommendation_query; 401 JWT; 404 thiếu
món/target; 422 thiếu profile/cân nặng/candidate hoặc profile chưa hỗ trợ;
502 response dependency sai; 503 dependency unavailable/profile metadata chưa
đủ; 504 timeout. Mã cụ thể ở ../../docs/api/api-spec.md §3.

ServingSize và quantity cùng servingUnit (100 g + quantity150 là150g). Summary
có remaining âm khi vượt target; log giữ dinh dưỡng lịch sử, không tính lại catalog.
Food tự khai allergen không phải kiểm định y tế.

## Counted sync

8 operation / 6 path / 16 DTO / 100 field khai báo (không nhân type lồng nhau).
So với baseline 6/13/75, thêm MealRecommendationsResponse (7),
MealSuggestionResponse (11), MealCitationResponse (7); calculate tái dùng
UpdateTargetRequest và TargetResponse. Swagger test đối chiếu toàn bộ inventory.
Không đánh dấu READY_FOR_FRONTEND toàn feature khi rollout/live AI chưa kiểm chứng.
