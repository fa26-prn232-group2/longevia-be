# Sửa Weight gRPC timestamp, integrity và kiểm chứng Identity E2E

Test cũ so timestamp100ns trong RAM với PostgreSQL microsecond và gọi flow seed DB là Identity E2E. Corrective so exact timestamp đọc lại từ DB, bổ sung chain actual Identity → Progress HTTP/JWT → Progress PostgreSQL → Weight HTTP/2, và kiểm tra outage DB khi gRPC listener vẫn sống.

- Provider phân biệt absence với Found=true thiếu/sai dữ liệu: sanitized INTERNAL + structured integrity log. Malformed protobuf timestamp trả INVALID_ARGUMENT; giữ cancellation/transport semantics, không đổi proto/Q10/authentication hay migrations.
- Tái sử dụng một Identity fixture giữa các nhóm test; immutable BMI, đổi height, historical as-of và hai-user isolation đã chạy thật. Server-unreachable tách khỏi real Npgsql outage.
- Local gates PASS: lint + make check equivalents,829/829 tests,0 skip; Progress unit108 và integration151. Debug/Release0 warnings/errors, vulnerable scan include-transitive27 projects sạch; Docker image, Compose example config, EF model/applied migrations, team registry và diff check PASS.
- Timestamp negative control đỏ đúng lỗi; bản sửa lặp3 lượt, mỗi lượt2/2 PASS trên PostgreSQL. Checkout CRLF được kiểm chứng bằng snapshot source LF trên ổ D, đã đối chiếu10 file C#; không normalize file ngoài scope. Local test process nhìn thấy2 CPU, không đổi assertion hay filter full suite.

HEAD bắt đầu `74cea4bd517148eded4046cd38963375dc6660b9` có Push/PR CI FAILURE và image step skip. CI của corrective published SHA sẽ được kiểm tra sau push và cập nhật tại PR; chưa suy CI PASS từ local. Chi tiết/hồ sơ cũ: [verification.md](verification.md).

Feature `qbao-001-progress-service`, PR #13 giữ OPEN/DRAFT; không merge hay tự mark Ready for Review. Huy còn Diet client/snapshot, Q01 acknowledgment PENDING và replay; Nguyên còn Shared Auth follow-up; Duy phối hợp rollout/infra. Production replay/outbox drain/Dashboard completeness chưa được chứng minh bằng fixture, độc lập Q10 approval.
