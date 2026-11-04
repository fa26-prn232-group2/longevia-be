# Verification close-out — 2026-11-04

## Source và scope

- Start/base main6df059d9858cff1db28aa9abc9782c7d3023abbf, branch feat/progress-benchmark-utc-closeout.
- Main được fetch lại, không đổi từ assignment. PR13/15/16 đã merge; không reopen/merge PR nào.
- Q01 commitdd9c95b; toolingd552f2f; sampler fix và measured HEAD
  c795500193a2deef1e2ae463734b6054e1797cc9, working tree sạch lúc đo.
- Sau measuredHEAD chỉ cập nhật docs/evidence; không đổi production code, DTO, proto/event,
  applied migrations hoặc formula BMI. [Report](benchmark-report.md), [JSON](benchmark-evidence.json).
- Final deliveryHEAD/CI được kiểm tra trên GitHub PR checks và báo trong handoff;
  không tái sử dụng CI main41488082385/855tests làm evidence của thay đổi này.

## Gates thực thi

GNU Make không có trên máy: đọc Makefile rồi chạy format-check, Debug build,
Release build warnings-as-errors, full Release tests và transitive vulnerability scan.

|Gate|Kết quả thật|
|---|---|
|git diff --check|PASS|
|dotnet format longevia-be.slnx --verify-no-changes|PASS|
|dotnet build longevia-be.slnx --no-restore -warnaserror|PASS,0warning/error|
|dotnet build longevia-be.slnx -c Release --no-restore -warnaserror|PASS,0warning/error|
|dotnet test longevia-be.slnx -c Release -m:1|863/863PASS,9assemblies,0skip|
|dotnet list longevia-be.slnx package --vulnerable --include-transitive|27projects, không vulnerable package theo current NuGet sources|
|Seed Release build / format verify / vulnerability scan|PASS,0warning/error, không vulnerable package|
|python unittest discover scripts/progress/benchmark|19/19PASS|
|team-feature check|7featuresPASS,0warning|
|Docker Progress + Identity actual Dockerfiles|BuildPASS; actual images chạy trong benchmark|
|Compose benchmark config --quiet|PASS với isolated generated .env|
|Compose deployments config --quiet|PASS với repository .env.example; không có developer .env, không khởi chạy stack team|
|Progress pending-model check|Không pending changes;5migration applied trên own PG benchmark|

Full .NET counts: Diet45integration+12unit, Identity21integration+67unit,
Progress151integration+115unit, Recommendation64integration+372unit, Worker16unit =863.
151Progress integration gồm actual Height→BMI→Weight gRPC, JWT isolation, fasting
concurrency, sleep overlap, real RabbitMQ transport, snapshot/Postgres reconciliation,
UTC boundaries. Không skip/bypass broker hoặc dùng in-memory làm real-transport evidence.

Hai full-suite parallel run trước bị đỏ:1Height test nhận DeadlineExceeded thay
Unavailable;1Identity email E2E Worker queue startup timeout. Các test/assertion
không bị sửa; full suite chạy project tuần tự trên host (`-m:1`) PASS cả863.
Không khẳng định nguyên nhân đã được chứng minh chỉ từ repeat; CI chuẩn `make check`
vẫn chạy như cũ, không thêm skip hoặc sửa CI scheduling để che lỗi.

Local Windows checkout bị core.autocrlf chuyển CRLF: normalize tracked C# về LF để
chạy formatter, giữ BOM; restore chỉ native line endings khi nội dung canonical
giống HEAD. Không stage/commit whitespace noise của service khác.

## UTC evidence

-7new Progress unit cases:23:59:59Z/00:00Z, Việt Nam00:30/07:00, UTC Monday và
  Việt Nam Monday chưa sang UTC week. Full115unitPASS.
- Negative control đổi Day từ UtcDateTime sang DateTime:2FAIL/5PASS; restore đúng
  bytes rồi115PASS. Source production giữ nguyên, không có mutation trong Git.
- Diet UTC test mới thực thi riêng1/1PASS rồi full Diet45/45PASS trên PostgreSQL:
  TimeProvider trước/sau UTC midnight, default summary đổi ngày, explicit historical
  MealLog.Date và CreatedAt giữ nguyên. Không đổi Diet production DI/schema.
- Progress UTC tests kiểm tra water rollover, sleep wake/date/duration, UTC metric
  history boundaries, today/yesterday streak, UTC Monday fasting và meal/metric day union.
  Assert thêm persisted inbox BusinessDate và historical175cm/BMI22.86 không đổi sau replay.
- Evaluator19tests; in-memory mutant `<`→`<=` làm đúng2threshold testsFAIL. Missing,
  truncated, failed, dropped, insufficient duration/count không thể PASS. Actual k6
  fractional timestamps có regression test cho Python3.10.

## Performance acceptance

3independent process runs,15s warmup/60s steady,120offered/s, readp95<150ms,
writep95<250ms,success completions>=100/s,0HTTPfail/drop: cả3PASS.
Report ghi actual throughput, all-sample latencies, bounded late drain, per-route,
dataset, CPU/RAM/throttling/limits, image IDs, query-plan và rawSHA256.
Mỗi run1800metric snapshots được EF kiểm chứng height175.25/BMI22.79/NormalWeight.
Không tối ưu production hoặc tạo index vì baseline không cho thấy bottleneck cần sửa.

Các lượt preliminary không được nhận vẫn giữ ignored local raw/results: cold run
14dropped arrivals; Python timestamp parser chưa tương thích; d552f2f sampler race
khi k6 --rm thoát. Không sửa metrics/verdict cũ thành PASS. c795500 chạy lại đủ3run.
Trước reboot Docker disabled firmware/API500 chỉ là lịch sử setup failure; sau Bảo
bật SVM/reboot engine hoạt động. Không còn coi firmware là blocker hiện hành.

## Readiness riêng

Q01 APPROVED bởi Bảo thực hiện quyền Huy ủy quyền (ADR005), T034DONE cho môi trường
đo. Huy còn frontend auto-today UTC rollout/UX; Bảo và Duy còn evidence production
replay/outbox drainage/snapshot reconciliation và Dashboard completeness. Không sửa
public Dashboard DTO. Nguyên giữ canonical Identity/provider/auth ownership.
ADR001 internal-network trust không phải caller-authenticated service identity;
future multi-host trust phải review riêng. Không expose gRPC host hoặc đổi trust model.
Không claim production-ready toàn hệ thống hoặc100RPS của service khác.
