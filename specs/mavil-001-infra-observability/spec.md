# Đặc tả Tính năng: Hạ tầng, Nginx Gateway, Background Worker & Giám sát Grafana

**Feature Branch**: `feat/infra-observability`  
**Created**: 2026-10-22  
**Status**: Draft  
**Owner**: `mavil`  
**Input**: Triển khai kế hoạch thiết lập hạ tầng Docker Compose, Nginx Gateway, Background Worker, hệ thống giám sát Prometheus/Grafana (Port 3001) và hướng dẫn chuẩn hóa cho AGENTS.md.

---

## User Scenarios & Testing *(Bắt buộc)*

### User Story 1 - Khởi động & Quản trị Hạ tầng Phát triển Cục bộ Một Chạm (Độ ưu tiên: P1)

Là một lập trình viên trong nhóm dự án, tôi muốn có một bộ lệnh duy nhất để khởi động toàn bộ các dịch vụ phụ trợ (Cơ sở dữ liệu, Hàng đợi thông điệp, Cổng API, Hộp thư giả lập) nhằm thiết lập môi trường làm việc local nhanh chóng mà không cần cài đặt thủ công từng phần mềm vào hệ điều hành.

**Tại sao ưu tiên P1**: Đây là điều kiện tiên quyết để tất cả 5 thành viên trong nhóm có thể phát triển, chạy thử và kiểm thử tích hợp các microservices.

**Kiểm thử độc lập**: Có thể kiểm tra độc lập bằng cách chạy lệnh `make infra-up` và xác minh: Nginx Gateway phản hồi `200 OK` tại `/healthz`; PostgreSQL sẵn sàng qua `pg_isready`; RabbitMQ sẵn sàng qua Management API (`http://localhost:15672`); Mailpit Web UI phản hồi HTTP `200 OK` tại `http://localhost:8025`. (PostgreSQL qua TCP 5432, RabbitMQ AMQP 5672 và Mailpit SMTP 1025 không phải HTTP nên không thể trả `200 OK` — kiểm tra chúng bằng giao thức tương ứng.)

**Kịch bản chấp nhận (Acceptance Scenarios)**:
1. **Given** lập trình viên đã tải mã nguồn dự án, **When** thực thi lệnh khởi động hạ tầng, **Then** các dịch vụ PostgreSQL (với tiện ích mở rộng vector), RabbitMQ, Nginx, Redis và Mailpit phải được khởi chạy ở trạng thái sẵn sàng (Healthy).
2. **Given** hạ tầng đang chạy, **When** lập trình viên gửi yêu cầu HTTP tới cổng API Gateway tại `/healthz`, **Then** cổng Nginx phải phản hồi mã `200 OK` với độ trễ dưới 50ms.
3. **Given** hạ tầng đang chạy, **When** gửi yêu cầu tới một đường dẫn API không tồn tại qua Gateway, **Then** hệ thống phải trả về cấu trúc lỗi chuẩn RFC 7807 (Problem Details) định dạng JSON với bảng mã ký tự tiếng Việt UTF-8.

---

### User Story 2 - Đóng gói Container An toàn và Tối ưu cho Dịch vụ (Độ ưu tiên: P1)

Là một kỹ sư vận hành, tôi muốn các microservices .NET được đóng gói bằng cơ chế multi-stage container siêu nhẹ, không chứa shell hệ thống và chạy dưới quyền non-root nhằm tối đa hóa tính bảo mật và giảm dung lượng image lưu trữ.

**Tại sao ưu tiên P1**: Đảm bảo tiêu chuẩn đóng gói ứng dụng an toàn cho môi trường sản xuất (Production-ready) và ngăn chặn nguy cơ leo thang đặc quyền khi bị tấn công.

**Kiểm thử độc lập**: Có thể kiểm tra bằng cách build image của Identity và Worker, kiểm tra user runtime là non-root (`UID 1654`) và kích thước image tối ưu.

**Kịch bản chấp nhận (Acceptance Scenarios)**:
1. **Given** mã nguồn của service, **When** thực hiện lệnh build container, **Then** quá trình biên dịch và xuất bản diễn ra thành công với cảnh báo bằng 0.
2. **Given** container đang chạy trong Docker Compose, **When** kiểm tra thông tin tiến trình, **Then** ứng dụng phải thực thi dưới quyền người dùng không đặc quyền (`app`) và kết nối thành công với mạng cơ sở dữ liệu nội bộ.

---

### User Story 3 - Xử lý Tác vụ Nền & Gửi Email Tự động (Độ ưu tiên: P1)

Là hệ thống backend, tôi muốn các tác vụ tốn thời gian như gửi email xác thực tài khoản và thông báo được đẩy vào hàng đợi thông điệp để xử lý ngầm (Asynchronous), giúp luồng đăng ký của người dùng phản hồi ngay lập tức.

**Tại sao ưu tiên P1**: Đảm bảo tính sẵn sàng và trải nghiệm người dùng mượt mà, phân tách rõ ràng giữa nghiệp vụ xác thực và gửi thư.

**Kiểm thử độc lập**: Đẩy một sự kiện đăng ký người dùng vào RabbitMQ, kiểm tra Worker nhận được thông điệp và email xuất hiện trên giao diện Web UI của Mailpit.

**Kịch bản chấp nhận (Acceptance Scenarios)**:
1. **Given** một sự kiện đăng ký tài khoản được xuất bản, **When** Background Worker tiêu thụ thông điệp, **Then** một email định dạng HTML chuẩn chứa liên kết xác thực phải được gửi tới hộp thư của người nhận.
2. **Given** môi trường phát triển cục bộ, **When** email được gửi đi, **Then** lập trình viên có thể mở giao diện Web tại cổng `8025` (Mailpit) để xem nội dung email trực quan mà không cần tài khoản email thật.
3. **Given** cấu hình máy chủ Brevo SMTP Relay trên môi trường thực tế, **When** Worker gửi thư, **Then** hệ thống phải thiết lập kết nối an toàn có mã hóa TLS để truyền tải email thành công.

---

### User Story 4 - Giám sát Toàn diện Hiệu năng với Prometheus & Grafana (Độ ưu tiên: P2)

Là một kỹ sư hệ thống hoặc lập trình viên phụ trách dịch vụ, tôi muốn theo dõi biểu đồ thời gian thực về tải CPU, bộ nhớ, lưu lượng request và tỷ lệ lỗi của từng service thông qua giao diện Grafana trực quan.

**Tại sao ưu tiên P2**: Giúp phát hiện sớm các điểm nghẽn cổ chai (Bottlenecks), rò rỉ bộ nhớ hoặc lỗi diện rộng trước khi người dùng phát hiện.

**Kiểm thử độc lập**: Truy cập `http://localhost:3001` và kiểm tra Dashboard hiển thị số liệu trực tiếp từ các microservices.

**Kịch bản chấp nhận (Acceptance Scenarios)**:
1. **Given** hệ thống đang vận hành, **When** lập trình viên truy cập cổng `3001` trên trình duyệt, **Then** giao diện Grafana phải hiển thị và nhường hoàn toàn cổng `3000` cho ứng dụng giao diện (Frontend).
2. **Given** Grafana vừa khởi động, **When** người dùng mở danh sách Dashboard, **Then** biểu đồ giám sát ASP.NET Core (.NET Runtime, Memory, Throughput, p95 Latency, Error Rate) đã được nạp sẵn tự động (Provisioned) mà không cần cấu hình bằng tay.
3. **Given** một service phát sinh các lượt gọi API, **When** Prometheus thực hiện chu kỳ cào dữ liệu (mỗi 10 giây), **Then** chỉ số lưu lượng và thời gian phản hồi trên Grafana phải được cập nhật tương ứng.

---

### User Story 5 - Chuẩn hóa Hướng dẫn Tích hợp Đo lường cho Toàn Đội ngũ (Độ ưu tiên: P2)

Là một lập trình viên mới bắt đầu viết một microservice mới (như Diet, Progress, Recommendation), tôi muốn có hướng dẫn rõ ràng trong tài liệu điều phối dự án (`AGENTS.md`) để có thể kích hoạt tính năng tự động đo lường chỉ với 2 dòng mã nguồn chuẩn dùng chung.

**Tại sao ưu tiên P2**: Đảm bảo mọi service mới được tạo ra đều tự động tích hợp vào hệ thống giám sát mà không tốn công sức cấu hình lại từ đầu.

**Kiểm thử độc lập**: Áp dụng hướng dẫn vào một service mẫu và kiểm tra endpoint `/metrics` trả về số liệu OpenTelemetry chuẩn xác.

**Kịch bản chấp nhận (Acceptance Scenarios)**:
1. **Given** tài liệu `AGENTS.md`, **When** lập trình viên đọc phần quy chuẩn giám sát, **Then** tài liệu phải mô tả chính xác cách gọi hàm tiện ích đo lường từ thư viện dùng chung.
2. **Given** service mới đã tích hợp hàm tiện ích, **When** Prometheus thực hiện cào dữ liệu, **Then** service mới phải xuất hiện trong danh sách các mục tiêu (Targets) đang hoạt động bình thường.

---

## Tình huống Biên (Edge Cases)

- **Khi cơ sở dữ liệu hoặc RabbitMQ khởi động chậm**: Các dịch vụ ứng dụng (Identity, Worker) phải có cơ chế chờ và tự động kết nối lại (Retry policy / Healthcheck condition) mà không bị crash ngay lập tức.
- **Khi Nginx nhận request chứa ký tự đặc biệt hoặc ngôn ngữ tiếng Việt**: Phản hồi lỗi 404/502 phải giữ nguyên bảng mã UTF-8, không bị lỗi font hoặc vỡ cấu trúc JSON.
- **Khi dịch vụ gửi email gặp sự cố mạng tạm thời**: Worker phải áp dụng cơ chế thử lại (Retry with exponential backoff) và chuyển vào hàng đợi thư chết (Dead-letter queue) sau số lần thử quy định.
- **Khi xung đột cổng mạng cục bộ**: Cổng Grafana được cố định ở `3001` để bảo vệ cổng `3000` dành riêng cho phát triển Frontend.

---

## Yêu cầu Hệ thống *(Bắt buộc)*

### Yêu cầu Chức năng (Functional Requirements)

- **FR-001**: Hệ thống PHẢI cung cấp tệp cấu hình Docker Compose phân tách 3 phân vùng mạng (`edge`, `internal_net`, `data_net`) để bảo vệ dữ liệu nội bộ theo kiến trúc dự án.
- **FR-002**: Hệ thống PHẢI có dịch vụ Nginx đóng vai trò Cổng API duy nhất, hỗ trợ định tuyến theo tiền tố dịch vụ, loại bỏ tiền tố trước khi chuyển tiếp, và cấu hình đầy đủ hỗ trợ kết nối thời gian thực (SSE/WebSockets).
- **FR-003**: Cổng Nginx PHẢI trả về thông điệp lỗi 404 theo chuẩn cấu trúc RFC 7807 Problem Details có hỗ trợ tiếng Việt UTF-8.
- **FR-004**: Hệ thống PHẢI cung cấp máy chủ lưu trữ cơ sở dữ liệu PostgreSQL 17 tích hợp tiện ích mở rộng pgvector và tự động khởi tạo các database riêng biệt cho từng service.
- **FR-005**: Hệ thống PHẢI tích hợp máy chủ bộ đệm Redis 7 trên phân vùng mạng dữ liệu để phục vụ nhu cầu lưu tạm và kiểm soát tần suất truy cập.
- **FR-006**: Hệ thống PHẢI tích hợp máy chủ thư giả lập Mailpit cho môi trường phát triển cục bộ, cung cấp giao diện Web trực quan tại cổng `8025` và cổng nhận thư SMTP tại `1025`.
- **FR-007**: Background Worker PHẢI hỗ trợ gửi email qua giao thức SMTP chuẩn, linh hoạt chuyển đổi giữa Mailpit (Local) và Brevo SMTP Relay (Production, `smtp-relay.brevo.com:587`, TLS) thông qua biến môi trường. Mọi service khác trong hệ thống cũng gửi email qua Brevo theo `docs/system-design/event-driven-design.md` và Hiến pháp — tính năng này không thêm nhà cung cấp thứ hai.
- **FR-008**: Background Worker PHẢI chứa sẵn 4 mẫu thư điện tử HTML chuẩn: Xác thực tài khoản, Đặt lại mật khẩu, Chào mừng và Báo cáo tiến độ tuần. Worker CHỈ render mẫu từ payload đã đủ của event (không tự tổng hợp số liệu nghiệp vụ — mọi tính toán như calo trung bình, giờ nhịn ăn, chuỗi Streak do service producer chuẩn bị trước, xem `data-model.md` §2.2).
- **FR-009**: Hệ thống PHẢI tích hợp Prometheus để thu thập dữ liệu định kỳ mỗi 10 giây từ các dịch vụ thành viên.
- **FR-010**: Hệ thống PHẢI tích hợp Grafana tại cổng `3001`, tự động nạp sẵn Prometheus làm nguồn dữ liệu và nạp sẵn biểu đồ giám sát ASP.NET Core.
- **FR-011**: Thư viện dùng chung PHẢI cung cấp hàm mở rộng OpenTelemetry để các service kích hoạt đo lường tự động (CPU, Bộ nhớ, Thông lượng request, Độ trễ) chỉ với 2 dòng code chuẩn trong `Program.cs` (`AddObservability()` + `MapPrometheusScrapingEndpoint()`), khớp quy chuẩn `AGENTS.md` §3.
- **FR-012**: Tài liệu `AGENTS.md` PHẢI được cập nhật quy chuẩn tích hợp Observability để mọi thành viên trong dự án tuân thủ khi viết service mới.

---

## Tiêu chí Thành công *(Bắt buộc)*

### Kết quả Đo lường được (Measurable Outcomes)

- **SC-001**: Thời gian từ khi lập trình viên gõ lệnh khởi động hạ tầng (`make infra-up`) đến khi toàn bộ hạ tầng sẵn sàng không vượt quá 30 giây.
- **SC-002**: 100% các microservices được build trên nền tảng runtime chiseled non-root, giảm kích thước image xuống dưới 180MB mỗi container.
- **SC-003**: 100% email gửi đi từ Worker trong môi trường phát triển hiển thị tức thì trên giao diện Mailpit trong vòng dưới 1 giây.
- **SC-004**: Grafana hiển thị trực quan biểu đồ hiệu năng ngay sau khi bật mà không đòi hỏi bất kỳ thao tác cấu hình thủ công nào từ phía người dùng.
- **SC-005**: Toàn bộ giải pháp vượt qua cổng kiểm tra chất lượng `make lint` với 0 lỗi và 0 cảnh báo.

---

## Làm Rõ Yêu Cầu (Clarifications)

- **Q1 (Xác thực Grafana)**: Trong môi trường phát triển cục bộ, Grafana được cấu hình bật **Anonymous Access** với vai trò `Viewer` (chỉ xem Dashboard và Datasource, không được sửa/xóa cấu hình đã provision). Lập trình viên mở `http://localhost:3001` xem được ngay Dashboard mà không cần nhập tài khoản `admin/admin`.
- **Q2 (Lưu trữ Redis)**: Container Redis chạy ở chế độ **In-Memory / Ephemeral** (không gắn volume vĩnh viễn), tự động làm sạch cache sau mỗi lần khởi động lại môi trường dev, tránh tồn đọng cache rác khi chuyển đổi nhánh.
- **Q3 (Chính sách Thử lại Worker)**: Khi gửi email thất bại, Worker áp dụng cơ chế thử lại 3 lần với khoảng cách tăng dần (**Exponential Backoff: 2s, 5s, 10s**) trước khi chuyển thông điệp vào Dead-Letter Queue (DLQ).

---

## Giả định (Assumptions)

- **AS-001**: Máy trạm phát triển đã cài đặt sẵn Docker Desktop / Docker Engine và PowerShell 7.
- **AS-002**: Cổng `3000` được dành riêng cho ứng dụng Frontend; cổng `3001` dành riêng cho Grafana Dashboard.
- **AS-003**: Cổng gửi email trong môi trường thực tế sử dụng giao thức SMTP có hỗ trợ bảo mật TLS (Brevo SMTP Relay `smtp-relay.brevo.com:587`), khớp Hiến pháp và `docs/system-design/event-driven-design.md`.
- **AS-004**: Mọi service mới trong tương lai đều tham chiếu thư viện dùng chung `Longevia.Shared` để tái sử dụng cấu hình đo lường.
