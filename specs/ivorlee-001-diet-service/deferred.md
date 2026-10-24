# Phần tích hợp chưa triển khai

- **BMR/TDEE**: thiếu contract lấy profile Identity và cân nặng Progress. Phối hợp
  dev 1/dev 3, duyệt thiết kế/ADR/proto trước khi thêm chiều gọi; không đọc chéo DB.
- **AI**: thiếu nguồn profile/allergies, metadata allergen FoodItem, mealType/goal
  và REST citation/disclaimer. Giữ proto Recommendation, phối hợp dev 4. Cần đo
  ngân sách dưới 3 giây thật, không coi server deadline 30s là đạt yêu cầu.
- **Streak/báo cáo tuần**: cần event meal-log và consumer Progress/Worker,
  version/topology/outbox/idempotency, E2E broker thật. Không tạo event tùy ý.
- **Kế hoạch tuần**: đã có GenerateMealPlan phía Recommendation, nhưng cần chốt
  REST/persistence của Diet thành feature tiếp theo.
