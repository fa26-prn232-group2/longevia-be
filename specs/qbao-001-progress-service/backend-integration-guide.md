# Hướng dẫn Tích hợp Backend (Backend Integration Guide)

Trạng thái: **IN_PROGRESS** (Phần core Progress đã hoàn tất và kiểm chứng; chờ canonical integration provider từ Identity và Diet).

Tài liệu này xác định giao diện tích hợp giữa **Progress Service** và các microservice liên quan trong hệ thống Longevia Backend.

---

## 1. Điểm tích hợp 1: Identity Service → Progress Service (gRPC Read Height)

- **Mục đích**: Progress Service cần lấy chiều cao (`HeightCm`) từ hồ sơ người dùng trong Identity Service để tính BMI và lưu snapshot bất biến (`HeightUsedCm`, `Bmi`, `BmiCategory`) khi người dùng ghi nhận chỉ số cơ thể (`POST /api/progress/metrics`).
- **Quyết định kiến trúc (Q02)**: **APPROVED (Bảo + Nguyên)**.
  - Hình thức: gRPC read nội bộ đồng bộ qua mạng nội bộ Docker (`http://identity:5000`).
  - Kiểu dữ liệu: `decimal`, khớp với định nghĩa cột PostgreSQL `numeric(5,2)` của Identity trong PR #10.
  - Ranh giới trách nhiệm: Identity là **Provider**, Progress là **Consumer**.
- **Hiện trạng & External Blocker**:
  - Identity đã merge PR #10 (cập nhật model và REST user profile).
  - Schema protobuf `GetBodyProfile` trong `contracts/identity/v1/identity.proto` và runtime implementation gRPC service trong Identity Service hiện **chưa có trên `main`**.
  - **Trạng thái**: **BLOCKED EXTERNAL PROVIDER**.
  - **Triển khai tại Progress**:
    - Progress đã tạo abstraction [`IBodyProfileProvider`](file:///src/Services/Progress/Progress.Application/Abstractions/IBodyProfileProvider.cs) trong Application layer.
    - Infrastructure layer đã có stub [`BodyProfileProvider`](file:///src/Services/Progress/Progress.Infrastructure/Adapters/Grpc/BodyProfileProvider.cs) sẵn sàng kết nối client gRPC khi RPC schema được phát hành.
    - Kiểm thử tích hợp `MetricsTests.cs` đã kiểm chứng đầy đủ:
      - Khi provider trả về chiều cao (vd: 175cm) -> tính BMI 22.86, lưu snapshot, trả 201 Created.
      - Khi provider trả về `null` (profile chưa có chiều cao) -> trả **`422 Unprocessable Entity`** với mã lỗi `progress.height_unavailable`.

---

## 2. Điểm tích hợp 2: Diet Service → Progress Service (RabbitMQ Meal Activity Event)

- **Mục đích**: Cập nhật ngày hoạt động (active day) của người dùng khi có bữa ăn được ghi nhận trong Diet Service để tính toán chuỗi ngày hoạt động liên tục (Streak) trên Dashboard.
- **Quyết định kiến trúc (Q06 / Q09)**: **APPROVED**.
  - Quy tắc Active Day: Là tập hợp hợp (union) của các ngày có nhật ký bữa ăn (`Diet.MealLog`) và các ngày có ghi nhận chỉ số cơ thể (`BodyMetricLog`). Nước, ngủ và fasting không tạo streak độc lập.
  - Hình thức tích hợp: Asynchronous Event qua RabbitMQ (`MealActivityEvent`).
  - Ngày tính streak: Sử dụng `MealLog.Date` (`DateOnly` do client cung cấp hoặc UTC date), không dùng thời điểm nhận event qua mạng.
  - Ranh giới trách nhiệm: Diet Service là **Producer**, Progress Service là **Consumer**.
- **Hiện trạng & External Blocker**:
  - Diet Service hiện tại chỉ có REST POST `/meals` ghi vào `diet_db`, chưa có outbox pattern hoặc publisher sự kiện RabbitMQ trên `main`.
  - **Trạng thái**: **BLOCKED EXTERNAL PROVIDER**.
  - **Triển khai tại Progress**:
    - Progress đã thiết kế entity [`ActivityContribution`](file:///src/Services/Progress/Progress.Domain/Entities/ActivityContribution.cs) và bảng `activity_contributions` trong `progress_db`.
    - Đã có unique constraint `(Source, SourceLogId)` để đảm bảo tính Idempotent tuyệt đối khi nhận event lặp lại từ message broker.
    - Repository [`IActivityContributionRepository`](file:///src/Services/Progress/Progress.Application/Abstractions/IActivityContributionRepository.cs) cung cấp phương thức truy vấn danh sách các ngày hoạt động từ bữa ăn.
    - Kiểm thử tích hợp `DashboardTests.cs` đã kiểm chứng đầy đủ:
      - Khi có ActivityContribution từ Diet (bữa ăn hôm qua) + BodyMetricLog (hôm nay) -> Dashboard tính chính xác `currentStreakDays = 2`.
      - Nước uống hoặc giấc ngủ không tạo streak độc lập.

---

## 3. Điểm tích hợp 3: Progress Service → Diet Service (Latest Weight for BMR/TDEE - Q10)

- **Mục đích**: Diet Service cần cân nặng mới nhất để tính chỉ số chuyển hoá cơ bản (BMR/TDEE) khi thiết lập mục tiêu dinh dưỡng.
- **Hiện trạng (Q10 / T038)**:
  - Đây là dependency ngược (Diet phụ thuộc Progress).
  - Hiện tại mục tiêu BMR/TDEE của Diet đang trả `null` (theo `deferred.md` của Diet).
  - Progress Service đã chuẩn bị sẵn phương thức `GetLatestAsync(userId)` trong `IBodyMetricRepository`.
  - Sau khi các bên phê duyệt ADR chính thức cho gRPC read cân nặng giữa Progress (Provider) và Diet (Consumer), Progress sẽ expose RPC method tương ứng trong một feature riêng.
