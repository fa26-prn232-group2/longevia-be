# Nhờ Nguyên review bổ sung profile phục vụ Diet

## Hiện đã làm

Đã merge PR #15. Theo yêu cầu developer, Huy bổ sung tương thích ngược vào
contracts/identity/v1/identity.proto: optional dietary_preference field5 và
repeated allergies field6. IdentityGrpcService map dữ liệu profile đang có và
validate dietary preference; IdentityGrpcTests assert đầy đủ. Không đổi DB hay
profile REST/event, không sửa tasks.md feature của Nguyên.

Diet.GetUserProfile client theo ADR004: deadline2s, không retry, phân biệt
NOT_FOUND/unavailable/timeout/malformed. BMR/TDEE và facade AI đã nối.

## Nhờ Nguyên thực hiện

1. Review proto, IdentityGrpcService.cs, IdentityGrpcTests.cs và ADR004 trong PR
   Diet sắp tới; giữ nguyên field numbers. Không cần code endpoint mới khác.
2. Khi deploy, cập nhật Identity có hai field này trước khi bật luồng meal AI.
   Server cũ thiếu optional field5 sẽ khiến Diet trả503 diet.profile_incomplete;
   không tự hiểu allergies absent là không dị ứng. BMR vẫn chạy với bốn field cũ.
3. Với profile tồn tại, [] nghĩa user khai báo không có dị ứng; không phải chứng
   nhận an toàn y tế. Xác nhận semantics này khi review.

Không còn thiếu endpoint profile để Huy code tiếp. Nguyên không cần tính BMR/TDEE
thay Diet. Evidence test và giới hạn rollout ở ../evidence.md.
