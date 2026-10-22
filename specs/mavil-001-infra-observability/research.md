# Nghiên Cứu Kỹ Thuật (Research & Technical Decisions) — Infra & Observability

**Feature**: `specs/mavil-001-infra-observability`  
**Nhánh**: `feat/infra-observability`  
**Ngày**: 2026-10-22

Tài liệu này ghi lại các quyết định kỹ thuật, cơ sở lập luận và các phương án đã được đánh giá cho gói hạ tầng, gateway, background worker và hệ thống giám sát.

---

## 1. Quyết định: Container Runtime Chiseled Distroless cho .NET 10

- **Quyết định**: Sử dụng base image multi-stage với SDK `mcr.microsoft.com/dotnet/sdk:10.0` để build và `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` cho runtime.
- **Lý do lựa chọn**:
  - Tối ưu bảo mật: Image chiseled không chứa shell (`/bin/sh`, `/bin/bash`), không có package manager (apt/apk), triệt tiêu bề mặt tấn công RCE.
  - Chạy với tài khoản không đặc quyền (`USER app`, UID 1654), tuân thủ nguyên tắc Least Privilege.
  - Kích thước siêu nhẹ: Kích thước image giảm từ ~450MB xuống dưới 180MB (gate SC-002; mục tiêu stretch <150MB), giúp `docker compose up` và pull/push image nhanh chóng.
- **Các phương án thay thế đã đánh giá**:
  - *Debian chuẩn (`aspnet:10.0`)*: Dung lượng lớn, chứa nhiều binary thừa không cần thiết cho production.
  - *Alpine Linux (`aspnet:10.0-alpine`)*: Nhẹ nhưng dùng `musl-libc` thay vì `glibc`, có thể gây lỗi tương thích với một số thư viện native C# / gRPC trong tương lai.

---

## 2. Quyết định: Chuẩn Giám sát OpenTelemetry .NET 10 & Prometheus Scrape

- **Quyết định**: Sử dụng bộ thư viện chính thức **OpenTelemetry .NET** (`OpenTelemetry.Exporter.Prometheus.AspNetCore`) đóng gói thành extension dùng chung trong `src/Shared/`.
- **Lý do lựa chọn**:
  - Là chuẩn mở của CNCF và được Microsoft khuyến nghị chính thức cho .NET 8/9/10.
  - Tự động thu thập toàn bộ runtime metrics của .NET (GC, Threadpool, Process Memory, CPU), ASP.NET Core HTTP metrics (Throughput, Latency p50/p95/p99, Status codes) và HTTP Client outbound metrics mà không cần viết code thủ công.
  - Mỗi service chỉ cần 2 dòng lệnh trong `Program.cs` (`AddObservability()` và `MapPrometheusScrapingEndpoint()`).
- **Các phương án thay thế đã đánh giá**:
  - *prometheus-net.AspNetCore*: Thư viện bên thứ 3 cũ hơn, không theo chuẩn OpenTelemetry Semantic Conventions của CNCF.
  - *OTel Collector Agent*: Quá nặng và phức tạp cho giai đoạn phát triển cục bộ (cần thêm 1 container trung gian).

---

## 3. Quyết định: Phân bổ Cổng Grafana :3001 và Tự động Provisioning

- **Quyết định**: 
  - Grafana chạy trên container port 3000, ánh xạ ra host port **`3001:3000`** (`http://localhost:3001`).
  - Kích hoạt **Anonymous Access** với vai trò `Viewer` cho môi trường dev local.
  - Tự động nạp Prometheus làm Default Datasource qua file `provisioning/datasources/prometheus.yml`.
  - Tự động nạp sẵn Dashboard giám sát ASP.NET Core qua file `provisioning/dashboards/`.
- **Lý do lựa chọn**:
  - Bảo lưu cổng `3000` cho ứng dụng Frontend (Next.js / Vite / React), tránh xung đột cổng khi dev chạy song song cả BE và FE.
  - Zero manual setup: Lập trình viên chỉ cần gõ `make infra-up`, mở `http://localhost:3001` là có ngay dashboard trực quan, không cần đăng nhập và không cần tự cấu hình datasource.
- **Các phương án thay thế đã đánh giá**:
  - *Định tuyến qua Nginx `/grafana`*: Phức tạp hóa cấu hình reverse proxy subpath (`root_url`) của Grafana và làm tăng tải cho Nginx Gateway.

---

## 4. Quyết định: Kiến Trúc Email Đa Môi Trường (Mailpit Local + Brevo SMTP Relay Production)

- **Quyết định**:
  - Tích hợp container **Mailpit** (`axllent/mailpit:latest`) trong `docker-compose.yml` (Web UI port `8025`, SMTP port `1025`).
  - Sử dụng thư viện **MailKit** trong Background Worker để gửi thư qua giao thức SMTP chuẩn.
  - Chuyển đổi giữa Mailpit (Local) và Brevo SMTP Relay (Production, `smtp-relay.brevo.com:587`, TLS) 100% qua biến môi trường `SMTP__*`. Brevo là nhà cung cấp email chuẩn của dự án theo Hiến pháp và `docs/system-design/event-driven-design.md`; dùng SMTP Relay nên không cần SDK riêng, cùng một `SmtpEmailSender` cho cả hai môi trường.
- **Lý do lựa chọn**:
  - Mailpit nhẹ, nhanh hơn MailHog, cung cấp Web UI trực quan, hỗ trợ xem trước HTML, headers và responsive trên mobile.
  - Không bao giờ lo bị gửi nhầm email thật ra ngoài trong quá trình phát triển và kiểm thử.
  - Chuyển sang Brevo (`smtp-relay.brevo.com:587`, TLS) trên production chỉ cần điền thông tin vào `.env`, đúng quy chuẩn dùng chung toàn hệ thống.
- **Các phương án thay thế đã đánh giá**:
  - *Brevo REST API trực tiếp*: Phụ thuộc vào kết nối Internet và yêu cầu API key thật ngay cả khi chạy local; dùng SMTP Relay của Brevo giữ cùng một code path MailKit cho cả local và production.

---

## 5. Quyết định: Nâng cấp PostgreSQL sang pgvector và Bổ sung Redis 7

- **Quyết định**:
  - Nâng cấp image PostgreSQL trong `docker-compose.yml` lên `pgvector/pgvector:pg17`.
  - Thêm container Redis 7 (`redis:7-alpine`) trên mạng `data_net` chạy ở chế độ In-Memory (không dùng volume vĩnh viễn ở local dev).
- **Lý do lựa chọn**:
  - `pgvector:pg17` hoàn toàn là PostgreSQL 17 chuẩn, tương thích 100% với EF Core Npgsql hiện tại, nhưng được kích hoạt sẵn extension `vector` cho tính năng AI Recommendation trong tương lai.
  - Redis 7 phục vụ Caching thực đơn món ăn, rate limiting và phân tán khóa cho các microservices.
  - Chế độ Ephemeral của Redis giúp reset sạch sẽ cache khi chuyển đổi nhánh phát triển.

---

## 6. Quyết định: Nginx Gateway Chuẩn Hóa Tiếng Việt & Problem Details

- **Quyết định**:
  - Cấu hình `charset utf-8; charset_types application/json application/problem+json;` trong `nginx.conf`.
  - Định nghĩa fallback 404 trả về chuẩn RFC 7807 Problem Details có hỗ trợ tiếng Việt.
  - Bổ sung header nâng cấp kết nối (Upgrade / Connection) để hỗ trợ SSE và WebSockets.
- **Lý do lựa chọn**:
  - Ngăn ngừa lỗi hiển thị font tiếng Việt khi Gateway trả về thông báo lỗi JSON (tương tự như giải pháp đã áp dụng thành công trong `smart-wardrobe-be`).
