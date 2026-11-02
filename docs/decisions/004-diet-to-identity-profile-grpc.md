# ADR 004: Diet truy vấn profile Identity qua gRPC

- Ngày: 2026-11-02
- Trạng thái: Accepted — developer yêu cầu nối và hoàn thiện Diet sau PR #15.

## Quyết định

Cho phép Diet → Identity.GetUserProfile trên mạng nội bộ, contract canonical
contracts/identity/v1/identity.proto. Identity sở hữu profile; Diet không truy cập
identity_db hoặc import source Identity. Internal trust theo ADR 001, không thêm
auth riêng. Deadline 2 giây, không retry; cancellation truyền xuyên.

Bổ sung field 5 optional dietary_preference và field 6 repeated allergies tương
thích ngược. Presence field 5 xác nhận server cung cấp trọn bộ dietary/allergies;
server cũ không được mặc định thành không dị ứng. BMR/TDEE chỉ cần bốn field cũ.
Không tìm thấy profile → Diet 422; unavailable → 503; timeout → 504; dữ liệu sai
→ 502. AI thiếu dietary/allergies → 503 diet.profile_incomplete.

Diet lấy profile hiện tại và cân nặng as-of từ Progress (ADR 003), lưu inputs và
BMR/TDEE cùng target. Thay đổi profile/cân nặng không sửa target đã lưu. AI dùng
profile hiện tại, target và nhật ký ngày UTC; không lưu profile bên Recommendation.
