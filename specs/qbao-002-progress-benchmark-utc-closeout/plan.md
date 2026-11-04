# Close-out Progress T034 / Q01

Kế thừa qbao-001, base main 6df059d9858cff1db28aa9abc9782c7d3023abbf.
Không đổi DTO, RPC, event, dữ liệu lịch sử hoặc công thức BMI. Bảo quyết định Q01
theo ủy quyền Huy được Bảo xác nhận trong yêu cầu ngày 2026-11-04.

## Constitution Check

Service chỉ truy cập DB của mình; benchmark setup seed progress_db riêng, Identity
profile tạo qua API Identity. Runtime metrics gọi Identity bằng gRPC thật. JWT
ES256 thật; broker thật. Secrets/artifacts nằm trong artifacts/ gitignored.
Không bypass validation, không sửa migration cũ, không claim pass nếu chưa đo.

## Shared Files & Conflict Hotspots

- docs/decisions/README.md và ADR005 mới; không sửa ADR003/004.
- .github/workflows/ci.yml: kiểm tra evaluator và setup tool; benchmark thủ công
  trên runner có tài nguyên ghi rõ, không gán kết quả performance cho CI correctness.
- specs/qbao-001-progress-service/*: cập nhật Q01/T034 theo yêu cầu owner sau PR13 merge.
- specs/ivorlee-001-diet-service/frontend-guide.md, backend-integration-guide.md,
  handoffs/dev3-bao.md: chỉ Q01 được ủy quyền, không thay contract/logic khác của Huy.
- Diet test-only DietApiFactory cho phép injected TimeProvider và UTC test mới;
  không thay DI production hoặc endpoints của Diet.
- docs/system-design/progress-service.md: link ADR005.
- Không dự kiến sửa solution, package versions, DI, DbContext/migration. Nếu số đo
  cho thấy cần tối ưu sẽ khai phạm vi cụ thể trước sửa và giữ baseline.

## Kiểm chứng

UTC boundary tests deterministic + Diet explicit DateOnly/summary. Docker isolated
project, actual Progress/Identity images + PostgreSQL + RabbitMQ; k6 pinned1.3.0,
constant-arrival120req/s, warmup15s, gap5s, steady60s,3process runs. Dataset reset
trước mỗi run, history-heavy user10000metrics và32users đại diện; mix60/40.
Evaluator strict p95<150/<250, successful completions>=100RPS,0fail/drop,
đủ dữ liệu/duration/routes và3run. Ghi đủ failures, timeout, raw latency kể cả fail.
Full gates, Docker/Compose, actual HTTP flow trước commit/push/newPR; không merge.
