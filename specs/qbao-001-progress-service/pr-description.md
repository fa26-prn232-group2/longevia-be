# Progress Service — Phase 1 specification và implementation plan

**Tiêu đề**: docs(progress): Phase 1 specification and implementation plan
**Base**: main | **Head**: feat/progress-service | **Feature**: qbao/001/progress-service

Đề nghị review tài liệu Phase 1 và các quyết định PENDING trước khi triển khai.
Không công bố trạng thái implementation-ready.

## Summary

Đặc tả Progress Service cho body metrics/BMI, fasting, water/sleep và dashboard/streaks;
phân rã roadmap Phase 2–6 với các dependency chưa có contract được chặn rõ ràng.

Corrective theo main/base `418d41b82c4eca846995ed3d505736f0ba7b0c5e` sau PR #8:
Diet đã có Food Catalog, Manual Targets, Meal Logging create-only, Daily Summary,
EF diet_db, auth/tests/Docker. Còn thiếu meal activity contract/outbox và tích hợp
Progress projection/bootstrap/reconciliation/E2E; Q09 vẫn BLOCKED/PENDING INTEGRATION.
Streak dùng MealLog.Date (DateOnly client cấp), không timestamp publish/receive;
CreatedAt và ProcessedAt UTC có ý nghĩa riêng. Future edit/delete/change-date cần
contract/version/revision/tombstone và rollout, không giả endpoints đã có.

## Phạm vi Phase 1

Chỉ tài liệu trong specs/qbao-001-progress-service. Không business code, migration,
canonical contract, thay đổi Gateway/Worker hoặc source thành viên khác.

## Artifacts

meta.json, spec.md, plan.md, tasks.md, research.md, data-model.md,
contracts/README.md, quickstart.md, deferred.md, checklists/requirements.md,
verification.md và nội dung PR này. 19 FR, 7 SC, 38 task chưa thực hiện (thêm T038).

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

Q01–Q10 trong research chưa approved. User chọn giữ PENDING để review kế hoạch;
đề xuất chiều cao nguyên theo source hiện tại vẫn chờ Nguyên reconcile API decimal.
Không công bố READY_FOR_FRONTEND/READY_FOR_BACKEND hoặc implementation-ready.
Q10 đề xuất Progress weight read provider cho Diet BMR/TDEE, cần duyệt latest/asOf/
effective-time, absence/null, future/backdate/stale, provenance, refresh và deadline.
Diet sở hữu thuật toán/consumer; Identity sở hữu profile. Diet → Progress/Identity
cần lead ADR + canonical contract trước code. T038 chỉ tracking, implementation
provider deferred riêng, không chặn BMI core. T002/T003 skeleton không chờ Q01/Q08;
security acceptance vẫn chờ Q08, business date logic vẫn chờ Q01.

## Dependencies

- Nguyên: height contract, precision/presence/missing/time semantics; Shared auth với lead.
- Huy + Bảo: Q09 create event/outbox/bootstrap/completeness; future lifecycle có điều kiện;
  Q10 weight provider Progress / consumer Diet cho BMR/TDEE, phối hợp Nguyên + lead ADR.
- Duy: compose/scrape/routing/solution shared slots; digest contract feature riêng.
- Đức: future workout/longevity direction/inputs; không thuộc core increment hiện tại.
- Bảo/product/lead: timezone, fasting policy, biomarkers, water/sleep, dashboard baseline/windows,
  pagination/auth statuses và ADR các chiều gọi mới.

## Verification evidence

Chi tiết SHA/thời điểm/nguồn và giới hạn tại verification.md. Layout 5 feature
consistent; 38 unique task, 0 tick; FR 19/19, SC 7/7 có coverage; không vòng dependency.

- Historical local trước Diet: 510 pass/0 fail/0 skip, scan 15 project sạch.
- CI push head 70d4faa2: [run 40112349086](https://github.com/fa26-prn232-group2/longevia-be/actions/runs/40112349086), success; 510/15, không phải 537/21.
- CI PR head 70d4faa2, merge ref 58a4f801 vào base 418d41b8: [run 40112359186](https://github.com/fa26-prn232-group2/longevia-be/actions/runs/40112359186), success; 537 pass/0 fail/0 skip, 21 project sạch, Release 0 warning/error.
- Local corrective đã chạy lại source HEAD 70d4faa2 cùng docs sửa: format, Debug/Release warnings-as-errors pass; 510 pass/0 fail/0 skip, 15 project sạch. Branch chưa merge Diet nên không claim local 537.
- CI của corrective HEAD phải kiểm tra sau push; các run cũ không chứng minh new HEAD. Trạng thái mới nhất ghi riêng ở mô tả PR khi xuất bản.

## Ngoài phạm vi

AI facade/workout integration, weekly digest delivery/scheduler, thay đổi producer
Identity/Diet/Worker, wearable/payment/social và chỉnh Shared/hạ tầng chung.

## Reviewer checklist

- Xác nhận bốn story, FR/task coverage và các test scenario đủ dùng.
- Phê duyệt hoặc yêu cầu sửa Q01–Q10; không hiểu PENDING thành contract accepted.
- Kiểm tra không đổi business source/migration/canonical contract.
- Kiểm tra height snapshot BR-05, meal lifecycle/timezone/backfill và policy delta tuần trước.
- Kiểm tra shared-file coordination và ownership không giao sửa service người khác.
- Kiểm tra evidence gate mới nhất xanh trước nhận PR ready for review.
- Review chỉ Phase 1; không merge tự động hoặc bắt đầu Phase 2 trong phiên này.
