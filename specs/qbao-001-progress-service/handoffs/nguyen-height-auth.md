# Bảo bàn giao Nguyên — Height functional verified, caller-auth còn chặn

Progress đã dùng canonical GetUserHeight từ PR #14: 17525 →175.25 cm, deadline2s,
cancellation và error classification đúng; actual Identity process/PG →Progress
JWT API/PG đã kiểm chứng BMI snapshot, kể cả profile đổi chiều cao không đổi lịch sử.

Hiện provider nhận request không credentials. ADR001 §2.2 chấp nhận Compose network
trust, không có mTLS/service token/caller authorization; điều này khác yêu cầu
service-to-service authentication của lượt tích hợp này. JWT frontend không thay
service identity. Progress không sửa Identity và không thêm hard-coded API key.

Nguyên/Bảo cần chốt rõ trust model Height cho MVP: phê duyệt tường minh network
trust hiện tại hoặc triển khai authenticated caller với credential lifecycle và
authorization provider. Nếu triển khai, thống nhất contract metadata/audience/caller
policy trước để Bảo nối client; test caller hợp lệ, không credential và sai quyền
với actual provider trước khi gỡ BLOCKED. Không tự suy diễn đã có joint approval.

PR #13 giữ Draft, functional integration DONE nhưng secure readiness BLOCKED.
Q01/Huy/Diet và Q10 Weight là các đầu việc riêng, không đổi trong lượt Height.
