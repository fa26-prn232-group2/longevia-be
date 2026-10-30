# Gửi Đức — Recommendation Service (dev 4)

Huy (dev 2) đã có catalog, manual target, meal log và summary. Chưa mở REST meal AI.

## Nền đã có

recommendation.proto hiện có GetMealRecommendations và GenerateMealPlan.
Diet → Recommendation đã được kiến trúc cho phép. Diet chưa có profile provider
và cân nặng hợp lệ; không tạo profile/allergy/weight giả để gọi AI.

## Cần Đức xác nhận để Huy viết adapter và REST facade

- Đối chiếu field bắt buộc trong canonical proto với nguồn: Identity profile,
  Progress weight, Diet catalog/target. Xác nhận cách thiếu profile/allergy/weight
  trả lỗi, không ngầm hiểu allergies rỗng là đã xác nhận không dị ứng.
- Chốt định danh món kết quả, serving/quantity và macro; FoodItem hiện đã có allergens nullable do Huy bổ sung;
  null là unknown, [] chỉ là khai báo của chủ món, không phải chứng nhận an toàn. Không ghép gợi ý với món catalog bằng tên mơ hồ, không dùng
  prompt như bằng chứng kiểm tra dị ứng hoặc độ an toàn.
- Chốt goal/mealType, citation/disclaimer và error mapping HTTP. Response REST
  công khai cập nhật API spec + frontend guide trước khi triển khai.
- Deadline ngân sách end-to-end <3 giây theo yêu cầu, cancellation truyền xuyên;
  server deadline lớn hơn không chứng minh đạt latency. Test lỗi transport,
  thiếu citation và provider unavailable.
- Weekly plan cần Huy chốt REST/persistence của Diet, không coi GenerateMealPlan
  tồn tại là đã hoàn thành flow lưu kế hoạch.

Đức sở hữu Recommendation/provider/RAG; Huy sở hữu Diet facade và persistence.
Đây là yêu cầu contract/test phối hợp; chưa yêu cầu Đức đổi proto đang có tùy ý.

## Phần Huy đã chủ động cung cấp

Catalog POST/GET trả allergens optional/nullable; tags trim/lowercase/dedup/sort,
32 tag tối đa, 1..50 ký tự. Row cũ và seed là null (unknown). Field phù hợp hình
thức CandidateFood.allergens trong proto, nhưng caller chưa được map unknown
thành [] an toàn, không tự nhận diện alias/ingredents hay xác nhận bởi prompt.
Phần còn chờ thật: provider profile/allergies và weight, chốt provenance/safety
của metadata để chọn candidate và kiểm chứng adapter AI dưới deadline.
