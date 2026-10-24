# Progress Service — Phase 1 specification và implementation plan

**Tiêu đề**: docs(progress): Phase 1 specification and implementation plan
**Base**: main | **Head**: feat/progress-service | **Feature**: qbao/001/progress-service

Đề nghị review tài liệu Phase 1 và các quyết định PENDING trước khi triển khai.
Không công bố trạng thái implementation-ready.

## Summary

Đặc tả Progress Service cho body metrics/BMI, fasting, water/sleep và dashboard/streaks;
phân rã roadmap Phase 2–6 với các dependency chưa có contract được chặn rõ ràng.

## Phạm vi Phase 1

Chỉ tài liệu trong specs/qbao-001-progress-service. Không business code, migration,
canonical contract, thay đổi Gateway/Worker hoặc source thành viên khác.

## Artifacts

meta.json, spec.md, plan.md, tasks.md, research.md, data-model.md,
contracts/README.md, quickstart.md, deferred.md, checklists/requirements.md,
verification.md và nội dung PR này. 19 FR, 7 SC, 37 task chưa thực hiện.

## User stories

- US-PROG-01: log chỉ số, snapshot BMI/height, history/date/pagination.
- US-PROG-02: start/current/end, Custom, retry/concurrency/ownership.
- US-PROG-03: water totals/target, sleep qua nửa đêm/quality/validation.
- US-PROG-04: summary, delta tuần trước, union meal/metric active days, current/best.

## Quyết định có căn cứ

Constitution: progress_db riêng, gRPC đồng bộ, RabbitMQ/outbox/idempotency async,
contract trước code. BR-05 giữ BMI lịch sử. PRD định nghĩa streak bằng meal hoặc
body metric, delta so tuần trước; không lấy ví dụ API làm policy thay thế.
Pagination nội bộ và validation theo system-design; wire adapter là đề xuất Q07.

## Open questions / blocked contracts

Q01–Q09 trong research chưa approved. User chọn giữ PENDING để review kế hoạch;
đề xuất chiều cao nguyên theo source hiện tại vẫn chờ Nguyên reconcile API decimal.
Không công bố READY_FOR_FRONTEND/READY_FOR_BACKEND hoặc implementation-ready.

## Dependencies

- Nguyên: height contract, precision/presence/missing/time semantics; Shared auth với lead.
- Huy: meal activity add/edit/delete/revision/timezone/backfill/completeness, schema/topology.
- Duy: compose/scrape/routing/solution shared slots; digest contract feature riêng.
- Đức: future workout/longevity direction/inputs; không thuộc core increment hiện tại.
- Bảo/product/lead: timezone, fasting policy, biomarkers, water/sleep, dashboard baseline/windows,
  pagination/auth statuses và ADR các chiều gọi mới.

## Verification evidence

Chi tiết tại verification.md. Layout: 5 feature consistent; 37 unique task, 0 tick;
12 file hợp lệ, 57 relative link không hỏng. Format pass, build Debug/Release
0 warning/error; full suite 510 pass, 0 fail, 0 skip, gồm 70 integration test trên
Docker thật. Vulnerability scan 15 project không phát hiện package vulnerable.
Đã xử lý checkout LF và cấu hình Docker máy-local trên ổ E; không đổi gate,
business source, canonical contract hoặc migration để đạt kết quả.

## Ngoài phạm vi

AI facade/workout integration, weekly digest delivery/scheduler, thay đổi producer
Identity/Diet/Worker, wearable/payment/social và chỉnh Shared/hạ tầng chung.

## Reviewer checklist

- Xác nhận bốn story, FR/task coverage và các test scenario đủ dùng.
- Phê duyệt hoặc yêu cầu sửa Q01–Q09; không hiểu PENDING thành contract accepted.
- Kiểm tra không đổi business source/migration/canonical contract.
- Kiểm tra height snapshot BR-05, meal lifecycle/timezone/backfill và policy delta tuần trước.
- Kiểm tra shared-file coordination và ownership không giao sửa service người khác.
- Kiểm tra evidence gate mới nhất xanh trước nhận PR ready for review.
- Review chỉ Phase 1; không merge tự động hoặc bắt đầu Phase 2 trong phiên này.
