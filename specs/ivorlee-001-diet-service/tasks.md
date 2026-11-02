# Công việc Diet độc lập

- [x] Đọc quy định, xác thực owner/nhánh và chốt phạm vi với developer.
- [x] Plan, Constitution Check và Shared Files & Conflict Hotspots.
- [x] Sáu project; test đỏ tính khẩu phần/validation.
- [x] Domain, repository, EF migration và seed idempotent.
- [x] Catalog, target nhập tay, meal log và daily summary.
- [x] Auth, Swagger, validation/errors và observability.
- [x] Integration PostgreSQL: isolation, atomicity, summary, auth.
- [x] Docker/Nginx smoke; Mailpit/Grafana/Prometheus local.
- [x] Gate và hướng dẫn bàn giao với evidence/sync counts.

Có đổi HTTP surface: cập nhật frontend-guide.md. Giai đoạn độc lập không đổi gRPC/event; giai đoạn tích hợp bên dưới bổ sung event
và được ghi trong backend-integration-guide.md.

Bằng chứng: [evidence.md](evidence.md). Checklist này chỉ đóng phạm vi độc lập
đã được developer chọn. Feature vẫn open vì [phần hoãn](deferred.md) còn thiếu.

## Giai đoạn cung cấp tích hợp

- [x] Khai báo shared hotspots và contract-first MealLoggedEvent v1.
- [x] Producer outbox cùng transaction meal, không đổi REST surface.
- [x] Replay snapshot meal cũ, stable identity, không tự declare queue Progress.
- [x] Soạn Markdown riêng cho Nguyên, Bảo, Đức, Duy trong handoffs/.
- [x] Kiểm chứng rollback/broker/replay nhiều batch và negative control.
- [x] Gate, migration và compose smoke cập nhật.
- [x] Sửa mapping Compose Identity và kiểm chứng token thật Identity → Diet (6 assertion).

Không thêm HTTP endpoint/DTO ở giai đoạn event; counted frontend giữ 6/13/73.

## Hoàn tất nghĩa vụ Diet không bị block

- [x] Contract snapshot v1 và CLI export read-only, streaming, không overwrite.
- [x] Verifier count/checksum/IDs và hướng dẫn đối soát cho Bảo.
- [x] Metadata allergens nullable, validation/normalization/EF migration/API docs.
- [x] PostgreSQL metadata + snapshot tests; verifier negative control.
- [x] Gate cuối và kiểm chứng CLI Docker trên DB local.
- [x] Handoffs/remaining-blockers.md chỉ giữ phụ thuộc bên ngoài.

Giai đoạn metadata thêm hai field: frontend counts hiện 6 operation / 13 DTO /
75 field; phần 73 field ở evidence cũ là lịch sử trước metadata.

## Q10 Diet client

- [x] Codegen từ proto canonical; DI và Docker copy contract.
- [x] Deadline/cancellation, missing khác unavailable, decimal/provenance validation.
- [x] Internal calculate-target use case và snapshot DB; public route còn chờ full profile.

Không đổi HTTP surface: vẫn manual target; không cần client sửa request/response.

- [x] Domain Mifflin-St Jeor, calculated target factory và immutable input/result.
- [x] Migration nullable snapshot, atomic upsert và manual replacement clear.
- [x] Internal CalculateAsync nối weight/provider → calculation → repository.
- [x] Public calculate-target API: Identity profile + Progress weight + snapshot atomic.

## Meal AI adapter

- [x] Codegen Recommendation client, DI và Docker/network nội bộ.
- [x] Map profile/candidate/macro và phục hồi catalog food ID/serving trong result.
- [x] Loại unknown allergens/foreign-owned foods; kiểm tra citation/disclaimer.
- [x] Explicit transport errors và caller deadline budget.
- [x] Public AI facade/full-profile integration, ngân sách 2.8s và error mapping.
- [x] Kiểm chứng xuyên server HTTP/2 thật và cập nhật evidence (1 probe, model/corpus fixture).

Không đổi HTTP endpoint/event/DTO client ở phase adapter; frontend vẫn 6/13/75.

## Hoàn thiện facade — 2026-11-02

- [x] Bổ sung dietary_preference/allergies, Identity server mapping và test.
- [x] ADR 004, boundary docs và Diet profile client deadline/error/presence.
- [x] POST calculate dùng profile thật, giữ bốn mục tiêu user chọn.
- [x] GET meal AI, lọc ownership/unknown/tag trước limit 100, DTO HTTP riêng.
- [x] Test âm endpoint 404 trước triển khai, test snapshot/summary/presence/errors.
- [x] API spec, frontend/backend guides và bàn giao từng người cập nhật.
