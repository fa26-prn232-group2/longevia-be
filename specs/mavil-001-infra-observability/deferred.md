# Deferred — Hạ tầng, Nginx Gateway, Background Worker & Giám sát Grafana

Công việc phát hiện trong lúc build feature `mavil-001-infra-observability` nhưng **không thuộc phạm vi của feature này**. Không mục nào ở đây chặn việc đóng feature; không mục nào là task của feature này.

## Các mục

### D-01 — Producer-side transactional outbox cho event Identity

- **Vấn đề**: Hiến pháp §III yêu cầu event được publish trong cùng transaction nghiệp vụ (transactional outbox). Feature này chỉ làm phía consumer (Worker) và contract event; Identity chưa có outbox/relay.
- **Vì sao ngoài phạm vi**: Việc ghi bảng outbox và relay nền thuộc service sở hữu `identity_db` cùng migration của Identity, trong khi quy tắc nhóm là 1 service = 1 feature đang mở tạo migration. Nghĩa vụ này đã ghi trong `data-model.md` §1.1 và `contracts/events/v1/README.md` để producer tuân thủ.
- **Gỡ chặn bằng cách nào**: Mở feature phía Identity (owner Identity) thêm bảng outbox, ghi event cùng transaction, background relay publish lên `longevia.events`; sau đó thêm test e2e chứng minh không mất event khi transaction commit.
- **Ảnh hưởng tới task đã tick**: Không. T017 chỉ cam kết consumer idempotent + retry/DLQ (vẫn đúng khi producer thêm outbox).

### D-02 — Rate limiting ở tầng gateway

- **Vấn đề**: `spec.md` FR-005 ban đầu nêu Redis phục vụ "kiểm soát tần suất truy cập". Feature chỉ dùng Redis cho cache/idempotency dedup; chưa có cơ chế rate limiting (Nginx `limit_req` hoặc Redis-based).
- **Vì sao ngoài phạm vi**: Rate limiting là hành vi client-facing mới, cần chọn ngưỡng, mã lỗi (429 ProblemDetails) và ảnh hưởng tới mọi client; chưa có yêu cầu/ngưỡng nào trong `docs/requirements/`. FR-005 đã được thu hẹp lại cho đúng phạm vi feature này.
- **Gỡ chặn bằng cách nào**: Bổ sung yêu cầu rate limiting (ngưỡng/route) vào `docs/requirements/`, rồi thêm `limit_req_zone`/`limit_req` trong `deployments/nginx/nginx.conf` cùng test e2e trả `429` kèm ProblemDetails.
- **Ảnh hưởng tới task đã tick**: Không (không task nào của feature này cam kết rate limiting).

### D-03 — Adoption observability cho service tương lai

- **Vấn đề**: SC-002 và quy chuẩn `AGENTS.md` §3 áp lên mọi microservice, nhưng hiện giải pháp chỉ có `Identity.Api` và `Worker`; Diet/Progress/Recommendation chưa tồn tại.
- **Vì sao ngoài phạm vi**: Các service đó chưa được tạo trong repo; feature này chỉ cung cấp extension và quy chuẩn để chúng kế thừa.
- **Gỡ chặn bằng cách nào**: Khi feature của từng service được mở, áp 2 dòng `AddObservability`/`MapPrometheusScrapingEndpoint` và thêm scrape job vào `deployments/prometheus/prometheus.yml`.
- **Ảnh hưởng tới task đã tick**: Không (SC-002 đã được ghi rõ phạm vi là service hiện có).

### D-04 — TLS/HSTS tại gateway

- **Vấn đề**: `deployments/nginx/nginx.conf` chỉ `listen 80`, không TLS/HSTS, nên bearer token đi plaintext trên đường client → gateway; trong khi `docs/system-design/architecture.md` mô tả gateway là HTTP/HTTPS.
- **Vì sao ngoài phạm vi**: TLS termination cần chứng chỉ, cơ chế cấp phát (ACME hoặc cert nội bộ) và quyết định hạ tầng triển khai — chưa có yêu cầu/chứng chỉ nào trong feature hạ tầng local này; thêm vào sẽ đổi hành vi client-facing ngoài spec.
- **Gỡ chặn bằng cách nào**: Bổ sung ADR cho TLS termination (nơi đặt cert, auto-renew), rồi thêm `listen 443 ssl` + redirect 80→443 + HSTS trong `nginx.conf` và test e2e HTTPS.
- **Ảnh hưởng tới task đã tick**: Không (FR-002 chỉ yêu cầu routing/prefix/SSE; không task nào cam kết TLS).

### D-05 — Xác minh Brevo SMTP Relay trên môi trường thật

- **Vấn đề**: US3 AS3 yêu cầu Worker gửi được thư qua Brevo SMTP Relay (`smtp-relay.brevo.com:587`, TLS). Feature chỉ kiểm chứng được nhánh Mailpit local (SC-003); nhánh Brevo cần credential thật và kết nối Internet.
- **Vì sao ngoài phạm vi**: Cần `SMTP__USER`/`SMTP__PASSWORD` của Brevo — secret production, không có trong repo và không được commit; agent không chạy thử gửi thư thật.
- **Gỡ chặn bằng cách nào**: Một lần kiểm tay có kiểm soát: đặt 3 biến `SMTP__HOST=smtp-relay.brevo.com`, `SMTP__PORT=587`, `SMTP__USE_SSL=true` cùng credential trong `.env`, chạy Worker, publish `UserRegisteredEvent` với email của người kiểm, xác nhận nhận thư và ghi lại bằng chứng (thời điểm, message-id) vào báo cáo.
- **Ảnh hưởng tới task đã tick**: Không (T015 cam kết code path TLS + không log secret; T028 chỉ cam kết SC-003 trên Mailpit local).

### D-06 — SSE streaming buffering tại gateway

- **Vấn đề**: FR-002 yêu cầu "cấu hình đầy đủ hỗ trợ kết nối thời gian thực (SSE/WebSockets)"; `nginx.conf` đã có `map $http_upgrade`, header `Upgrade`/`Connection` và `proxy_read_timeout 300s`, nhưng chưa tắt `proxy_buffering` nên response SSE có thể bị gom đệm thay vì chảy theo thời gian thực.
- **Vì sao ngoài phạm vi**: Repo chưa có endpoint SSE nào để chứng minh hành vi này; thêm `proxy_buffering off` lúc này là thay đổi chưa kiểm thử được (Hiến pháp §V yêu cầu thay đổi hành vi phải có test). WebSocket hiện đã được hỗ trợ đầy đủ bằng upgrade headers.
- **Gỡ chặn bằng cách nào**: Khi feature của service có endpoint SSE đầu tiên được mở, thêm `proxy_buffering off` (và `add_header X-Accel-Buffering no`) cho location SSE tương ứng, kèm smoke test assert sự kiện đầu tiên đến client trong < 1s.
- **Ảnh hưởng tới task đã tick**: Không (T007 cam kết cấu hình upgrade/SSE theo hiện trạng; phần buffering được theo dõi ở đây).
