# Bàn giao tích hợp Diet — Huy (dev 2)

Ngày 2026-10-25. Đây là tài liệu để Huy gửi từng người; agent chưa gửi thay Huy.

| Người nhận | File | Việc chính |
| --- | --- | --- |
| Nguyên — dev 1 Identity | [dev1-nguyen.md](dev1-nguyen.md) | Profile provider, JWT/claim policy |
| Bảo — dev 3 Progress | [dev3-bao.md](dev3-bao.md) | Consume MealLoggedEvent, bootstrap và cung cấp weight |
| Đức — dev 4 Recommendation | [dev4-duc.md](dev4-duc.md) | Meal AI request/response và phần dữ liệu còn thiếu |
| Duy — dev 5 Worker/Gateway | [dev5-duy.md](dev5-duy.md) | Broker/topology/deployment, digest contract |

Producer Diet được bàn giao qua nhánh feat/diet-service.
Đọc [backend-integration-guide.md](../backend-integration-guide.md) và evidence
trước khi tích hợp. Không coi consumer Progress đã tồn tại hoặc READY toàn hệ thống.

Bảng nghĩa vụ đã làm và chỉ phần bị block thật: [remaining-blockers.md](remaining-blockers.md).
Bảo có thêm snapshot/checksum CLI; Đức có thêm metadata allergens của catalog.
