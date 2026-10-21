# Phạm vi MVP (MVP Scope)

Chi tiết yêu cầu kỹ thuật và user stories xem tại: [Product Requirement Document (PRD)](../requirements/prd.md).

## Tính năng trong MVP

| Service | Tính năng chính | Giao thức / Ranh giới |
| :--- | :--- | :--- |
| **Identity** | Đăng ký tài khoản, Đăng nhập, Ký JWT ES256, Refresh Token, Đổi mật khẩu, Hồ sơ thể trạng cơ bản & Dị ứng | HTTP REST qua Nginx Gateway, sở hữu `identity_db` |
| **Diet** | Thư viện món ăn, Đặt mục tiêu Calo/Macro (TDEE/BMR), Ghi nhật ký ăn uống 4 bữa, Thực đơn mẫu tuần | HTTP REST qua Nginx Gateway, sở hữu `diet_db` |
| **Progress** | Nhật ký chỉ số cơ thể (Cân nặng, BMI, Huyết áp, Nhịp tim), Fasting Timer (16:8/18:6), Log nước & giấc ngủ, Dashboard tiến độ & Streaks | HTTP REST qua Nginx Gateway, sở hữu `progress_db` |
| **Recommendation** | AI Gợi ý thực đơn cá nhân hóa, AI Phân tích thiếu hụt vi chất 7 ngày, AI Đánh giá thói quen trường thọ | **gRPC nội bộ**, Stateless, không DB, không route qua Nginx |
| **Background Worker** | Gửi email kích hoạt tài khoản, email chào mừng, email đặt lại mật khẩu, gửi email báo cáo tuần | Consumer bất đồng bộ từ RabbitMQ, gọi Brevo API |

## Ngoài phạm vi (Out of Scope)

- Tích hợp trực tiếp các thiết bị đeo thông minh (Apple Watch, Garmin, Whoop, Oura).
- Cổng thanh toán tài chính (VNPay, MoMo, Stripe).
- Chức năng mạng xã hội, bảng tin chia sẻ bài viết cộng đồng.
- Tư vấn trực tiếp 1-1 với bác sĩ / chuyên gia y tế.
- Nhận diện món ăn tự động qua ảnh chụp camera (Computer Vision).

## Tiêu chí hoàn thành MVP (Definition of Done)

1. **Kiến trúc & Ranh giới**:
   - Tuân thủ 100% [Constitution](../../.specify/memory/constitution.md): Không truy cập chéo DB, không import chéo mã nguồn giữa các service.
   - Xác thực bất đối xứng ES256 hoạt động chuẩn hóa theo [ADR 001](../decisions/001-authn-authz-architecture.md).
2. **Kiểm thử & Tích hợp**:
   - Tất cả các endpoint REST và hàm gRPC đều có Unit Tests và Integration Tests.
   - Có ít nhất một luồng kiểm thử tích hợp đầu cuối (E2E) qua hạ tầng Docker thật (Nginx, PostgreSQL, RabbitMQ).
3. **Chất lượng mã nguồn**:
   - Vượt qua cổng kiểm soát chất lượng đóng: `make check` (format, build Release không warning, chạy full test, quét mã độc gói Nuget) đều màu xanh.
