# Progress SC-006 benchmark

Chạy từ repo root; Python3.10+, .NET10, Docker Compose Linux engine hoạt động.
Không trỏ vào stack team/production. Script dùng project riêng cố định
`longevia-progress-sc006-benchmark`,2 PostgreSQL containers, RabbitMQ,1Progress và
1Identity actual Dockerfile; JWT ES256 thật, client production BodyProfileProvider.
Không bypass auth/validation; không tự lấy Identity height từ DB. Setup seed Progress
qua EF/own DB, Identity users/profiles qua REST chính thức. Không sửa schema bằng tay.

```powershell
python -m unittest discover -s scripts/progress/benchmark -p test_*.py -v
python scripts/progress/benchmark/run.py
python scripts/progress/benchmark/run.py --cleanup
```

Cleanup chỉ exact dedicated project trên; xóa volumes benchmark riêng, không dùng
Compose thường của team. Không chạy hai benchmark cùng lúc. `.env`, signing keys,
synthetic JWT/users, raw logs/metrics nằm trong gitignored `artifacts/progress-benchmark/`.
Không upload toàn bộ thư mục đó. Report compact phải bỏ credentials/JWT/user IDs.

## Workload và dataset

k6 `grafana/k6:1.3.0`, constant-arrival120requests/s; warmup15s, gap5s để warmup
drain, steady60s, graceful drain10s;3process runs độc lập, reset dataset trước mỗi run.
240 VUs preallocated và240max cho mỗi scenario. Read/write60/40 cố định mỗi10request:
history20%(2pages),dashboard20%,water/today10%,fasting/current10%,water/log20%,metrics20%.
Water write success200, metric write201; read200. Actual routes direct Progress
Kestrel HTTP1: `/metrics/history`, `/dashboard/summary`, `/water/today`, `/fasting/current`,
`/water/log`, `/metrics`. Gateway `/api/progress` prefix strip không nằm trong số đo này.
Fasting start/end không vào mixed load: seed active/completed để không tạo expected409
do state transitions. Regression concurrency hiện hữu kiểm chứng workflow này.

Dataset33users: heavy user10000metrics,32user khác mỗi100 =>13200metrics;
1980water,1980sleep,1980meal contributions +1980inbox,2013fasting (1980completed+33active).
Anchor UTC cố định2026-10-25; history hourly ngược về quá khứ;60ngày activity/sleep/water.
MealDate được chỉ định, CreatedAt/processedAt khác ngày để không suy ngày từ replay.
Không đưa thời gian seed vào latency. Trước mỗi run seed reset **chỉ DB benchmark**;
users/profiles giữ ID trong run/session, keys JWT tương ứng; không truy cập DB dev.

Giới hạn mỗicontainer trong compose; aggregate CPU quota có thể cao hơn Docker CPUs,
vì vậy report phải ghi contention thực tế. Generator/servers chung host; kết quả chỉ
áp dụng cấu hình đo, không khẳng định production hoặc service khác đạt100RPS.

## Evidence và evaluator

`results.json`, `report.md`, `run-N.json`, `query-plan-N.txt` được tạo tự động.
Raw `raw-N.json` k6JSON points giữ để audit; SHA256 được ghi trong result.
Metadata ghi sourceHEAD/dirtytree, host vs DockerCPU/RAM, runtime/tool/imageIDs,
resource limits, instances, pool limits, dataset,2sIdentitydeadline. Samples Docker
stats ghi CPU/RAM; PG/Rabbit cgroup cpu.stat có throttling; chiseled app không có
cat nên ghi rõ thiếu counter thay vì giả số0. Không claim dedicated host nếu có contention.

Evaluator dùng nearest-rank p50/p95/p99 trên **mọi** steady sample, kể cả timeout/lỗi;
latency dùng monotonic k6 transport timings (blocked+duration gồm connect/TLS/DNS).
Successful RPS = success completions có timestamp trong scenario window60s/60;
Late completions vẫn có latency, được báo riêng và loại khỏi successful RPS; drain hữu hạn không phải HTTP failure.
Drop arrivals, HTTPfailures, missing/truncated evidence không thể PASS. Warmup
results riêng, không gộp latency warmup vào steady; error xảy ra trong steady không
được loại vì gọi là warmup. Chỉ PASS khi3run đủ evidence và đều readp95<150,
writep95<250,successfulRPS>=100,0error/drop. Offered120 không tự chứng minh120RPS.

EXPLAIN ANALYZE BUFFERS chỉ trên isolated progress_db, không trên DB team.
Không tối ưu query/caching/index trước khi baseline đo được cho thấy bottleneck.
Không coi CI tooling/unit tests là performance acceptance. Chạy command trên runner
có Docker và tài nguyên ghi rõ; gate đỏ hoặc virtualization chưa bật là BLOCKED,
T034 chưa DONE và chưa được commit/push theo constitution gate policy.
