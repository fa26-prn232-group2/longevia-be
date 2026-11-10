# ADR 006 - Lịch nghiệp vụ theo múi giờ Việt Nam (thay ADR 005)

- **Status**: Accepted
- **Date**: 2026-11-09
- **Supersedes**: ADR 005 - Lịch nghiệp vụ UTC cho MVP
- **Decision Maker**: Duck (owner feature `duck-005`), theo yêu cầu sản phẩm: người dùng Việt Nam

## Bối cảnh

ADR005 chốt mọi "ngày" do server suy ra dùng UTC. Thực tế người dùng Việt Nam thấy
ngày lệch 7 giờ: nước uống/nhật ký reset lúc 07:00 sáng, weekly digest chạy lệch
ngày, dashboard "hôm nay" sai vào buổi sáng. Sản phẩm hướng người Việt nên mốc
reset phải là nửa đêm giờ Việt Nam.

## Quyết định

1. **Ngày nghiệp vụ** (water, dashboard, meal daily summary, longevity, fasting
   weekly, weekly digest, streak, activity date) tính theo timezone cấu hình
   `APP_TIMEZONE`, mặc định `Asia/Ho_Chi_Minh`.
2. **Timestamp** (StartedAt, LoggedAt, CreatedAt, OccurredOn…) vẫn lưu UTC ISO 8601.
   Không đổi kiểu dữ liệu, không migration schema.
3. **Calo/macro/meal plan** không đổi: ngày meal log vẫn do client gửi `DateOnly`,
   FE gửi ngày địa phương.
4. **Không backfill** dữ liệu cũ. Các bản ghi được tạo trước khi deploy giữ nguyên
   nhãn ngày cũ; từ thời điểm deploy, ranh giới ngày mới áp dụng.
5. Timezone được resolve một lần khi khởi động; id không hợp lệ fallback vùng +7
   cố định để container không có tzdata vẫn chạy.

## Hệ quả

- Người dùng Việt Nam thấy ngày reset đúng nửa đêm local.
- Dữ liệu lịch sử quanh ranh giới 00:00–07:00 có thể nằm ở ngày cũ; đây là đánh đổi
  đã biết, không sửa vì không thể suy ra giờ thực tế lúc ghi.
- Test từng khẳng định UTC phải cập nhật sang +7.
- Tên `UtcCalendar` trong Progress được giữ để không phá call site, nhưng hành vi
  nay là business calendar.

## Phạm vi áp dụng

- Progress: `UtcCalendar.Day/StartOfWeek`, water, dashboard, longevity,
  sleep date, metric activity date, weekly digest scheduler.
- Diet: `DailySummary` mặc định ngày, meal recommendations, nutrient gaps,
  meal plan "hôm nay", tuổi từ ngày sinh.
- FE: helper `src/lib/business-date.ts` và các trang dùng ngày local.

## Liên quan

- Thay thế: `docs/decisions/005-mvp-utc-business-calendar.md`
- Cấu hình: `APP_TIMEZONE` (`.env.example`, `deployments/docker-compose.yml`)
