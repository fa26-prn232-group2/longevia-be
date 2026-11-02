# Diet Service — phần độc lập của dev 2

Owner ivorlee. Developer chốt catalog, target nhập tay, nhật ký, hạ tầng và test.

## User stories và acceptance

1. User đăng nhập tìm món chung và món riêng, tìm tên, lọc category, phân trang;
   tạo món custom. User A không thấy hoặc log được món riêng của user B.
2. User lưu/xem target của mình. Calo >0, macro không âm,
   4P+4C+9F trong ±5% mục tiêu. Bmr/tdee null trong giai đoạn nhập tay.
3. User log nhiều món theo ngày/bữa bằng quantity cùng servingUnit.
   100g món 165 kcal/31g protein, log 150g cho 247.5 kcal/46.5g protein.
   Một food không hợp lệ làm toàn request không được ghi.
4. Summary tổng hợp mọi log cùng ngày, gộp theo bữa; snapshot không tính lại
   khi catalog thay đổi. Thiếu target trả lỗi rõ, không tạo target giả.
5. Chạy local qua Nginx/Swagger; metrics Prometheus/Grafana.
   Email phát triển đi Mailpit, không đi provider thật.

Test hành vi và database thật; không khai báo BMR/TDEE/AI đã hoàn thành.
Xem [plan.md](plan.md), [deferred.md](deferred.md),
[PRD](../../docs/requirements/prd.md), [API](../../docs/api/api-spec.md).

## US6 — Diet cung cấp meal activity cho Progress

Developer yêu cầu ngày 2026-10-25: hoàn thiện phần Huy cung cấp cho các dev,
không đợi profile/weight. Mỗi meal tạo thành công phải có MealLoggedEvent v1 qua
transactional outbox, giữ ngày nghiệp vụ và EventId qua replay. Rollback không
để lại meal/event; broker gián đoạn giữ outbox để giao lại. Có replay lịch sử
snapshot/batch 100 sau khi consumer bind. Không thêm edit/delete hay HTTP admin.
Soạn file phối hợp riêng cho Nguyên, Bảo, Đức, Duy. Consumer thực vẫn do Bảo làm;
không tự đánh dấu toàn Streak READY bằng receiver trong test của Diet.

## US7 — Bộ đối soát và metadata thuộc Diet

Cung cấp snapshot operator v1 có danh sách meal activity, count và checksum để
Progress đối chiếu, export không ghi đè/không xuất file dở; không mở giao diện
liên service chưa có ADR. Catalog lưu allergens nullable do chủ món khai báo,
chuẩn hóa tags và giữ unknown khác declared-empty; không hứa an toàn dị ứng.
Thực hiện độc lập với profile/weight provider, không đẩy thành blocker của Đức/Bảo.
