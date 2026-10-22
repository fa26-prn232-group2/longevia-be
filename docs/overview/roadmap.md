# Lộ trình Phát triển (Roadmap)

Kế hoạch phân bổ và triển khai được tổ chức thành 6 giai đoạn (tương ứng với sự phân bổ cho team 5 thành viên):

| Giai đoạn | Nội dung chính | Trạng thái |
| :--- | :--- | :--- |
| **Giai đoạn 0** | Hoàn thiện repo, hạ tầng Docker, Central Package Management, bộ tài liệu BRD/PRD | Đã hoàn thành |
| **Giai đoạn 1** | **Shared Foundation & Identity Service**: Thư viện `Longevia.Shared` (AppException, JWT ES256 handler), AuthN/AuthZ, User Profile | Đang chuẩn bị |
| **Giai đoạn 2** | **Background Worker & Messaging**: Tích hợp MassTransit RabbitMQ, Brevo Email Adapter cho email xác thực | Kế tiếp |
| **Giai đoạn 3** | **Diet Service**: Thư viện món ăn, tính toán TDEE/Macro, Nhật ký bữa ăn 4 buổi, gRPC Client | Kế tiếp |
| **Giai đoạn 4** | **Progress Service**: Nhật ký chỉ số cơ thể, Fasting Timer (16:8), log nước & giấc ngủ, Dashboard Streaks | Kế tiếp |
| **Giai đoạn 5** | **Recommendation Service & AI Integration**: Khởi tạo gRPC Server, Google AI Gemini Adapter, RAG trên tri thức sách (`recommendation_db` + pipeline ingest `scripts/book-kb`), 5 hàm gợi ý / phân tích / tra cứu | Kế tiếp |
| **Giai đoạn 6** | **Tích hợp toàn diện & Gate Close-out**: Cấu hình Nginx gateway, Swagger UI gộp, Docker Compose multi-service, E2E Smoke Tests | Kế tiếp |

## Quy tắc thực hiện

- Mỗi tính năng lớn tuân theo quy trình Spec-kit (xem [../development/agent-workflow.md](../development/agent-workflow.md)).
- Kiểm soát chất lượng thông qua các gate trong [`Makefile`](../../Makefile) (`make lint` cho fast gate và `make check` trước khi kết thúc phase).
- Chi tiết yêu cầu kỹ thuật: xem [Product Requirement Document (PRD)](../requirements/prd.md).
