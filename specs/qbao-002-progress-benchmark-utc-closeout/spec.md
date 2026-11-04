# Close-out T034 / SC-006 và Q01

Owner qbao/Bảo; branch feat/progress-benchmark-utc-closeout, base main6df059d.
Yêu cầu hiện hành ngày2026-11-04; tiếp nối Progress đã merge PR13, không mở lại PR đó.

1. ADR005 Accepted: Bảo thực hiện quyền Q01 Huy ủy quyền; canonical UTC calendar
   cho server-derived days, Diet explicit DateOnly giữ nguyên. Không claim Huy ký.
2. Regression deterministic UTC midnight/Vietnam difference/Monday/meal replay,
   giữ historical BMI/meal data và union active day. Không đổi public contracts.
3. Real HTTP Kestrel/PG/JWT + actual IdentityHeight dependency; isolated synthetic
   dataset heavy user10000metrics, representative33users, history và inbox hợp lệ.
4. k6constant-arrival,15s warmup,60smeasurement,3independent runs. Mọi run phải
   readp95<150ms,writep95<250ms,successful completions>=100RPS,0failure/drop.
5. Evaluator strict boundary/missing/failure tests; metadata host/Docker/resources,
   raw evidence và per-route latency/RPS, EXPLAIN trên DB benchmark riêng.
6. Chỉ tối ưu Progress khi baseline thật chỉ ra bottleneck; giữ correctness.
7. Full gates + Docker/Compose + benchmark pass trước T034DONE/newPRreview.
   Gate đỏ không commit/push. Không merge bất kỳ PR nào.

Frontend auto-today UTC không đồng nghĩa frontend đã rollout; Dashboard historical
completeness và service-caller auth không được tự gỡ trong scope Q01.
