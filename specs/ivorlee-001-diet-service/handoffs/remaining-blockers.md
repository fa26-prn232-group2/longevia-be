# Phần còn chờ thật của Huy — Diet

Chỉ liệt kê dependency bên ngoài; không dùng việc chưa tự làm làm lý do bị block.
Code bàn giao thuộc nhánh feat/diet-service; feature vẫn open vì các dependency dưới đây.

## Nghĩa vụ Diet đã thực hiện

| Cung cấp cho | Phần Huy đã làm | Bằng chứng / cách dùng |
| --- | --- | --- |
| Bảo | MealLoggedEvent v1, outbox, replay stable IDs/ngày | PostgreSQL + RabbitMQ, rollback/reconnect/replay tests |
| Bảo | Snapshot danh sách meal activity, count/checksum, verifier | CLI export; đối chiếu đủ MealLogId/businessDate mà không đọc diet_db |
| Đức | Catalog allergens nullable, validation/normalization, persisted response | Unknown không tự thành danh sách an toàn; proto Đức giữ nguyên |
| Nguyên/Duy/Bảo | Mapping Compose JWT, token thật Identity → Diet | Register/email Mailpit/verify/login/catalog/target smoke |
| Tất cả | Handoff riêng, contract inventory và giới hạn readiness | dev1-nguyen.md, dev3-bao.md, dev4-duc.md, dev5-duy.md |

## Chỉ những phần bị block

| Chức năng | Đang chờ ai/cái gì | Điều kiện Huy tiếp tục |
| --- | --- | --- |
| BMR/TDEE tự động | Nguyên: profile provider; Bảo: weight provider; lead: ADR chiều gọi | Canonical contracts + provider có presence/provenance/deadline, rồi Huy adapter/công thức/snapshot target |
| REST meal AI (route thứ 7) | Profile/allergies/weight đáng tin, Đức/lead xác nhận provenance candidate/goal/mealType/citation mapping | Input contract đủ, không dựng health data giả; Huy gRPC adapter + endpoint/error mapping/test latency |
| Full Streak E2E | Bảo: consumer/inbox/projection; Duy/Bảo: queue/retry/DLQ + policy ngày metric | Huy đã cung cấp event/replay/snapshot; chạy integration với consumer thực sau khi có |
| JWT policy chung | Nguyên/Bảo/Duy/lead: missing kid, rotation, fail-start | Đường token hợp lệ đã chạy; policy lỗi cần thống nhất, không tự sửa Shared của cả nhóm |
| Weekly digest | Duy/Bảo/lead: schema/scheduler, recipient/consent, tuần/timezone, report ownership | Chốt contract/nguồn recipient và schedule, Huy cung cấp nutrition summary thuộc Diet, Worker chỉ gửi |

Không để Huy tự sửa code/migrations Identity/Progress/Recommendation/Worker.
Edit/delete meal và REST weekly plan chưa thuộc baseline API Diet đã duyệt; không
thêm tùy ý rồi gọi đó là blocker của các bạn. Không có task độc lập còn mở trong
phạm vi catalog/manual target/meal logging/provider meal activity đã được giao.
