# T034 / SC-006 — kết quả đo thật ngày 2026-11-04

**PASS cả 3 run; T034 DONE cho Progress trong cấu hình đo dưới đây.**

Source/tool commit: [c795500193a2deef1e2ae463734b6054e1797cc9](https://github.com/fa26-prn232-group2/longevia-be/commit/c795500193a2deef1e2ae463734b6054e1797cc9).
Working tree sạch lúc bắt đầu. Base production source6df059d; không sửa production logic,
schema hoặc migration trong close-out. Commit delivery sau số đo chỉ cập nhật evidence/docs.
[Machine-readable evidence](benchmark-evidence.json), [runner](../../scripts/progress/benchmark/README.md).

## Môi trường và đường đi thật

- Host: Windows-10-10.0.19045-SP0, AMD64; AMD Ryzen 5 5600 6-Core Processor              ; 6physical/12logical CPU.
- RAM host 16663976KiB; available lúc audit 5535752KiB.
- Docker Engine29.9.2, Linux/x86_64, 12CPU, 4107780096bytes RAM; phân biệt với RAM host.
- PostgreSQL 17.11 on x86_64-pc-linux-musl, compiled by gcc (Alpine 15.2.0) 15.2.0, 64-bit.
- .NET SDK10.0.203 trên host; server runtimes: {'progress': ['Microsoft.AspNetCore.App 10.0.14', 'Microsoft.NETCore.App 10.0.14'], 'identity': ['Microsoft.AspNetCore.App 10.0.14', 'Microsoft.NETCore.App 10.0.14']}.
- k6 grafana/k6:1.3.0; một instance Progress và một Identity, cùng Docker host với generator,
  PostgreSQL riêng mỗi service và RabbitMQ thật. Không chạy Testcontainers/build khác trong các cửa sổ đo.
- JWT ES256 thật; direct Kestrel HTTP/1, không qua Gateway. Metrics → production BodyProfileProvider
  → Identity HTTP/2 gRPC thật → profile175.25cm; không stub, không bypass JWT/validation.
- Progress pool80, Identity40, setup-only20; gRPC deadline2s, không retry.
- 240VU preallocated/max mỗi scenario không chồng nhau;33synthetic users. VU budget không phải RPS đo được.

| Container | CPU quota | RAM MiB | Image ID |
|---|---:|---:|---|
|identity|1|384|`sha256:32f2e1ffe46a629805b36460d44ef5ba9a3c95bc417f6605c7788457c7a25c5b`|
|progress|2|512|`sha256:96e49aa9f07cd4dda8b5237fa57b2b7dd0b734538ae718585ba273dad9691894`|
|progress-db|2|512|`sha256:ab20e934b27ba3ff927c1b4c9ff8cf6c32f315f743efe68dd3441182a87fb085`|
|identity-db|1|256|`sha256:ab20e934b27ba3ff927c1b4c9ff8cf6c32f315f743efe68dd3441182a87fb085`|
|rabbitmq|1|384|`sha256:4e32174ea7bfa3a979e37493b0b7348eb1f0c46f8201a12fd91c7db8098b0fa4`|
|generator|2|512|grafana/k6:1.3.0|

Tổng quota9CPU; Docker12CPU. Đây là host dùng chung generator/server, không phải chứng nhận capacity production.

## Dataset và workload

Mỗi run reset riêng progress_db benchmark qua EF; Identity users/profiles qua API thật.
33users: user nặng10000metrics,32user×100 =13200metrics;1980water,1980sleep,
2013fasting (33active/1980completed),1980meal contributions +1980inbox;5applied migrations, no pending model changes.
Anchor2026-10-25T00:00Z cố định (10ngày trước ngày đo), metric hourly về quá khứ,60ngày activity.
HTTP writes thêm logs ngày UTC hiện tại; boundary rollover/Monday kiểm chứng bằng deterministic regression,
không claim load window chạy đúng wall-clock UTC midnight. Meal BusinessDate khác CreatedAt/ProcessedAt.
Warmup15s, gap5s/drain, steady60s, bounded drain10s; open-loop120arrival/s,3process độc lập.
Mix: history20%(2pages),dashboard20%,water/today10%,fasting/current10%,water/log20%,metrics20%.
Không đưa setup vào latency. Fasting transitions được regression riêng, không tạo expected409 để tăng throughput.

## Số đo steady-state

|Run|Read p50/p95/p99 ms|Write p50/p95/p99 ms|Attempted|Success trong60s|Success RPS|HTTP fail/drop|Late drain|Verdict|
|---|---|---|---:|---:|---:|---|---:|---|
|1|3.889/11.207/13.368|4.978/6.772/8.473|7201|7200|120.000|0/0|1|PASS|
|2|3.439/10.621/12.475|4.956/6.420/8.005|7201|7200|120.000|0/0|1|PASS|
|3|3.671/11.132/12.958|5.262/7.310/9.916|7201|7201|120.017|0/0|0|PASS|

Nearest-rank trên mọi steady latency, kể cả failure/timeout/late. Monotonic k6 transport timings blocked+duration.
Success RPS chỉ đếm HTTP200 read/water hoặc201metric có completion trong window60s; late drain báo riêng,
giữ latency nhưng không cộng vào throughput. Không lấy offered120 làm measured120. Warmup không lỗi.
Ngưỡng strict: readp95<150ms, writep95<250ms, successfulRPS>=100,0HTTPfail/drop, cả3run đều đạt.

|Route|Run1 p95 ms / successRPS|Run2 p95 ms / successRPS|Run3 p95 ms / successRPS|
|---|---|---|---|
|/metrics/history?pageNumber=1&pageSize=20|5.070 / 12.000|4.537 / 12.000|4.725 / 12.017|
|/metrics/history?pageNumber=2&pageSize=20|5.051 / 12.000|4.430 / 12.000|4.866 / 12.000|
|/dashboard/summary|12.726 / 24.000|11.837 / 24.000|12.465 / 24.000|
|/water/today|2.208 / 12.000|2.457 / 12.000|2.390 / 12.000|
|/fasting/current|2.383 / 12.000|2.387 / 12.000|2.617 / 12.000|
|/water/log|5.105 / 24.000|5.021 / 24.000|6.559 / 24.000|
|/metrics|7.228 / 24.000|6.913 / 24.000|7.533 / 24.000|

## Persistence và tài nguyên

Mỗi run1800new metric snapshots khớp tổngHTTP201warmup+steady, HeightUsedCm175.25, weight70,
BMI22.79/NormalWeight;0invalid. Kiểm tra bằng EF read-only trên progress_db riêng, không đọc Identity DB.

|Run|Service|Peak CPU % (100=1core)|Peak RAM MiB|
|---|---|---:|---:|
|1|generator|18.37|100.00|
|1|identity|32.24|122.90|
|1|identity-db|8.64|71.55|
|1|progress|164.09|138.40|
|1|progress-db|150.02|202.00|
|1|rabbitmq|101.10|193.40|
|2|generator|17.45|99.38|
|2|identity|6.45|130.80|
|2|identity-db|5.81|73.38|
|2|progress|50.09|142.90|
|2|progress-db|36.49|202.90|
|2|rabbitmq|101.44|194.40|
|3|generator|17.94|99.59|
|3|identity|6.17|139.20|
|3|identity-db|7.84|73.45|
|3|progress|53.37|139.40|
|3|progress-db|30.63|203.90|
|3|rabbitmq|102.13|196.40|

Docker stats sampling có2s wait cộng thời gian gọi Docker; peak sampled không phải profiler liên tục.
Generator được lấy mẫu thật trong mỗi run. Khi k6 --rm thoát giữa ps và stats, chỉ fallback sau xác nhận
generator đã biến mất và lấy lại đủ5persistent services; lỗi server/daemon thật vẫn INCONCLUSIVE.
Không OOM. Rabbit bị throttle287/278/272periods (18.473/17.803/16.965s tổng throttled_usec quy đổi),
Progress PG5/0/1periods; Identity PG0/0/0. Đây là CPU quota contention quan sát được, không gán là0.
CPU cgroup deltas/throttling PG/Rabbit được ghi đầy đủ trong JSON; chiseled app không có cat
nên không có app throttle counter, không suy0throttling từ việc thiếu counter.

## Query plans và quyết định không tối ưu

|Run|Activity-query / history-query execution ms|Plan nodes|
|---|---|---|
|1|[7.914, 2.501]|Aggregate, Incremental Sort, Index Only Scan, Index Scan, Limit, Seq Scan, Sort|
|2|[8.567, 2.808]|Aggregate, Incremental Sort, Index Only Scan, Index Scan, Limit, Seq Scan, Sort|
|3|[7.069, 2.458]|Aggregate, Incremental Sort, Index Only Scan, Index Scan, Limit, Seq Scan, Sort|

EXPLAIN ANALYZE BUFFERS trên own isolated DB; SQL diagnostic bao gồm subquery tìm user nặng nên
execution time không phải latency riêng của exact app query. History ordering LoggedAt/Id có LIMIT20;
activity query đọc timestamp của user nặng. Plan hashes nằm trong JSON, full plans giữ ở ignored artifacts.
Read/write đều dưới ngưỡng với headroom lớn; không có căn cứ sửa query/index/cache. Không sửa code production
hoặc tạo migration, không có before/after optimization để claim. Không suy kết quả này cho dataset vô hạn.

## Audit các lượt không được nhận

- Baseline đầu:80VU preallocated, một cold run14dropped arrivals → không được nhận. Bộ đọc Python3.10
  gặp k6 fractional timestamps trước khi sửa parser. Hai run sau không thay thế yêu cầu3valid runs.
- Series d552f2f: HTTP/DB tốt nhưng sampler gặp generator --rm ở teardown; cả3báo INCONCLUSIVE.
  Giữ raw/results trong ignored sampler-boundary-d552f2f; không sửa verdict cũ thành PASS.
- c795500 sửa observer race với4resource tests; series hiện hành chạy lại đầy đủ3run, không loại slow/error samples.

## Reproduce, integrity và giới hạn

`python scripts/progress/benchmark/run.py` từ repo root. Cleanup đúng dedicated project:
`python scripts/progress/benchmark/run.py --cleanup`. Không xóa DB/volumes team.
Raw/result/logs giữ local ignored artifacts; không commit secrets/JWT/user IDs. RawSHA256 từng run:

- Run1: `78c407b8724d6fd57840aad42e35c8d8eddf9c904ba9a482923be94b86c9289d`; startUTC2026-11-04T14:35:25.172000+00:00.
- Run2: `049a700cafcfcde32622746f6fc3b71fec9fbb852bdfc16deaf0a97057355a66`; startUTC2026-11-04T14:37:03.841000+00:00.
- Run3: `2d82a64dde5841895e5b49d0b6358e274131c3b7112f4b031821698120e58406`; startUTC2026-11-04T14:38:41.699000+00:00.

Performance evidence là local controlled run, không phải GitHub CI benchmark. CI kiểm chứng correctness,
Docker build và tooling; finalHEAD job link được báo trong PR/final handoff. Q01 approval theoADR005
không chứng minh frontend đã rollout, broker/outbox production đã drain hay historical Dashboard completeness.
