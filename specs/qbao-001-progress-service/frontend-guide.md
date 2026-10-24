# Bàn giao HTTP — Phase 2 foundation

Trạng thái: **IN_PROGRESS**. Phạm vi chỉ operational endpoints; chưa bàn giao
API nghiệp vụ cho frontend. Q08 vẫn PENDING Bảo + Nguyên, Phase 3–6 chưa triển khai.

Nginx bỏ prefix `/api/progress` trước khi gọi Progress. Development có Swagger
qua `/api/progress/swagger/index.html`, tài liệu `/api/progress/swagger/v1/swagger.json`;
OpenAPI server `/api/progress`, JWT HTTP Bearer. Production không expose Swagger.

| Method / gateway path | Auth | Response |
| --- | --- | --- |
| GET /api/progress/alive | Anonymous | 200 JSON `{ "status": "ok" }`, không cần DB. |
| GET /api/progress/health | Anonymous | 200 cùng JSON khi PostgreSQL sẵn sàng; 503 khi mất DB sau startup. |
| GET /api/progress/metrics | Anonymous, operational | 200 Prometheus text; đây không phải API body metrics. |

Không request field/enum hoặc userId selector. Không có route business nào trong
9 route đề xuất ở contracts/README.md. Lỗi tập trung Problem Details kèm traceId;
auth test probes chỉ được map trong test assembly, không ship trong service.
Sync hiện tại: 3 operational routes, 1 JSON field status, 0 request field/enum;
HTTP statuses operational 200/503; future business/status catalog chưa bàn giao.
Test/source và Docker evidence chi tiết trong verification.md; không đặt
READY_FOR_FRONTEND nhờ operational routes hay Q08 chưa approved.
