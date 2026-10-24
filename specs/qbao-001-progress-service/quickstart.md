# Hướng dẫn kiểm chứng

## Phase 1 hiện tại

Từ repository root, cần .NET 10, pwsh, GNU Make; nếu thiếu make dùng chính recipe
trong [Makefile](../../Makefile). Không chạy lệnh migration/seed/clean để kiểm tra tài liệu.

```powershell
pwsh scripts/speckit/team-feature.ps1 resolve -Json
pwsh scripts/speckit/team-feature.ps1 check
git diff --check
make lint
make check
```

Kiểm tra chỉ feature directory thay đổi, meta owner/branch/services đúng, links nội bộ
resolve được, không checkbox implement đã tick. Full gate có tests dùng Docker nên cần
engine đang hoạt động; thiếu engine phải ghi đúng lỗi, không skip test để claim pass.
Kết quả thực tế lưu ở [verification.md](verification.md), không suy từ hướng dẫn này.

## Phase 2–6 sau khi được giao triển khai

**Chưa chạy các kịch bản dưới đây ở Phase 1.** Prerequisite: các Q liên quan đã duyệt,
canonical contracts/ADR hợp nhất, Docker/Postgres/RabbitMQ chạy thật, Progress có host
và test projects, JWT hai user được fixture cấp, hạ tầng phối hợp Duy. Không in key/token
vào evidence. Làm theo [testing](../../docs/development/testing.md), dùng Makefile targets.

```powershell
make test PROJECTS=src/Services/Progress/tests/Progress.UnitTests
make test PROJECTS=src/Services/Progress/tests/Progress.IntegrationTests
```

| Kịch bản | Setup / thao tác | Assertion cụ thể |
| --- | --- | --- |
| Fasting qua đêm | Start 20:00Z target 16h, end 12:15Z hôm sau | 975 phút, actualHours 16.25, Completed, goal true. |
| Fasting cạnh tranh | 20 start cùng user, thêm start user B | DB chỉ 1 active/A, B độc lập; retry không thêm hàng. |
| Nước | A log 350/650 cùng ngày, B log riêng | A today consumed=1000; ngày rỗng=0; không mất cập nhật khi concurrent. |
| Ngủ | 23:00 đến 07:00 ngày sau cùng offset | 8h, quality đúng; invalid/overlap theo policy không ghi. |
| BMI lịch sử | Height 175, A log 70kg; profile đổi chiều cao | BMI snapshot tính từ 175 và bất biến; B không thấy A. |
| History | Nhiều timestamp + tie, khoảng ngày, nhiều trang | Sort ổn định, Count đúng, filter theo Q01, wire theo Q07. |
| Dashboard delta | Initial 72, baseline tuần trước 70, current 68.5 | Delta -1.5 khi fixture đúng cutoff Q06; không -3.5. |
| Streak | Meal/metric ngày 1,2,3,5 | Best=3; current theo clock/Q06, water-only không active. |
| Meal MVP thật | Create qua Diet-approved outbox/broker; duplicate/late, business date khác ngày publish | Union theo MealLog.Date, không ngày receive/processed; dedup không nhân ngày, bootstrap đầy đủ. |
| Future meal lifecycle | Chỉ sau approval và producer hỗ trợ sửa/chuyển/xóa | Revision/tombstone đúng, không xóa ngày còn metric; không giả Diet MVP đã có endpoint này. |
| Bootstrap race | Backfill snapshot + event đến lúc backfill | Watermark/replay không mất thay đổi, completeness chỉ true khi đủ. |
| E2E hạ tầng | HTTP qua Nginx ghi metric, broker cấp meal fixture đúng contract | DB có snapshot đúng, summary/streak cụ thể, không chỉ assert 200. |
| Observability | Gọi host rồi scrape /metrics | Prometheus target Progress UP và request counter tăng, dashboard Grafana hiển thị. |

Negative control: cố ý làm hỏng snapshot/unique index/dedup đúng lỗi cần bắt trong
workspace triển khai được kiểm soát, thấy test đỏ đúng nguyên nhân, khôi phục và thấy xanh;
không để mutation kiểm chứng tồn tại trong commit. Test thật DB/broker không mock persistence.
Load benchmark theo plan, report throughput/p95 và lỗi cùng cấu hình môi trường;
chưa đo thì không claim đạt NFR. Không gọi API AI hoặc gửi digest trong core E2E.
