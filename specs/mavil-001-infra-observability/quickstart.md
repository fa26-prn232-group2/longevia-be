# Hướng Dẫn Khởi Chạy & Kiểm Thử Nhanh (Quickstart Guide) — Infra & Observability

**Feature**: `specs/mavil-001-infra-observability`  
**Nhánh**: `feat/infra-observability`  
**Ngày**: 2026-10-22

---

## 1. Yêu cầu Tiên quyết (Prerequisites)

- **Docker Desktop** (hoặc Docker Engine + Docker Compose v2) đang chạy.
- **.NET SDK 10** đã được cài đặt.
- **GNU Make** (Trên Windows: `choco install make` hoặc chạy trực tiếp lệnh bên dưới).
- PowerShell 7 (`pwsh`).

---

## 2. Các Bước Khởi Chạy Cục Bộ (Step-by-Step Execution)

### Bước 1: Chuẩn bị file môi trường
Tạo file cấu hình môi trường `.env` từ file mẫu:
```powershell
Copy-Item .env.example .env
```

### Bước 2: Khởi động toàn bộ Hạ tầng Cơ sở
Sử dụng Makefile để khởi chạy tất cả các dịch vụ phụ trợ:
```powershell
make infra-up
```
Lệnh này sẽ khởi động:
- **PostgreSQL 17 (pgvector)** tại `localhost:5432`
- **Redis 7** tại `localhost:6379`
- **RabbitMQ 4** tại `localhost:5672` (Giao diện quản lý: `http://localhost:15672`)
- **Mailpit** tại `localhost:1025` (Giao diện hòm thư: `http://localhost:8025`)
- **Prometheus** tại `http://localhost:9090`
- **Grafana Dashboard** tại `http://localhost:3001`
- **Nginx Gateway** tại `http://localhost:8080`
- **Swagger UI gộp** tại `http://localhost:8080/swagger`

---

## 3. Kịch bản Kiểm thử & Xác minh (Verification Scenarios)

### Kịch bản 1: Kiểm tra Cổng Nginx Gateway & Lỗi 404 Chuẩn
1. Truy cập kiểm tra sức khỏe Gateway:
   ```powershell
   curl http://localhost:8080/healthz
   ```
   *Kết quả mong đợi*: Phản hồi `ok` với HTTP Status 200.
2. Kiểm tra phản hồi lỗi 404 tiếng Việt:
   ```powershell
   curl http://localhost:8080/api/unknown-service/test
   ```
   *Kết quả mong đợi*:
   ```json
   {
     "type": "https://longevia.com/errors/not-found",
     "title": "Không tìm thấy",
     "status": 404,
     "detail": "Đường dẫn API không tồn tại hoặc phương thức không được hỗ trợ."
   }
   ```

### Kịch bản 2: Kiểm tra Hộp thư Mailpit
1. Mở trình duyệt truy cập: `http://localhost:8025`.
2. Giao diện Mailpit hiển thị sẵn sàng, trống hộp thư đến.
3. Khi Worker gửi email kích hoạt tài khoản hoặc đặt lại mật khẩu, email sẽ lập tức xuất hiện tại đây để kiểm tra nội dung HTML.

### Kịch bản 3: Kiểm tra Grafana Dashboard & Prometheus
1. Mở trình duyệt truy cập: `http://localhost:3001`.
2. Không cần đăng nhập (Anonymous `Viewer` — chỉ xem, không sửa được cấu hình).
3. Kiểm tra mục **Connections -> Data sources**: Thấy `Prometheus` (URL `http://prometheus:9090`) đã được kết nối tự động.
4. Kiểm tra mục **Dashboards**: Dashboard `ASP.NET Core - Overview` đã có sẵn, hiển thị các biểu đồ CPU, RAM, Throughput.

### Kịch bản 4: Kiểm tra Khởi động Full Ứng dụng với Docker Compose
```powershell
make docker-up
```
Toàn bộ các container bao gồm `identity` và `worker` sẽ được build và khởi chạy cùng mạng nội bộ.

---

## 4. Các Lệnh Dừng & Dọn dẹp (Teardown)

- Dừng các container nhưng giữ nguyên dữ liệu:
  ```powershell
  make docker-stop
  ```
- Dọn dẹp hoàn toàn containers và volumes:
  ```powershell
  make docker-down
  ```
