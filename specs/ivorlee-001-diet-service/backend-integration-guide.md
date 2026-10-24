# Hướng dẫn backend — Diet độc lập

Trạng thái: IN_PROGRESS. Giai đoạn này không đổi gRPC method/message, event
contract hoặc topology. Counts: 0 RPC mới / 0 message mới / 0 event mới.
Sáu endpoint HTTP có [frontend-guide.md](frontend-guide.md).

- Diet chỉ truy cập diet_db bằng diet_user, không tham chiếu nội bộ service khác.
- Recommendation proto giữ nguyên; chưa có client runtime vì chưa có nguồn
  profile/allergies đáng tin cậy và contract HTTP hoàn chỉnh cho AI.
- Dev 1/dev 3 cần thống nhất profile/cân nặng và chiều gọi được duyệt qua ADR/
  system-design; không được gọi REST trực tiếp hoặc lấy DB làm đường tắt.
- Dev 3/5 cần contract meal-log phục vụ streak/digest trước khi thêm event.
  Producer sau này phải outbox cùng transaction; consumer idempotent.
- Local Mailpit UI localhost:8025, SMTP trong compose mailpit:1025; Grafana 3001.
  Diet không publish event hoặc gửi email trong giai đoạn độc lập.
- Metrics: AddObservability("DietService"), /metrics; Prometheus job diet:8080.
- Không đánh dấu READY_FOR_BACKEND cho toàn bộ Diet khi integration chưa chạy thật.

Chi tiết hoãn và điều kiện gỡ chặn: [deferred.md](deferred.md).
