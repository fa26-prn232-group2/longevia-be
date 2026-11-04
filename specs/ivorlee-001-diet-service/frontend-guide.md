# Close-out hiện hành — 2026-11-04

**Q01 — APPROVED FOR MVP CROSS-SERVICE UTC POLICY** (Q01 — APPROVED, CROSS-SERVICE MVP UTC).
Bảo quyết định theo quyền Q01 do Huy ủy quyền, được Bảo xác nhận trực tiếp;
không tuyên bố Huy tự ký ADR hoặc review PR. [ADR005](../../docs/decisions/005-mvp-utc-business-calendar.md)
là quyết định hiện hành, thay các pending Q01 approval trong phần lịch sử bên dưới.
Server-derived dates/today/week/history dùng UTC; Diet DateOnly/BusinessDate do
client chọn giữ nguyên qua event/replay. UI tự chọn hôm nay dùng UTC. Việt Nam
05/11/2026 00:30(+07) vẫn là04/11 UTC;07:00(+07) mới sang05/11 UTC.
Không rebucket lịch sử/BMI snapshots. Huy vẫn phụ trách frontend/Diet alignment;
approval không đồng nghĩa frontend đã deploy, replay production đầy đủ hay
Dashboard completeness được nghiệm thu. Q02 auth và rollout blockers giữ riêng.

---

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
