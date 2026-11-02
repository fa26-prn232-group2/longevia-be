# Trạng thái chờ bên ngoài — cập nhật 2026-11-02

Diet đã có client profile/weight, snapshot BMR/TDEE và hai public facade. Không
còn coi GetUserProfile/GetLatestWeight/meal consumer là endpoint còn nợ của nhóm.
Bảo đã cung cấp Q10 và consumer/inbox/projection trong main; không yêu cầu code lại.

| Việc phối hợp | Nhờ ai | Phần còn thực hiện |
| --- | --- | --- |
| Review bổ sung profile | Nguyên | Review proto + Identity mapping hai field dietary/allergies do Huy bổ sung; deploy Identity trước AI |
| AI thực tế | Đức | Review mapping và đo latency Google AI/corpus thật dưới budget Diet; phản hồi mismatch |
| Rollout và đối soát Streak | Bảo + Duy | Khi chạy stack, xác nhận queue trước replay, drain và đối chiếu snapshot; không phải thiếu provider |
| Báo cáo tuần | Duy/Bảo/lead | Chưa có schema/scheduler/recipient/consent; scope riêng ngoài baseline Diet |

Food seed allergens null bị loại khỏi candidate. Người dùng cần khai metadata cho
món tùy chỉnh; hệ thống không tự xác nhận an toàn hoặc suy thành phần từ tên.
Luồng tính target vẫn nhận bốn mục tiêu user chọn; tự chọn calo/macros để giảm/tăng
cân chưa được sản phẩm quy định, không phải endpoint Nguyên/Bảo còn thiếu.

Test riêng và evidence không thay rollout Compose/live AI. Xem từng file dev1,
dev3, dev4, dev5 trong thư mục này; không cần nhắn yêu cầu provider đã có.
