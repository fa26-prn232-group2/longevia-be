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
