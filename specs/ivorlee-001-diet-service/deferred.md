# Phần tích hợp chưa triển khai

- **BMR/TDEE**: thiếu contract lấy profile Identity và cân nặng Progress. Phối hợp
  dev 1/dev 3, duyệt thiết kế/ADR/proto trước khi thêm chiều gọi; không đọc chéo DB.
- **AI**: thiếu nguồn profile/allergies và policy provenance/mealType/goal
  và REST citation/disclaimer. Giữ proto Recommendation, phối hợp dev 4. Cần đo
  ngân sách dưới 3 giây thật, không coi server deadline 30s là đạt yêu cầu.
- **Streak phía Progress**: Diet đã bổ sung contract MealLoggedEvent v1, outbox và
  replay; Bảo cần consumer/inbox/projection, timezone policy và reconcile lịch sử.
  Chưa coi test receiver của Diet là E2E Streak thực.
- **Báo cáo tuần**: còn thiếu schema/scheduler/recipient/consent, consumer Worker,
  idempotency và E2E; xem handoffs/dev5-duy.md.
- **Kế hoạch tuần ngoài baseline REST Diet hiện tại**: GenerateMealPlan có phía
  Recommendation, nhưng PRD/API Diet hiện chưa định nghĩa route/persistence tuần.
  Không ghi thành việc Đức đang block Huy; cần phạm vi sản phẩm/contract mới trước
  khi mở chức năng này. Không phải việc còn nợ trong bảy route Diet hiện hành.

Metadata allergens và CLI snapshot đối soát là việc Diet tự làm, đã được triển
khai thay vì để trong blocker. Chi tiết quyền sở hữu: handoffs/remaining-blockers.md.
