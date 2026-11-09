# ADR 005 - Lịch nghiệp vụ UTC cho MVP

- **Status**: Superseded by [ADR 006](006-business-timezone-vietnam.md)
- **Decision Date**: 2026-11-04
- **Decision Maker**: Bảo — owner Progress, thực hiện quyền quyết định Q01 do Huy (owner Diet) ủy quyền, theo xác nhận trực tiếp của Bảo trong yêu cầu close-out.

## 1. Context

Progress đã dùng UTC cho server-derived days, Diet lưu DateOnly client chọn.
MVP chưa có nguồn IANA timezone được phê duyệt. Một lịch UTC chung cho aggregation,
today và tuần tránh phụ thuộc cấu hình laptop/server hoặc browser. Quyết định
này chỉ đóng Q01; không phê duyệt tính năng/rule khác của Diet. Không ghi Huy
trực tiếp ký ADR hoặc review PR vì chưa có bằng chứng đó.

## 2. Decision

**Q01 — APPROVED FOR MVP CROSS-SERVICE UTC POLICY**.

- Ngày từ instant: `DateOnly.FromDateTime(instant.UtcDateTime)`; hôm nay:
  `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`.
- Water theo instant ghi; water/today theo UTC. Sleep theo WokeAt UTC; duration
  và overlap dựa full instants, giữ rule touching intervals hiện hành.
- Metric activity theo LoggedAt UTC; history [from UTC midnight, ngày kế tiếp
  toDate UTC midnight), không tính lại BMI/Height snapshots.
- Dashboard streak neo UTC hôm nay/hôm qua; tuần [Monday00:00UTC,nextMonday),
  fasting thuộc tuần của completion instant UTC. Cửa sổ bảy ngày theo UTC.
- Diet MealLog.Date là DateOnly client chỉ định. MealLoggedEvent.BusinessDate
  giữ nguyên, kể cả khác ngày UTC của CreatedAt/OccurredOn/delivery/replay.
- UI tự chọn “hôm nay” phải lấy ngày UTC; user chủ động chọn ngày khác giữ nguyên
  DateOnly đó, không convert timezone. Diet API default today dùng UTC;
  age/date calculations UTC hiện hữu giữ nguyên.
- Không lấy server-local, browser-local hay Asia/Ho_Chi_Minh làm lịch server.
  Không đổi timestamp, ngày meal hoặc phân nhóm lịch sử sau quyết định.

## 3. Consequences

UTC đơn giản, tất định giữa services, nhưng không phải ngày địa phương của user.
Việt Nam `2026-11-05 00:30 +07:00` là `2026-11-04 17:30Z`, business date04/11.
Đến `2026-11-05 07:00 +07:00` (=00:00Z), business date mới sang05/11.
Frontend phải giải thích giới hạn này; Huy phụ trách alignment Diet/frontend,
Bảo phụ trách Progress/test/handoff, Duy rollout, Nguyên Identity/auth ngoài Q01.

Future user-local policy cần quyết định versioned được các owner duyệt, nguồn
IANA timezone, quy tắc DST, effective time, migration/backfill/rebucketing và
compatibility/replay plan. Không tự chuyển semantics khi thêm timezone sau này.
Approval không chứng minh production replay đầy đủ, Dashboard completeness hoặc
service authentication; các blocker rollout/bảo mật đó vẫn độc lập.
