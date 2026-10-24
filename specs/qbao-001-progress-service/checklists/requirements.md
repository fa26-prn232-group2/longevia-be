# Checklist chất lượng đặc tả

Ngày: 2026-10-24. Feature: [spec.md](../spec.md).
Đánh giá sau specify/clarify; đây là trạng thái thực, không đánh dấu mọi mục xanh
chỉ để chạy plan. Developer yêu cầu giữ PENDING cho review planning.

- [x] Spec tập trung giá trị/nghiệp vụ, thiết kế kỹ thuật tách sang plan/contracts.
- [x] Đủ bốn user story, thứ tự ưu tiên và cách kiểm thử độc lập có điều kiện.
- [x] Đủ các phần template: scenarios, requirements, entities, success criteria, assumptions.
- [x] Có acceptance scenarios và edge cases cho từng story.
- [x] Ownership, validation và dependency được ghi cho cả bốn story.
- [x] Tiêu chí thành công định lượng và truy xuất được tới test/task.
- [x] Phạm vi Phase 1 chỉ tài liệu; canonical contract/source/migration không thay đổi.
- [x] Phân biệt yêu cầu có căn cứ và proposal PENDING, có owner/phase bị chặn.
- [ ] Không còn quyết định nghiệp vụ/tích hợp ảnh hưởng triển khai chưa duyệt: Q01–Q09 còn PENDING.
- [ ] Tất cả yêu cầu hoàn toàn không mơ hồ để implement ngay: cần team duyệt policy/schema trước.
- [x] Không khẳng định ví dụ API là policy BMI/water/delta đã duyệt.
- [x] Không thay điều kiện streak PRD bằng water/sleep/fasting hoặc metrics-only.
- [x] Không có implementation detail như code body/migration trong đặc tả nghiệp vụ.
- [x] Contract-first, service boundaries, BR-05 được bảo toàn.
- [ ] Sẵn sàng triển khai toàn bộ feature: chưa, đây là review Phase 1 planning.
- [x] Từng dependency chưa rõ đều có câu hỏi/phương án/điều kiện gỡ.

Kết quả specify → clarify: 13/16 → 13/16; không regression, không mục mới pass.
Hai quyết định developer đã ghi ở spec; chúng xác nhận cách giữ pending và hướng
đề xuất height, không giải quyết thay team các Q. Các mục chưa đạt được giữ nguyên.
