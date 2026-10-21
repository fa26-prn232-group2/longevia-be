# Tầm nhìn & Mục tiêu (Vision & Objectives)

Chi tiết nghiệp vụ đầy đủ xem tại: [Business Requirement Document (BRD)](../requirements/brd.md).

## Sản phẩm là gì

**Longevia** là nền tảng số hỗ trợ chăm sóc sức khỏe chủ động và tối ưu hóa tuổi thọ khỏe mạnh (**Healthspan**). Hệ thống kết hợp giữa khoa học dinh dưỡng chính xác, theo dõi chỉ số sinh học cơ thể và trí tuệ nhân tạo (Google AI) nhằm cung cấp giải pháp cá nhân hóa toàn diện cho người dùng trong việc xây dựng và duy trì lối sống lành mạnh, phòng ngừa sớm các bệnh mạn tính không lây.

## Mục tiêu cốt lõi

- **Cá nhân hóa dinh dưỡng**: Đưa ra chế độ calo và vi chất tối ưu cho từng cá nhân dựa trên thể trạng, dị ứng và mục tiêu sinh học.
- **Theo dõi tiến độ toàn diện**: Kết nối dữ liệu bữa ăn với sự thay đổi của cân nặng, huyết áp, nhịp tim và thói quen sinh hoạt (nhịn ăn gián đoạn, nước, giấc ngủ).
- **Ứng dụng AI thông minh**: Phân tích thiếu hụt vi chất tuần và gợi ý món ăn tức thời loại trừ 100% thành phần gây dị ứng.
- **Xây dựng thói quen bền vững**: Duy trì động lực người dùng thông qua hệ thống chuỗi ngày liên tiếp (Streaks) và báo cáo tiến trình định kỳ.

## Phạm vi (Scope)

- **Trong phạm vi (In-Scope)**:
  - Xác thực an toàn phi tập trung với JWT ES256, hồ sơ thể chất cơ bản.
  - Thư viện món ăn, tính toán TDEE/Macro, ghi nhật ký ăn uống 4 bữa trong ngày.
  - Nhật ký đo chỉ số sinh học (cân nặng, BMI, huyết áp, nhịp tim), đồng hồ nhịn ăn gián đoạn (Fasting Timer), log nước và giấc ngủ.
  - Gợi ý món ăn và phân tích vi chất qua AI (gRPC nội bộ, Google AI Gemini).
  - Tự động hóa gửi email chào mừng, kích hoạt, đặt lại mật khẩu và báo cáo tuần qua RabbitMQ & Brevo.
- **Ngoài phạm vi (Non-goals)**:
  - Tích hợp trực tiếp cảm biến phần cứng thiết bị đeo (Apple Watch/Garmin).
  - Cổng thanh toán tài chính / Mua bán gói dịch vụ.
  - Mạng xã hội cộng đồng và tư vấn y tế 1-1 với bác sĩ.

## Đối tượng người dùng mục tiêu

1. **Người tối ưu tuổi thọ (Longevity Biohackers)**: Quan tâm đến cơ chế tự thực bào (Autophagy), nhịn ăn gián đoạn, chống viêm tế bào và theo dõi biomarkers.
2. **Người cần quản lý vóc dáng & cân nặng**: Cần công cụ theo dõi calo/macro nhanh gọn, có gợi ý thực đơn lành mạnh dễ làm.
3. **Người trung niên phòng ngừa bệnh chuyển hóa**: Cần kiểm soát huyết áp, nhịp tim, đường huyết và duy trì giấc ngủ chất lượng.
